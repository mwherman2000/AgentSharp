using AgentSharp;
using AgentSharp.Agent.MultiAgent;
using AgentSharp.Context;
using AgentSharp.Memory;
using AgentSharp.Safety;
using AgentSharp.Tools;
using AgentSharp.Tools.Implementations;
using AgentSharp.Ui;
using Spectre.Console;

// ============================================================================
// AgentSharp - AI Coding Agent CLI
//
// Built with architectural patterns from Claude Code:
//   - The Agent Loop (think -> decide -> execute -> observe -> repeat)
//   - Tool Registry with auto-discovery
//   - Safety & Approval Gates
//   - Project Context Awareness
//   - Streaming LLM responses
//   - Session persistence
// ============================================================================

// Handle --help and --version
if (args.Contains("--help") || args.Contains("-h"))
{
    PrintUsage();
    return;
}

if (args.Contains("--version") || args.Contains("-v"))
{
    AnsiConsole.MarkupLine("[bold]AgentSharp[/] v0.1.0");
    return;
}

try
{
    // --- Configuration ---
    var config = Configuration.Load(args);

    // --- LLM Client ---
    var llm = config.CreateLlmClient();

    // --- Tool Registry (auto-discover all ITool implementations) ---
    var tools = new ToolRegistry();
    tools.DiscoverTools();

    // --- Safety ---
    var approval = new ApprovalGate();

    // --- Multi-Agent Orchestrator ---
    // Create orchestrator and register the sub_agent tool
    // (must be done after tool discovery since SubAgentTool requires constructor args)
    var promptBuilder = new AgentSharp.Context.SystemPromptBuilder(new ProjectContext());
    var orchestrator = new AgentOrchestrator(llm, tools, approval, promptBuilder.Build());
    tools.Register(new SubAgentTool(orchestrator));

    // --- Project Context ---
    var project = new ProjectContext();
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Scanning project...", async ctx =>
        {
            await project.RefreshAsync();
        });

    // --- Memory ---
    var sessions = new SessionManager();
    var memory = new MemoryManager();

    // --- Check for one-shot mode (prompt passed as argument) ---
    var promptArg = GetPromptArgument(args);
    if (promptArg is not null)
    {
        // One-shot mode: run a single turn and exit
        var oneShotPromptBuilder = new AgentSharp.Context.SystemPromptBuilder(project);
        var agentLoop = new AgentSharp.Agent.AgentLoop(llm, tools, approval, oneShotPromptBuilder.Build());
        await agentLoop.RunTurnAsync(promptArg);
        return;
    }

    // --- Interactive REPL ---
    var repl = new ReplHost(llm, tools, approval, project, sessions, memory);
    await repl.RunAsync();
}
catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
{
    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Example:[/]");
    AnsiConsole.MarkupLine("  [green]export ANTHROPIC_API_KEY=sk-ant-...[/]");
    AnsiConsole.MarkupLine("  [green]agentsharp[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Or pass directly:[/]");
    AnsiConsole.MarkupLine("  [green]agentsharp --api-key sk-ant-...[/]");
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Fatal error:[/] {Markup.Escape(ex.Message)}");
    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(ex.StackTrace ?? "")}[/]");
    Environment.ExitCode = 1;
}

static string? GetPromptArgument(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--prompt" && i + 1 < args.Length)
            return args[i + 1];

        // Support: agentsharp "do something"
        // (first non-flag argument)
        if (!args[i].StartsWith('-') && !IsValueOfPreviousFlag(args, i))
            return args[i];
    }
    return null;
}

static bool IsValueOfPreviousFlag(string[] args, int index)
{
    if (index == 0) return false;
    var prev = args[index - 1];
    return prev is "--provider" or "-p" or "--model" or "-m"
        or "--api-key" or "-k" or "--base-url" or "--prompt";
}

static void PrintUsage()
{
    AnsiConsole.MarkupLine("[bold]AgentSharp[/] - AI Coding Agent CLI");
    AnsiConsole.MarkupLine("Built with patterns from Claude Code\n");
    AnsiConsole.MarkupLine("[bold]USAGE:[/]");
    AnsiConsole.MarkupLine("  agentsharp                          Start interactive REPL");
    AnsiConsole.MarkupLine("  agentsharp \"fix the bug in main.cs\"  One-shot mode");
    AnsiConsole.MarkupLine("  agentsharp --prompt \"explain this\"   One-shot mode (explicit)\n");
    AnsiConsole.MarkupLine("[bold]OPTIONS:[/]");
    AnsiConsole.MarkupLine("  -p, --provider <name>    LLM provider: anthropic, openai, grok, gemini");
    AnsiConsole.MarkupLine("  -m, --model <name>       Model identifier (e.g., claude-sonnet-4-20250514, gpt-4o)");
    AnsiConsole.MarkupLine("  -k, --api-key <key>      API key (or set via environment variable)");
    AnsiConsole.MarkupLine("      --base-url <url>     Custom API base URL for compatible providers");
    AnsiConsole.MarkupLine("  -h, --help               Show this help");
    AnsiConsole.MarkupLine("  -v, --version            Show version\n");
    AnsiConsole.MarkupLine("[bold]ENVIRONMENT VARIABLES:[/]");
    AnsiConsole.MarkupLine("  ANTHROPIC_API_KEY        API key for Anthropic (Claude)");
    AnsiConsole.MarkupLine("  OPENAI_API_KEY           API key for OpenAI");
    AnsiConsole.MarkupLine("  XAI_API_KEY              API key for xAI (Grok)");
    AnsiConsole.MarkupLine("  GEMINI_API_KEY           API key for Google (Gemini)");
    AnsiConsole.MarkupLine("  AGENT_PROVIDER           Default provider");
    AnsiConsole.MarkupLine("  AGENT_MODEL              Default model");
    AnsiConsole.MarkupLine("  AGENT_API_KEY            Generic API key (any provider)\n");
    AnsiConsole.MarkupLine("[bold]REPL COMMANDS:[/]");
    AnsiConsole.MarkupLine("  /help       Show commands");
    AnsiConsole.MarkupLine("  /exit       Exit the agent");
    AnsiConsole.MarkupLine("  /clear      Clear conversation");
    AnsiConsole.MarkupLine("  /save       Save session");
    AnsiConsole.MarkupLine("  /load <id>  Load session");
    AnsiConsole.MarkupLine("  /sessions   List sessions");
    AnsiConsole.MarkupLine("  /status     Agent status");
    AnsiConsole.MarkupLine("  /memory     View memory");
}
