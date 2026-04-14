using AgentSharp.Agent;
using AgentSharp.Context;
using AgentSharp.Llm;
using AgentSharp.Memory;
using AgentSharp.Safety;
using AgentSharp.Tools;
using Spectre.Console;

namespace AgentSharp.Ui;

/// <summary>
/// The main REPL (Read-Eval-Print Loop) host.
/// Manages the interactive session, handles slash commands,
/// and delegates user messages to the agent loop.
/// </summary>
public class ReplHost
{
    private readonly ILlmClient _llm;
    private readonly ToolRegistry _tools;
    private readonly ApprovalGate _approval;
    private readonly ProjectContext _project;
    private readonly SessionManager _sessions;
    private readonly MemoryManager _memory;
    private AgentLoop _agent;
    private int _turnCount;

    public ReplHost(
        ILlmClient llm,
        ToolRegistry tools,
        ApprovalGate approval,
        ProjectContext project,
        SessionManager sessions,
        MemoryManager memory)
    {
        _llm = llm;
        _tools = tools;
        _approval = approval;
        _project = project;
        _sessions = sessions;
        _memory = memory;

        var promptBuilder = new SystemPromptBuilder(_project);
        _agent = new AgentLoop(_llm, _tools, _approval, promptBuilder.Build());
        WireEvents(_agent);
    }

    /// <summary>
    /// Start the interactive REPL loop.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        PrintWelcome();

        while (!ct.IsCancellationRequested)
        {
            AnsiConsole.WriteLine();
            var input = ReadMultiLineInput();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Parse slash commands
            var command = CommandParser.Parse(input);
            if (command.Type != CommandType.None)
            {
                var shouldContinue = await HandleCommandAsync(command, ct);
                if (!shouldContinue)
                    break;
                continue;
            }

            // Regular message -- send to agent loop
            try
            {
                _turnCount++;
                AnsiConsole.Write(new Rule($"[dim]Turn {_turnCount}[/]").RuleStyle("dim"));
                AnsiConsole.WriteLine();

                await _agent.RunTurnAsync(input, ct);
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("\n[yellow]Cancelled.[/]");
            }
            catch (HttpRequestException ex)
            {
                AnsiConsole.MarkupLine($"\n[red]API Error:[/] {Markup.Escape(ex.Message)}");
                AnsiConsole.MarkupLine("[dim]Check your API key and network connection.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
            }
        }

        AnsiConsole.MarkupLine("[dim]Goodbye![/]");
    }

    private async Task<bool> HandleCommandAsync(ParsedCommand command, CancellationToken ct)
    {
        switch (command.Type)
        {
            case CommandType.Help:
                PrintHelp();
                break;

            case CommandType.Exit:
                return false;

            case CommandType.Clear:
                _agent = new AgentLoop(_llm, _tools, _approval,
                    new SystemPromptBuilder(_project).Build());
                WireEvents(_agent);
                _turnCount = 0;
                AnsiConsole.Clear();
                PrintWelcome();
                AnsiConsole.MarkupLine("[green]Conversation cleared.[/]");
                break;

            case CommandType.Save:
                var sessionId = await _sessions.SaveAsync(_agent.History, command.Argument);
                AnsiConsole.MarkupLine($"[green]Session saved:[/] {sessionId}");
                break;

            case CommandType.Load:
                if (command.Argument is null)
                {
                    AnsiConsole.MarkupLine("[yellow]Usage: /load <session-id>[/]");
                    break;
                }
                var history = await _sessions.LoadAsync(command.Argument);
                if (history is null)
                {
                    AnsiConsole.MarkupLine($"[red]Session not found:[/] {command.Argument}");
                    break;
                }
                _agent = new AgentLoop(_llm, _tools, _approval,
                    new SystemPromptBuilder(_project).Build(), history);
                WireEvents(_agent);
                AnsiConsole.MarkupLine($"[green]Session loaded:[/] {command.Argument} ({history.Count} messages)");
                break;

            case CommandType.Sessions:
                var sessions = _sessions.ListSessions();
                if (sessions.Count == 0)
                {
                    AnsiConsole.MarkupLine("[dim]No saved sessions.[/]");
                    break;
                }
                var table = new Table()
                    .AddColumn("ID")
                    .AddColumn("Created")
                    .AddColumn("Messages");
                foreach (var s in sessions)
                    table.AddRow(s.Id, s.CreatedAt.ToString("yyyy-MM-dd HH:mm"), s.MessageCount.ToString());
                AnsiConsole.Write(table);
                break;

            case CommandType.Status:
                AnsiConsole.MarkupLine($"[bold]Provider:[/] {_llm.ProviderName}");
                AnsiConsole.MarkupLine($"[bold]Model:[/] {_llm.ModelId}");
                AnsiConsole.MarkupLine($"[bold]Tools:[/] {_tools.All.Count}");
                AnsiConsole.MarkupLine($"[bold]Turns:[/] {_turnCount}");
                AnsiConsole.MarkupLine($"[bold]Messages:[/] {_agent.History.Count}");
                AnsiConsole.MarkupLine($"[bold]Directory:[/] {_project.WorkingDirectory}");
                AnsiConsole.MarkupLine($"[bold]Git branch:[/] {_project.GitBranch ?? "N/A"}");
                break;

            case CommandType.Model:
                AnsiConsole.MarkupLine($"[bold]Current model:[/] {_llm.ProviderName} / {_llm.ModelId}");
                AnsiConsole.MarkupLine("[dim]To change the model, restart with --model <name>[/]");
                break;

            case CommandType.Memory:
                if (command.Argument == "clear")
                {
                    File.Delete(Path.Combine(_project.WorkingDirectory, "MEMORY.md"));
                    AnsiConsole.MarkupLine("[green]Memory cleared.[/]");
                }
                else
                {
                    var mem = _memory.Read();
                    if (mem is null)
                        AnsiConsole.MarkupLine("[dim]No memory file found.[/]");
                    else
                        AnsiConsole.Write(new Panel(Markup.Escape(mem)).Header("MEMORY.md"));
                }
                break;

            case CommandType.Unknown:
                AnsiConsole.MarkupLine($"[yellow]Unknown command: /{command.Argument}. Type /help for available commands.[/]");
                break;
        }

        return true;
    }

    private void WireEvents(AgentLoop agent)
    {
        agent.OnToolStart += (name, summary) =>
        {
            AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(name)}[/]").RuleStyle("dim"));
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(summary)}[/]");
        };

        agent.OnToolEnd += (name, result) =>
        {
            if (result.IsError)
            {
                AnsiConsole.MarkupLine($"[red]  Error: {Markup.Escape(TruncateForDisplay(result.Output))}[/]");
            }
            else
            {
                var preview = TruncateForDisplay(result.Output, 200);
                AnsiConsole.MarkupLine($"[green]  Done[/] [dim]({preview.Split('\n')[0]})[/]");
            }
        };
    }

    private static string TruncateForDisplay(string text, int maxLength = 500)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }

    private void PrintWelcome()
    {
        AnsiConsole.Write(new FigletText("AgentSharp").Color(Color.Blue));
        AnsiConsole.MarkupLine("[bold]AI Coding Agent[/] - Built with patterns from Claude Code");
        AnsiConsole.MarkupLine($"[dim]Provider: {_llm.ProviderName} | Model: {_llm.ModelId} | Tools: {_tools.All.Count}[/]");
        if (_project.IsGitRepo)
            AnsiConsole.MarkupLine($"[dim]Git: {_project.GitBranch} | Dir: {_project.WorkingDirectory}[/]");
        AnsiConsole.MarkupLine("[dim]Type /help for commands, or start chatting.[/]");
        AnsiConsole.Write(new Rule().RuleStyle("dim"));
    }

    private static void PrintHelp()
    {
        var table = new Table()
            .Title("[bold]Commands[/]")
            .AddColumn("Command")
            .AddColumn("Description")
            .AddRow("/help", "Show this help message")
            .AddRow("/exit", "Exit the agent")
            .AddRow("/clear", "Clear conversation and start fresh")
            .AddRow("/save [[id]]", "Save current session")
            .AddRow("/load <id>", "Load a saved session")
            .AddRow("/sessions", "List saved sessions")
            .AddRow("/status", "Show current agent status")
            .AddRow("/model", "Show current model info")
            .AddRow("/memory", "Show persistent memory")
            .AddRow("/memory clear", "Clear persistent memory");

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Read user input with multiline support.
    /// Alt+Enter inserts a newline, Enter submits.
    /// Trailing backslash also continues to the next line.
    /// </summary>
    private static string ReadMultiLineInput()
    {
        var lines = new List<string>();
        var current = new System.Text.StringBuilder();
        AnsiConsole.Markup("[bold blue]>[/] ");

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                var isPaste = Console.KeyAvailable; // more keys buffered = paste
                if (key.Modifiers.HasFlag(ConsoleModifiers.Alt) || isPaste)
                {
                    // Alt+Enter or paste: insert newline, continue editing
                    lines.Add(current.ToString());
                    current.Clear();
                    Console.WriteLine();
                    if (!isPaste)
                        AnsiConsole.Markup("[bold blue]..[/] ");
                }
                else
                {
                    // Enter: check for backslash continuation
                    var line = current.ToString();
                    Console.WriteLine();
                    if (line.EndsWith('\\'))
                    {
                        lines.Add(line[..^1]);
                        current.Clear();
                        AnsiConsole.Markup("[bold blue]..[/] ");
                    }
                    else
                    {
                        lines.Add(line);
                        return string.Join('\n', lines);
                    }
                }
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (current.Length > 0)
                {
                    current.Remove(current.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else if (key.KeyChar >= ' ')
            {
                current.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }
    }
}
