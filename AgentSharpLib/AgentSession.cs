using AgentSharpLib.Agent;
using AgentSharpLib.Context;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;
using AgentSharpLib.Output;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;
using AgentSharpLib.Tools.Implementations;
using AgentSharpLib.Transcripts;

namespace AgentSharpLib;

/// <summary>
/// A ready-to-use agent: one conversation plus everything wired around it (LLM client,
/// tools, approval gate, output, project context, memory). Built by
/// <see cref="AgentBuilder"/>. The conversation can be restarted -- optionally with a
/// different persona -- or replaced with a saved history without rebuilding the rest.
/// </summary>
public sealed class AgentSession
{
    private readonly string? _customSystemPrompt;
    private string? _superPrompt;
    private bool _useCustomSystemPrompt;
    private readonly HandoffQueue? _handoff;
    private string? _handoffContext;
    private readonly List<string> _handoffStack = [];

    public ILlmClient Llm { get; }
    public ToolRegistry Tools { get; }
    public ApprovalGate Approval { get; }
    public IAgentOutput Output { get; }
    public ProjectContext Project { get; }

    /// <summary>Project memory (MEMORY.md), or null if built without memory.</summary>
    public MemoryManager? Memory { get; }

    public int MaxTokens { get; }
    public int MaxIterations { get; }

    /// <summary>Elapsed time and tokens consumed over the whole session -- every
    /// conversation (across <see cref="Reset"/> and <see cref="Restore"/>) and every
    /// sub-agent. <see cref="Loop"/>'s own totals cover only the current conversation.</summary>
    public SessionUsage Usage { get; }

    /// <summary>The active persona name passed to --Superprompt / <see cref="Reset"/>;
    /// null means the default. Ignored while a custom system prompt is in use.</summary>
    public string? SuperPrompt => _superPrompt;

    /// <summary>The agent loop running the current conversation. Replaced by
    /// <see cref="Reset"/> and <see cref="Restore"/>.</summary>
    public AgentLoop Loop { get; private set; }

    public ConversationHistory History => Loop.History;
    public string SystemPrompt => Loop.SystemPrompt;

    /// <summary>Raised when a tool starts, whichever conversation is current: (toolName, inputSummary).</summary>
    public event Action<string, string>? OnToolStart;

    /// <summary>Raised when a tool finishes (or is denied), whichever conversation is current.</summary>
    public event Action<string, ToolResult>? OnToolEnd;

    internal AgentSession(
        ILlmClient llm,
        ToolRegistry tools,
        ApprovalGate approval,
        IAgentOutput output,
        ProjectContext project,
        MemoryManager? memory,
        int maxTokens,
        int maxIterations,
        string? superPrompt,
        string? customSystemPrompt,
        SessionUsage usage,
        HandoffQueue? handoff = null)
    {
        _handoff = handoff;
        Usage = usage;
        Llm = llm;
        Tools = tools;
        Approval = approval;
        Output = output;
        Project = project;
        Memory = memory;
        MaxTokens = maxTokens;
        MaxIterations = maxIterations;
        _superPrompt = superPrompt;
        _customSystemPrompt = customSystemPrompt;
        _useCustomSystemPrompt = customSystemPrompt is not null;
        Loop = CreateLoop(history: null);
    }

    /// <summary>
    /// Send one user message and run the agent until it produces a final answer
    /// (calling tools along the way). Streams unless <see cref="AgentFlags.SyncMode"/>
    /// is set. Returns the assistant's text for the turn.
    /// </summary>
    public async Task<string> SendAsync(string message, CancellationToken ct = default, PromptKind kind = PromptKind.Std)
    {
        try
        {
            return await (AgentFlags.SyncMode
                ? Loop.RunTurnNonStreamingAsync(message, ct, kind)
                : Loop.RunTurnStreamingAsync(message, ct, kind));
        }
        finally
        {
            // A handoff the model asked for during the turn takes effect now, for the next prompt.
            if (_handoff?.Take() is { } request)
            {
                if (request.Persona == SystemPromptBuilder.ReturnTarget)
                    Return(request.Reason);
                else
                    Handoff(request.Persona, request.Reason);
            }
        }
    }

    /// <summary>
    /// Start a fresh conversation. The system prompt is rebuilt, so it picks up any
    /// MEMORY.md changes. Passing <paramref name="superPrompt"/> switches persona (and
    /// replaces a custom system prompt); an unknown name throws
    /// <see cref="ArgumentException"/> and leaves the session unchanged.
    /// </summary>
    public void Reset(string? superPrompt = null)
    {
        if (superPrompt is not null)
        {
            SystemPromptBuilder.ResolveSuperPrompt(superPrompt); // validate before changing anything
            _superPrompt = superPrompt;
            _useCustomSystemPrompt = false;
        }
        _handoffContext = null;
        _handoffStack.Clear();
        _handoff?.Take();
        Loop = CreateLoop(history: null);
    }

    /// <summary>
    /// Continue a previously saved conversation (see SessionManager). Pass the
    /// <see cref="HandoffState"/> saved with it to make it returnable again: the persona that
    /// was in charge takes over and the stack of personas that handed it over is restored.
    /// Without one, the current persona stays and the stack is empty. A state naming an
    /// unknown persona is ignored.
    /// </summary>
    public void Restore(ConversationHistory history, HandoffState? handoff = null)
    {
        _handoffContext = null;
        _handoffStack.Clear();
        _handoff?.Take();
        if (handoff is not null && !_useCustomSystemPrompt)
        {
            try
            {
                var persona = SystemPromptBuilder.ResolveKey(handoff.Persona);
                var stack = handoff.Stack.Select(SystemPromptBuilder.ResolveKey).ToList();
                _superPrompt = persona;
                _handoffStack.AddRange(stack);
            }
            catch (ArgumentException)
            {
                // A session file naming a persona this build doesn't have: load the history only.
            }
        }
        Loop = CreateLoop(history);
    }

    /// <summary>The personas that handed this conversation over, the most recent last; empty if
    /// it was never handed over (or has been returned all the way).</summary>
    public IReadOnlyList<string> HandoffStack => _handoffStack;

    /// <summary>What to save with the conversation so it can be returned after a /load, or null
    /// when there is nothing to return to.</summary>
    public HandoffState? HandoffState =>
        _handoffStack.Count == 0 || _useCustomSystemPrompt
            ? null
            : new HandoffState(_handoffStack.ToList(), SystemPromptBuilder.ResolveKey(_superPrompt));

    /// <summary>
    /// Return the conversation to the persona that handed it over, popping it off
    /// <see cref="HandoffStack"/>. The same history continues; the persona's system prompt says
    /// the conversation was returned to it. Applies to the next prompt. Throws
    /// <see cref="InvalidOperationException"/> if there is nothing to return to.
    /// </summary>
    public void Return(string? reason = null)
    {
        if (_handoffStack.Count == 0)
            throw new InvalidOperationException("Nothing to return to: this conversation has not been handed over.");

        var to = _handoffStack[^1];
        _handoffStack.RemoveAt(_handoffStack.Count - 1);
        ApplyHandoff(to, reason, returning: true);
    }

    /// <summary>
    /// Hand the current conversation to another persona: the same history continues under the
    /// new persona's system prompt, which says who handed it over and why. Applies to the next
    /// prompt. An unknown name throws <see cref="ArgumentException"/> and leaves the session
    /// unchanged. Switching to the persona already in charge does nothing. Unlike the
    /// <c>handoff</c> tool, any persona is allowed here, not just
    /// <see cref="SystemPromptBuilder.HandoffPersonas"/>.
    /// </summary>
    public void Handoff(string superPrompt, string? reason = null)
    {
        var to = SystemPromptBuilder.ResolveKey(superPrompt); // validate before changing anything
        if (!_useCustomSystemPrompt && to == SystemPromptBuilder.ResolveKey(_superPrompt))
            return;

        // The persona being left goes on the stack so the conversation can be returned to it.
        // A custom system prompt is not a persona, so there is nothing to return to.
        if (!_useCustomSystemPrompt)
            _handoffStack.Add(SystemPromptBuilder.ResolveKey(_superPrompt));
        ApplyHandoff(to, reason, returning: false);
    }

    /// <summary>Switch to <paramref name="to"/> on the existing history and say so; the caller
    /// has already updated <see cref="_handoffStack"/>.</summary>
    private void ApplyHandoff(string to, string? reason, bool returning)
    {
        // Two spellings of each persona: the nickname ("ray") is what the model is told, as it is what the
        // handoff tool takes; the full name ("Ray Pando") is for people, on the console and in transcripts.
        var fromKey = _useCustomSystemPrompt ? "the previous assistant" : SystemPromptBuilder.ResolveKey(_superPrompt);
        var fromName = _useCustomSystemPrompt ? "the previous assistant" : SystemPromptBuilder.ResolveAgentName(_superPrompt);
        var toName = SystemPromptBuilder.ResolveAgentName(to);
        var verb = returning ? "returned" : "handed over";
        var because = string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim().TrimEnd('.', ' ')}.";

        _superPrompt = to;
        _useCustomSystemPrompt = false;
        _handoffContext =
            $"This conversation was {verb} to you by {fromKey}.{because} " +
            $"Assistant turns earlier in the history were written by {fromKey} or an earlier persona" +
            (returning ? ", including you before the handoff" : "") + ", " +
            $"not by you now. You are {SystemPromptBuilder.ResolveKey(to)}: continue from where it left off, in your own voice.";
        Loop = CreateLoop(Loop.History);

        // A record in the history, so the saved session and its transcript show where the persona changed
        // (the same text goes to the console). Full names; never sent to the provider.
        var record = $"{(returning ? "Return" : "Handoff")}: {fromName} -> {toName}" +
                     $"{(string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason.Trim()})")}. Handoff stack: {DescribeHandoffStack()}";
        Loop.History.AddHandoffRecord(record);
        Output.Handoff(record);
    }

    /// <summary>
    /// The handoff stack on one line for people (the console and transcripts), by full name, oldest first
    /// and ending with the persona in charge: "Andy Pando > Ray Pando > Donald J. Pando (current)";
    /// "(empty)" when the conversation was never handed over (or has been returned all the way). The
    /// model is shown the same stack by nickname, in the system prompt.
    /// </summary>
    public string DescribeHandoffStack() =>
        _handoffStack.Count == 0
            ? "(empty)"
            : $"{string.Join(" > ", _handoffStack.Select(SystemPromptBuilder.ResolveAgentName))} > {SystemPromptBuilder.ResolveAgentName(_superPrompt)} (current)";

    /// <summary>
    /// Write a Q&amp;A transcript of the current conversation into the project
    /// directory and return its path, headed by the session's elapsed time and tokens
    /// consumed so far. See <see cref="ConversationTranscript.Write"/> for naming,
    /// format, and the exceptions it throws.
    /// </summary>
    public string WriteTranscript(string name, TranscriptOptions? options = null) =>
        ConversationTranscript.Write(History, SystemPrompt, Project.WorkingDirectory, name, Usage, options);

    private AgentLoop CreateLoop(ConversationHistory? history)
    {
        var returnTo = _handoffStack.Count > 0 ? _handoffStack[^1] : null;
        if (_handoff is not null)
        {
            _handoff.CurrentPersona = _useCustomSystemPrompt ? "" : SystemPromptBuilder.ResolveKey(_superPrompt);
            _handoff.ReturnTo = returnTo;
        }

        var systemPrompt = _useCustomSystemPrompt
            ? _customSystemPrompt!
            : new SystemPromptBuilder(Project, Memory, _superPrompt, offerHandoff: _handoff is not null,
                handoffContext: _handoffContext, returnTo: returnTo, handoffStack: _handoffStack).Build();

        var loop = new AgentLoop(Llm, Tools, Approval, systemPrompt, history, MaxTokens, MaxIterations, Output, Usage);
        loop.OnToolStart += (name, summary) => OnToolStart?.Invoke(name, summary);
        loop.OnToolEnd += (name, result) => OnToolEnd?.Invoke(name, result);
        return loop;
    }
}
