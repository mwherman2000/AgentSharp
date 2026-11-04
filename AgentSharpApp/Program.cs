using AgentSharpLib;
using AgentSharpLib.Context;
using AgentSharpLib.Memory;
using AgentSharpLib.Telemetry;
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

            // --- Personas: built-in superprompts, plus any files in ~/.agentsharp/superprompts and
            // ./.agentsharp/superprompts, rescanned whenever one is added, changed or deleted ---
            using var personaWatcher = PersonaRegistry.WatchAndReport(Directory.GetCurrentDirectory(),
                what => AnsiConsole.MarkupLine("[grey]" + Markup.Escape(what) + "[/]"),
                warning => AnsiConsole.MarkupLine("[yellow]" + Markup.Escape(warning) + "[/]"));

            // --- Agent (LLM client, tools, approval, memory, sub-agents, project scan) ---
            // Output and approval are the only terminal-specific pieces; everything
            // else is the library's default wiring.
            AgentSession session = null!;
            // One session store for the REPL and for the automatic saves (before/after /compact, before a handoff).
            var sessions = new SessionManager();
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Scanning project...", async ctx =>
                {
                    session = await new AgentBuilder()
                        .WithOptions(options)
                        .WithOutput(new SpectreAgentOutput())
                        .WithApprovalPrompt(new ConsoleApprovalPrompt())
                        .WithSessionManager(sessions)
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
            var repl = new ReplHost(session, sessions);
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
            foreach (var (flags, description) in CommandLineParser.OptionHelp(CommandLineParser.SuperPromptHelp(SystemPromptBuilder.AvailableSuperPrompts[0])))
                AnsiConsole.MarkupLine($"  {Markup.Escape(flags.PadRight(26))}{Markup.Escape(description)}");
            AnsiConsole.MarkupLine("\n[bold]ENVIRONMENT VARIABLES:[/]");
            foreach (var (name, description) in CommandLineParser.EnvironmentHelp)
                AnsiConsole.MarkupLine($"  {Markup.Escape(name.PadRight(26))}{Markup.Escape(description)}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]REPL COMMANDS:[/]");
            foreach (var (command, description) in CommandLineParser.ReplCommandHelp)
                AnsiConsole.MarkupLine($"  {Markup.Escape(command.PadRight(30))}{Markup.Escape(description)}");
        }
    }
}