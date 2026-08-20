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

internal class Program
{
    public static bool HistoryTrace;
    public static bool ToolsTrace;
    public static bool RequestTrace;

    /// <summary>When true, use AgentLoop.RunTurnNonStreamingAsync (SendAsync) instead
    /// of the default RunTurnAsync (StreamAsync). Toggled via the /sync REPL command.</summary>
    public static bool SyncMode;

    private static async Task Main(string[] args)
    {
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
            var __sw = System.Diagnostics.Stopwatch.StartNew();
            void __Mark(string label) { Console.Error.WriteLine($"[TIMING] {label}: {__sw.ElapsedMilliseconds}ms"); }

            // --- Configuration ---
            var config = Configuration.Load(args);
            __Mark("config loaded");

            // Set the process CWD before anything else reads it -- every relative path
            // (tool file I/O, ProjectContext scanning, MemoryManager, session files) falls
            // back to Directory.GetCurrentDirectory() on its own, so this one call is
            // enough to redirect all of them; nothing downstream needs to know --dir exists.
            if (config.WorkingDirectory is not null)
                Directory.SetCurrentDirectory(config.WorkingDirectory);

            // --- LLM Client ---
            var llm = config.CreateLlmClient();
            __Mark("llm client created");

            // --- Tool Registry (auto-discover all ITool implementations) ---
            var tools = new ToolRegistry();
            tools.DiscoverTools();

            // --- Safety ---
            var approval = new ApprovalGate();

            // --- Memory & Sessions ---
            var sessions = new SessionManager();
            var memory = new MemoryManager();

            // --- Multi-Agent Orchestrator ---
            // Create orchestrator and register the sub_agent and remember tools
            // (must be done after tool discovery since both require constructor args)
            var maxTokens = config.MaxTokens ?? AgentSharp.Agent.AgentLoop.DefaultMaxTokens;
            var promptBuilder = new SystemPromptBuilder(new ProjectContext(), memory);
            var orchestrator = new AgentOrchestrator(llm, tools, approval, promptBuilder.Build(), maxTokens);
            tools.Register(new SubAgentTool(orchestrator));
            tools.Register(new MemoryTool(memory));

            // --- Project Context ---
            var project = new ProjectContext();
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Scanning project...", async ctx =>
                {
                    await project.RefreshAsync();
                });
            __Mark("project scan done");

            // --- Check for one-shot mode (prompt passed as argument) ---
            var promptArg = GetPromptArgument(args);
            if (promptArg is not null)
            {
                // One-shot mode: run a single turn and exit
                var oneShotPromptBuilder = new SystemPromptBuilder(project, memory);
                var agentLoop = new AgentSharp.Agent.AgentLoop(llm, tools, approval, oneShotPromptBuilder.Build(), maxTokens: maxTokens);
                __Mark("about to call RunTurnAsync");
                if (SyncMode)
                    await agentLoop.RunTurnNonStreamingAsync(promptArg);
                else
                    await agentLoop.RunTurnStreamingAsync(promptArg);
                __Mark("RunTurnAsync done");
                return;
            }

            // --- Interactive REPL ---
            var repl = new ReplHost(llm, tools, approval, project, sessions, memory, maxTokens);
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
                or "--api-key" or "-k" or "--base-url" or "--prompt" or "--timeout" or "--max-tokens" or "--dir";
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
            AnsiConsole.MarkupLine("  -p, --provider <name>    LLM provider: anthropic, openai, grok, gemini, ollama");
            AnsiConsole.MarkupLine("  -m, --model <name>       Model identifier (e.g., claude-sonnet-4-20250514, gpt-4o)");
            AnsiConsole.MarkupLine("  -k, --api-key <key>      API key (or set via environment variable)");
            AnsiConsole.MarkupLine("      --base-url <url>     Custom API base URL for compatible providers");
            AnsiConsole.MarkupLine("      --timeout <minutes>  Request timeout, e.g. for slow local Ollama models (default: 60)");
            AnsiConsole.MarkupLine("      --max-tokens <n>     Max output tokens per request (default: 128000; lower this for small-context local models)");
            AnsiConsole.MarkupLine("      --dir <path>         Project directory to run in (default: current directory)");
            AnsiConsole.MarkupLine("  -h, --help               Show this help");
            AnsiConsole.MarkupLine("  -v, --version            Show version\n");
            AnsiConsole.MarkupLine("[bold]ENVIRONMENT VARIABLES:[/]");
            AnsiConsole.MarkupLine("  ANTHROPIC_API_KEY        API key for Anthropic (Claude)");
            AnsiConsole.MarkupLine("  OPENAI_API_KEY           API key for OpenAI");
            AnsiConsole.MarkupLine("  XAI_API_KEY              API key for xAI (Grok)");
            AnsiConsole.MarkupLine("  GEMINI_API_KEY           API key for Google (Gemini)");
            AnsiConsole.MarkupLine("                           (ollama needs no API key; run 'ollama serve' locally)");
            AnsiConsole.MarkupLine("  AGENT_PROVIDER           Default provider");
            AnsiConsole.MarkupLine("  AGENT_MODEL              Default model");
            AnsiConsole.MarkupLine("  AGENT_API_KEY            Generic API key (any provider)");
            AnsiConsole.MarkupLine("  AGENT_TIMEOUT_MINUTES    Request timeout in minutes (default: 60, Ollama only)");
            AnsiConsole.MarkupLine("  AGENT_MAX_TOKENS         Max output tokens per request (default: 128000)\n");
            AnsiConsole.MarkupLine("[bold]REPL COMMANDS:[/]");
            AnsiConsole.MarkupLine("  /help       Show commands");
            AnsiConsole.MarkupLine("  /exit       Exit the agent");
            AnsiConsole.MarkupLine("  /clear      Clear conversation");
            AnsiConsole.MarkupLine("  /save       Save session");
            AnsiConsole.MarkupLine("  /load <id>  Load session");
            AnsiConsole.MarkupLine("  /sessions   List sessions");
            AnsiConsole.MarkupLine("  /status     Agent status");
            AnsiConsole.MarkupLine("  /memory     View memory");
            AnsiConsole.MarkupLine("  /request    Toggle request trace");
            AnsiConsole.MarkupLine("  /history    Toggle history trace");
            AnsiConsole.MarkupLine("  /tools      Toggle tools trace");
            AnsiConsole.MarkupLine("  /sync       Toggle SendAsync (non-streaming) vs StreamAsync (default)");
        }
    }
}