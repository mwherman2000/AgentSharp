using AgentSharpLib.Agent;
using AgentSharpLib.Agent.MultiAgent;
using AgentSharpLib.Context;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;
using AgentSharpLib.Output;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;
using AgentSharpLib.Tools.Implementations;

namespace AgentSharpLib;

/// <summary>
/// Assembles an <see cref="AgentSession"/> with sensible defaults, so a host can go
/// from settings to a working agent in a few lines:
/// <code>
/// var agent = await new AgentBuilder()
///     .WithOptions(new AgentOptions { Provider = "anthropic", ApiKey = key })
///     .BuildAsync();
/// var answer = await agent.SendAsync("Summarize this repository.");
/// </code>
/// By default the agent gets every built-in tool, the sub_agent and remember tools,
/// project memory (MEMORY.md), and a system prompt built from the selected persona
/// plus a scan of the current directory. Output is discarded and Destructive tools
/// are denied unless you supply <see cref="WithOutput"/> and
/// <see cref="WithApprovalPrompt"/>.
///
/// Files, shell commands, the project scan and MEMORY.md all resolve against the
/// process's current directory. The builder never changes it --
/// <see cref="AgentOptions.WorkingDirectory"/> is not applied here; set
/// <see cref="Directory.SetCurrentDirectory"/> first if you need a different root.
/// </summary>
public sealed class AgentBuilder
{
    private AgentOptions _options = new();
    private ILlmClient? _llm;
    private IAgentOutput? _output;
    private IApprovalPrompt? _approvalPrompt;
    private string? _superPrompt;
    private string? _systemPrompt;
    private readonly List<ITool> _tools = new();
    private bool _builtInTools = true;
    private bool _subAgents = true;
    private bool _memory = true;
    private bool _scanProject = true;

    /// <summary>Provider, model, key, limits and persona. The builder reads but never
    /// modifies this object.</summary>
    public AgentBuilder WithOptions(AgentOptions options)
    {
        _options = options;
        return this;
    }

    /// <summary>Use this client instead of creating one from the options (e.g. a
    /// custom provider or a test fake).</summary>
    public AgentBuilder WithLlmClient(ILlmClient llm)
    {
        _llm = llm;
        return this;
    }

    /// <summary>Where model text and status events go. Default: discarded.</summary>
    public AgentBuilder WithOutput(IAgentOutput output)
    {
        _output = output;
        return this;
    }

    /// <summary>Who approves Destructive tool calls. Default: none, so they are denied.</summary>
    public AgentBuilder WithApprovalPrompt(IApprovalPrompt prompt)
    {
        _approvalPrompt = prompt;
        return this;
    }

    /// <summary>Persona to use (one of <see cref="SystemPromptBuilder.AvailableSuperPrompts"/>). Overrides
    /// <see cref="AgentOptions.SuperPrompt"/>.</summary>
    public AgentBuilder WithSuperPrompt(string superPrompt)
    {
        _superPrompt = superPrompt;
        return this;
    }

    /// <summary>Use this exact system prompt instead of a persona. Sub-agents get it too.</summary>
    public AgentBuilder WithSystemPrompt(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        return this;
    }

    /// <summary>Register an extra tool. Registered after the built-ins, so a tool with
    /// the same name replaces the built-in one.</summary>
    public AgentBuilder WithTool(ITool tool)
    {
        _tools.Add(tool);
        return this;
    }

    /// <summary>Don't auto-register the built-in tools (read_file, run_shell, web_fetch, ...).</summary>
    public AgentBuilder WithoutBuiltInTools()
    {
        _builtInTools = false;
        return this;
    }

    /// <summary>Don't register the sub_agent tool.</summary>
    public AgentBuilder WithoutSubAgents()
    {
        _subAgents = false;
        return this;
    }

    /// <summary>Don't use MEMORY.md: no remember tool, and no memory in the system prompt.</summary>
    public AgentBuilder WithoutMemory()
    {
        _memory = false;
        return this;
    }

    /// <summary>Skip scanning the current directory (git, instructions file, file
    /// tree) for the system prompt.</summary>
    public AgentBuilder WithoutProjectScan()
    {
        _scanProject = false;
        return this;
    }

    /// <summary>
    /// Build the session. Throws <see cref="InvalidOperationException"/> if no LLM
    /// client was given and one can't be created from the options (e.g. missing API
    /// key), and <see cref="ArgumentException"/> for an unknown persona.
    /// </summary>
    public async Task<AgentSession> BuildAsync(CancellationToken ct = default)
    {
        var superPrompt = _superPrompt ?? _options.SuperPrompt;
        if (_systemPrompt is null)
            SystemPromptBuilder.ResolveSuperPrompt(superPrompt); // fail fast on an unknown persona

        var llm = _llm ?? LlmClientFactory.Create(_options);
        var output = _output ?? NullAgentOutput.Instance;
        var maxTokens = _options.MaxTokens ?? AgentLoop.DefaultMaxTokens;
        var maxIterations = _options.MaxIterations ?? AgentLoop.DefaultMaxIterations;

        var tools = new ToolRegistry();
        if (_builtInTools)
            tools.DiscoverTools(typeof(ToolRegistry).Assembly, output);

        var approval = new ApprovalGate(_approvalPrompt, output);

        var project = new ProjectContext();
        if (_scanProject)
            await project.RefreshAsync(ct);

        var memory = _memory ? new MemoryManager() : null;

        if (_subAgents)
        {
            var subAgentPrompt = _systemPrompt ?? new SystemPromptBuilder(project, memory, superPrompt).Build();
            var orchestrator = new AgentOrchestrator(llm, tools, approval, subAgentPrompt, maxTokens, maxIterations, output: output);
            tools.Register(new SubAgentTool(orchestrator));
        }
        if (memory is not null)
            tools.Register(new MemoryTool(memory));
        foreach (var tool in _tools)
            tools.Register(tool);

        return new AgentSession(llm, tools, approval, output, project, memory, maxTokens, maxIterations, superPrompt, _systemPrompt);
    }
}
