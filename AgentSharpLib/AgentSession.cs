using AgentSharpLib.Agent;
using AgentSharpLib.Context;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;
using AgentSharpLib.Output;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;

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

    public ILlmClient Llm { get; }
    public ToolRegistry Tools { get; }
    public ApprovalGate Approval { get; }
    public IAgentOutput Output { get; }
    public ProjectContext Project { get; }

    /// <summary>Project memory (MEMORY.md), or null if built without memory.</summary>
    public MemoryManager? Memory { get; }

    public int MaxTokens { get; }
    public int MaxIterations { get; }

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
        string? customSystemPrompt)
    {
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
    public Task<string> SendAsync(string message, CancellationToken ct = default) =>
        AgentFlags.SyncMode
            ? Loop.RunTurnNonStreamingAsync(message, ct)
            : Loop.RunTurnStreamingAsync(message, ct);

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
        Loop = CreateLoop(history: null);
    }

    /// <summary>Continue a previously saved conversation (see SessionManager).</summary>
    public void Restore(ConversationHistory history) => Loop = CreateLoop(history);

    private AgentLoop CreateLoop(ConversationHistory? history)
    {
        var systemPrompt = _useCustomSystemPrompt
            ? _customSystemPrompt!
            : new SystemPromptBuilder(Project, Memory, _superPrompt).Build();

        var loop = new AgentLoop(Llm, Tools, Approval, systemPrompt, history, MaxTokens, MaxIterations, Output);
        loop.OnToolStart += (name, summary) => OnToolStart?.Invoke(name, summary);
        loop.OnToolEnd += (name, result) => OnToolEnd?.Invoke(name, result);
        return loop;
    }
}
