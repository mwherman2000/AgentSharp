using AgentSharpLib;
using AgentSharpLib.Context;
using AgentSharpLib.Memory;
using AgentSharpLib.Telemetry;
using AgentSharpApp.Cli;
using AgentSharpApp.Ui;
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
    private static async Task Main(string[] args)
    {
        // Handle --help and --version
        var commandLine = CommandLineParser.Parse(args);
        if (commandLine.ShowHelp)
        {
            PrintUsage();
            return;
        }

        if (commandLine.ShowVersion)
        {
            AnsiConsole.MarkupLine("[bold]AgentSharp[/] v0.1.0");
            return;
        }

        try
        {
            var __sw = System.Diagnostics.Stopwatch.StartNew();
            void __Mark(string label) { Console.Error.WriteLine($"[TIMING] {label}: {__sw.ElapsedMilliseconds}ms"); }

            // --- Telemetry (disabled unless AGENT_ENABLE_OTEL is set; can also be
            // switched on/redirected to Jaeger mid-session via the /jaeger command) ---
            AgentTelemetry.Initialize();

            // --- Configuration ---
            var options = commandLine.Options;
            __Mark("config loaded");

            // Set the process CWD before anything else reads it -- every relative path
            // (tool file I/O, ProjectContext scanning, MemoryManager, session files) falls
            // back to Directory.GetCurrentDirectory() on its own, so this one call is
            // enough to redirect all of them; nothing downstream needs to know --dir exists.
            if (options.WorkingDirectory is not null)
                Directory.SetCurrentDirectory(options.WorkingDirectory);

            // --- Agent (LLM client, tools, approval, memory, sub-agents, project scan) ---
            // Output and approval are the only terminal-specific pieces; everything
            // else is the library's default wiring.
            AgentSession session = null!;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Scanning project...", async ctx =>
                {
                    session = await new AgentBuilder()
                        .WithOptions(options)
                        .WithOutput(new SpectreAgentOutput())
                        .WithApprovalPrompt(new ConsoleApprovalPrompt())
                        .BuildAsync();
                });
            __Mark("agent built");

            // --- Check for one-shot mode (prompt passed as argument) ---
            if (commandLine.Prompt is { } promptArg)
            {
                // One-shot mode: run a single turn and exit
                __Mark("about to call RunTurnAsync");
                await session.SendAsync(promptArg);
                __Mark("RunTurnAsync done");
                return;
            }

            // --- Interactive REPL ---
            var repl = new ReplHost(session, new SessionManager());
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
        finally
        {
            // Flushes whichever provider ended up active (console from startup, or
            // OTLP/Jaeger if /jaeger was used) before the process exits.
            AgentTelemetry.Shutdown();
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
            AnsiConsole.MarkupLine("      --max-iterations <n> Max LLM/tool round-trips per turn (default: 100)");
            AnsiConsole.MarkupLine("      --dir <path>         Project directory to run in (default: current directory)");
            AnsiConsole.MarkupLine($"      --Superprompt <name> Base persona/prompt: {SystemPromptBuilder.AvailableSuperPrompts[0]} (default), {string.Join(", ", SystemPromptBuilder.AvailableSuperPrompts.Skip(1))}");
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
            AnsiConsole.MarkupLine("  AGENT_MAX_TOKENS         Max output tokens per request (default: 128000)");
            AnsiConsole.MarkupLine("  AGENT_MAX_ITERATIONS     Max LLM/tool round-trips per turn (default: 100)");
            AnsiConsole.MarkupLine("  AGENT_BASE_URL           Custom API base URL");
            AnsiConsole.MarkupLine("  AGENT_ENABLE_OTEL        Emit OpenTelemetry traces via the console exporter (default: off)\n");
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
            AnsiConsole.MarkupLine($"  /jaeger     Switch OTel export to Jaeger (OTLP @ {AgentTelemetry.DefaultJaegerEndpoint})");
        }
    }
}