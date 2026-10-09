using AgentSharpLib;
using AgentSharpLib.Agent;
using AgentSharpLib.Context;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;
using AgentSharpLib.Telemetry;
using AgentSharpLib.Transcripts;
using Spectre.Console;

namespace AgentSharpApp.Ui;

/// <summary>
/// The main REPL (Read-Eval-Print Loop) host.
/// Manages the interactive session, handles slash commands,
/// and delegates user messages to the agent loop.
/// </summary>
public class ReplHost
{
    private readonly AgentSession _session;
    private readonly SessionManager _sessions;
    private int _turnCount;
    private readonly List<string> _inputHistory = new();
    private CancellationTokenSource? _turnCts;

    public ReplHost(AgentSession session, SessionManager sessions)
    {
        _session = session;
        _sessions = sessions;
        WireEvents();
    }

    /// <summary>
    /// Start the interactive REPL loop.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        PrintWelcome();
        PrintSessions();

        Console.CancelKeyPress += OnCancelKeyPress;

        while (!ct.IsCancellationRequested)
        {
            AnsiConsole.WriteLine();
            var input = ReadMultiLineInput(_inputHistory);

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Record in history (skip consecutive duplicates)
            if (_inputHistory.Count == 0 || _inputHistory[^1] != input)
                _inputHistory.Add(input);

            // Parse and dispatch slash commands. Unlike the regular-message path
            // below, this had no exception handling at all -- an exception from
            // parsing (a bare "/" used to throw IndexOutOfRangeException, now fixed
            // at the source, but this stays as defense in depth) or from any command
            // handler (e.g. a corrupt /load session file) would propagate straight
            // out of RunAsync and take down the entire interactive session over a
            // single command, not just fail that one command.
            try
            {
                var command = CommandParser.Parse(input);
                if (command.Type != CommandType.None)
                {
                    var shouldContinue = await HandleCommandAsync(command, ct);
                    if (!shouldContinue)
                        break;
                    continue;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
                continue;
            }

            // Regular message -- send to agent loop
            _turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var historyCountBeforeTurn = _session.History.Count;
            try
            {
                _turnCount++;
                AnsiConsole.Write(new Rule($"[dim]Turn {_turnCount}[/]").RuleStyle("dim"));
                AnsiConsole.WriteLine();

                await _session.SendAsync(input, _turnCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Roll back so the interrupted user message and any partial
                // assistant/tool-result messages don't linger in history.
                _session.History.TruncateTo(historyCountBeforeTurn);
                AnsiConsole.MarkupLine(_turnCts.IsCancellationRequested && !ct.IsCancellationRequested
                    ? "\n[yellow]Interrupted (Ctrl+C). Returning to prompt.[/]"
                    : "\n[yellow]Cancelled. OperationCanceledException[/]");
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
            finally
            {
                var completedCts = _turnCts;
                _turnCts = null;
                completedCts.Dispose();
            }
        }

        Console.CancelKeyPress -= OnCancelKeyPress;
        AnsiConsole.MarkupLine("[dim]Goodbye![/]");
    }

    /// <summary>
    /// Intercepts Ctrl+C while a prompt is being processed: cancels the in-flight
    /// turn and returns control to the input prompt instead of terminating the
    /// process. Outside of turn processing, Ctrl+C falls through to the default
    /// behavior (process termination).
    /// </summary>
    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        var cts = _turnCts;
        if (cts is null)
            return;

        e.Cancel = true;
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Turn finished and disposed its token source between the null-check
            // above and Cancel() -- nothing left to interrupt.
        }
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

            case CommandType.History:
                AgentFlags.HistoryTrace = !AgentFlags.HistoryTrace;
                Console.WriteLine($"HistoryTrace: {AgentFlags.HistoryTrace.ToString()}");
                break;

            case CommandType.Tools:
                AgentFlags.ToolsTrace = !AgentFlags.ToolsTrace;
                Console.WriteLine($"ToolsTrace: {AgentFlags.ToolsTrace.ToString()}");
                break;

            case CommandType.Request:
                AgentFlags.RequestTrace = !AgentFlags.RequestTrace;
                Console.WriteLine($"RequestTrace: {AgentFlags.RequestTrace.ToString()}");
                break;

            case CommandType.Sync:
                AgentFlags.SyncMode = !AgentFlags.SyncMode;
                AnsiConsole.MarkupLine(AgentFlags.SyncMode
                    ? "[bold]SyncMode:[/] on (using SendAsync, non-streaming)"
                    : "[bold]SyncMode:[/] off (using StreamAsync, default)");
                break;

            case CommandType.Jaeger:
                var jaegerEndpoint = string.IsNullOrWhiteSpace(command.Argument)
                    ? AgentTelemetry.DefaultJaegerEndpoint
                    : command.Argument;
                if (!AgentTelemetry.IsValidEndpoint(jaegerEndpoint))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Not a valid endpoint URL: {Markup.Escape(jaegerEndpoint)} [dim](expected e.g. {AgentTelemetry.DefaultJaegerEndpoint})[/]");
                    break;
                }
                AgentTelemetry.SwitchToJaeger(jaegerEndpoint);
                AnsiConsole.MarkupLine($"[green]OTel export switched to Jaeger[/] (OTLP @ {Markup.Escape(jaegerEndpoint)}).");
                AnsiConsole.MarkupLine($"[dim]View traces at {AgentTelemetry.DefaultJaegerUiUrl} (assumes Jaeger is running locally).[/]");
                break;

            case CommandType.Clear:
                try
                {
                    _session.Reset(string.IsNullOrWhiteSpace(command.Argument) ? null : command.Argument);
                }
                catch (ArgumentException ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                    break;
                }
                _turnCount = 0;
                AnsiConsole.Clear();
                PrintWelcome();
                AnsiConsole.MarkupLine("[green]Conversation cleared.[/]");
                if (!string.IsNullOrWhiteSpace(command.Argument))
                    AnsiConsole.MarkupLine($"[green]Superprompt switched to:[/] {Markup.Escape(_session.SuperPrompt!)}");
                break;

            case CommandType.Save:
                var sessionId = await _sessions.SaveAsync(_session.History, command.Argument);
                if (sessionId is not null)
                {
                    AnsiConsole.MarkupLine($"[green]Session saved:[/] {sessionId}");
                    var savedTranscriptPath = WriteTranscript($"{sessionId}.docx");
                    if (savedTranscriptPath is not null)
                        AnsiConsole.MarkupLine($"[green]Transcript written:[/] {savedTranscriptPath}");
                }
                else
                    AnsiConsole.MarkupLine($"[red]Error:[/] Could not save session '{Markup.Escape(command.Argument ?? "")}'.");
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
                _session.Restore(history);
                AnsiConsole.MarkupLine($"[green]Session loaded:[/] {command.Argument} ({history.Count} messages)");
                break;

            case CommandType.Sessions:
                PrintSessions();
                break;

            case CommandType.Status:
                AnsiConsole.MarkupLine($"[bold]Provider:[/] {_session.Llm.ProviderName}");
                AnsiConsole.MarkupLine($"[bold]Model:[/] {_session.Llm.ModelId}");
                AnsiConsole.MarkupLine($"[bold]Superprompt:[/] {_session.SuperPrompt ?? "andy (default)"}");
                AnsiConsole.MarkupLine($"[bold]Sync mode:[/] {(AgentFlags.SyncMode ? "on (SendAsync, non-streaming)" : "off (StreamAsync, default)")}");
                AnsiConsole.MarkupLine($"[bold]Timeout (streaming):[/] {FormatTimeout(_session.Llm.StreamingTimeout)}");
                AnsiConsole.MarkupLine($"[bold]Timeout (non-streaming):[/] {FormatTimeout(_session.Llm.NonStreamingTimeout)}");
                AnsiConsole.MarkupLine($"[bold]Max tokens:[/] {_session.MaxTokens}");
                AnsiConsole.MarkupLine($"[bold]Max iterations:[/] {_session.MaxIterations}");
                var toolNames = string.Join(", ", _session.Tools.All.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal));
                AnsiConsole.MarkupLine($"[bold]Tools:[/] {_session.Tools.All.Count} [dim]({Markup.Escape(toolNames)})[/]");
                AnsiConsole.MarkupLine($"[bold]Turns:[/] {_turnCount}");
                AnsiConsole.MarkupLine($"[bold]Messages:[/] {_session.History.Count}");
                AnsiConsole.MarkupLine($"[bold]Tokens:[/] {_session.Loop.TotalInputTokens} in / {_session.Loop.TotalOutputTokens} out");
                AnsiConsole.MarkupLine($"[bold]Cache:[/] {_session.Loop.TotalCacheCreationTokens} written / {_session.Loop.TotalCacheReadTokens} read{FormatCacheHitRate()}");
                AnsiConsole.MarkupLine($"[bold]Session:[/] {SessionUsage.FormatElapsed(_session.Usage.Elapsed)} elapsed, {_session.Usage.TotalTokens:N0} tokens [dim](all conversations and sub-agents)[/]");
                AnsiConsole.MarkupLine($"[bold]Directory:[/] {_session.Project.WorkingDirectory}");
                AnsiConsole.MarkupLine($"[bold]Git branch:[/] {_session.Project.GitBranch ?? "N/A"}");
                break;

            case CommandType.Model:
                AnsiConsole.MarkupLine($"[bold]Current model:[/] {_session.Llm.ProviderName} / {_session.Llm.ModelId}");
                AnsiConsole.MarkupLine("[dim]To change the model, restart with --model <name>[/]");
                break;

            case CommandType.Memory:
                if (command.Argument == "clear")
                {
                    _session.Memory?.Clear();
                    AnsiConsole.MarkupLine("[green]Memory cleared.[/]");
                }
                else
                {
                    var mem = _session.Memory?.Read();
                    if (mem is null)
                        AnsiConsole.MarkupLine("[dim]No memory file found.[/]");
                    else
                        AnsiConsole.Write(new Panel(Markup.Escape(mem)).Header("MEMORY.md"));
                }
                break;

            case CommandType.Transcript:
                if (string.IsNullOrWhiteSpace(command.Argument))
                {
                    AnsiConsole.MarkupLine("[yellow]Usage: /transcribe <name>[/]");
                    break;
                }
                var transcriptPath = WriteTranscript(command.Argument);
                if (transcriptPath is not null)
                    AnsiConsole.MarkupLine($"[green]Transcript written:[/] {transcriptPath}");
                break;

            case CommandType.Unknown:
                AnsiConsole.MarkupLine($"[yellow]Unknown command: /{command.Argument}. Type /help for available commands.[/]");
                break;
        }

        return true;
    }

    private void WireEvents()
    {
        _session.OnToolStart += (name, summary) =>
        {
            AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(name)}[/]").RuleStyle("dim"));
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(summary)}[/]");
        };

        _session.OnToolEnd += (name, result) =>
        {
            var output = SanitizeForTerminal(result.Output);
            if (result.IsError)
            {
                AnsiConsole.MarkupLine($"[red]  Error: {Markup.Escape(TruncateForDisplay(output))}[/]");
            }
            else
            {
                var preview = TruncateForDisplay(output, 200);
                var firstLine = preview.Split('\n')[0].TrimEnd('\r');
                AnsiConsole.MarkupLine($"[green]  Done[/] [dim]({Markup.Escape(firstLine)})[/]");
            }
        };
    }

    /// <summary>
    /// Writes a Q&amp;A transcript of the conversation (see
    /// <see cref="AgentSession.WriteTranscript"/>) and returns its path, or reports
    /// the problem and returns null.
    /// </summary>
    private string? WriteTranscript(string name)
    {
        try
        {
            return _session.WriteTranscript(name);
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AnsiConsole.MarkupLine($"[red]Error writing transcript:[/] {Markup.Escape(ex.Message)}");
        }
        return null;
    }

    private string FormatCacheHitRate()
    {
        var effectiveInput = _session.Loop.TotalInputTokens + _session.Loop.TotalCacheCreationTokens + _session.Loop.TotalCacheReadTokens;
        if (effectiveInput == 0) return "";
        var hitRate = 100.0 * _session.Loop.TotalCacheReadTokens / effectiveInput;
        return $" ({hitRate:F0}% hit rate)";
    }

    /// <summary>
    /// Renders a timeout as a compact human-readable duration (e.g. "1m 40s", "1h",
    /// "10h") instead of TimeSpan's verbose default ToString().
    /// </summary>
    private static string FormatTimeout(TimeSpan timeout)
    {
        if (timeout.TotalHours >= 1)
            return timeout.Minutes == 0 ? $"{timeout.TotalHours:F0}h" : $"{(int)timeout.TotalHours}h {timeout.Minutes}m";
        if (timeout.TotalMinutes >= 1)
            return timeout.Seconds == 0 ? $"{timeout.Minutes}m" : $"{timeout.Minutes}m {timeout.Seconds}s";
        return $"{timeout.TotalSeconds:F0}s";
    }

    private static string TruncateForDisplay(string text, int maxLength = 500)
    {
        if (text.Length <= maxLength) return text;
        // Avoid splitting a surrogate pair (e.g. an emoji) in half at the cut point.
        if (char.IsHighSurrogate(text[maxLength - 1]))
            maxLength--;
        return text[..maxLength] + "...";
    }

    /// <summary>
    /// Strips raw control characters (e.g. ESC-prefixed ANSI/VT sequences) from
    /// untrusted tool output (web_fetch, run_shell, etc.) before it reaches the
    /// terminal. Markup.Escape only neutralizes '['/']' for Spectre's own markup
    /// parser -- it does nothing to stop injected terminal control sequences.
    /// </summary>
    private static string SanitizeForTerminal(string text)
    {
        Span<char> buffer = text.Length <= 1024 ? stackalloc char[text.Length] : new char[text.Length];
        var written = 0;
        foreach (var c in text)
        {
            if (c == '\n' || c == '\r' || c == '\t' || !char.IsControl(c))
                buffer[written++] = c;
        }
        return new string(buffer[..written]);
    }

    private void PrintWelcome()
    {
        AnsiConsole.Write(new FigletText("AgentSharp").Color(Color.Blue));
        AnsiConsole.MarkupLine($"[bold]AI Agent:[/] [green]{Markup.Escape(SystemPromptBuilder.ResolveAgentName(_session.SuperPrompt))}[/] - Built with patterns from Claude Code");
        AnsiConsole.MarkupLine($"[dim]Provider: {_session.Llm.ProviderName} | Model: {_session.Llm.ModelId} | Max tokens: {_session.MaxTokens} | Tools: {_session.Tools.All.Count}[/]");
        if (_session.Project.IsGitRepo)
            AnsiConsole.MarkupLine($"[dim]Git: {_session.Project.GitBranch} | Dir: {_session.Project.WorkingDirectory}[/]");
        var systemPromptFirstLine = _session.SystemPrompt.Split('\n', 2)[0].TrimEnd('\r');
        if (systemPromptFirstLine.Length > 0)
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(systemPromptFirstLine)}[/]");
        AnsiConsole.MarkupLine("[dim]Type /help for commands, or start chatting.[/]");
        AnsiConsole.Write(new Rule().RuleStyle("dim"));
    }

    /// <summary>
    /// Lists saved sessions, same as the /sessions command. Also run once at
    /// startup, right after the welcome banner, so returning users see what's
    /// available to /load without having to ask.
    /// </summary>
    private void PrintSessions()
    {
        var sessions = _sessions.ListSessions();
        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No saved sessions.[/]");
            return;
        }
        var table = new Table()
            .AddColumn("ID")
            .AddColumn("Created")
            .AddColumn("Messages");
        foreach (var s in sessions)
            table.AddRow(s.Id, s.CreatedAt.ToString("yyyy-MM-dd HH:mm"), s.MessageCount.ToString());
        AnsiConsole.Write(table);
    }

    /// <summary>Shared by both /help tables so their columns line up.</summary>
    private const int CommandColumnWidth = 24;

    private static void PrintHelp()
    {
        // Same sections, order, and wording as AgentLucyApp's /help, so the two apps
        // read alike; this one adds /clear's optional persona switch.
        var commands = new Table()
            .Title("[bold]Commands[/]")
            .AddColumn(new TableColumn("Command").Width(CommandColumnWidth))
            .AddColumn("Description")
            .AddRow("/help, /h, /?", "Show this help")
            .AddRow("/clear, /cls [[persona]]", $"Start a new conversation; optionally switch persona ({string.Join(", ", SystemPromptBuilder.AvailableSuperPrompts)})")
            .AddRow("/save [[id]]", "Save this conversation (and a .docx transcript)")
            .AddRow("/load, /resume <id>", "Continue a saved conversation")
            .AddRow("/sessions, /ls", "List saved conversations")
            .AddRow("/status", "Model, tools, token usage, directory")
            .AddRow("/model", "Show the current provider and model")
            .AddRow("/memory, /mem [[clear]]", "Show MEMORY.md, or delete it")
            .AddRow("/transcribe <name>", "Write a Q&A transcript (<name>.md, or <name>.docx)")
            .AddRow("/exit, /quit, /q", "Quit")
            .AddRow("Ctrl+C", "Interrupt the agent mid-reply (at the prompt: quit)");

        var diagnostics = new Table()
            .Title("[bold]Diagnostics[/]")
            .AddColumn(new TableColumn("Command").Width(CommandColumnWidth))
            .AddColumn("Description")
            .AddRow("/sync", "Toggle streaming vs. non-streaming replies")
            .AddRow("/request", "Toggle dumping each request sent to the model")
            .AddRow("/history", "Toggle dumping the conversation history with each request")
            .AddRow("/tools", "Toggle dumping the tool definitions with each request")
            .AddRow("/jaeger [[endpoint]]", $"Send OpenTelemetry traces to Jaeger (default {AgentTelemetry.DefaultJaegerEndpoint})");

        AnsiConsole.Write(commands);
        AnsiConsole.Write(diagnostics);
    }

    /// <summary>
    /// Read user input with multiline support.
    /// Alt+Enter inserts a newline, Enter submits.
    /// Trailing backslash also continues to the next line.
    /// Up/Down arrows recall previous entries from <paramref name="history"/>
    /// (only while still on the first line of input, before any continuation).
    /// </summary>
    private static string ReadMultiLineInput(List<string> history)
    {
        var lines = new List<string>();
        var current = new System.Text.StringBuilder();
        AnsiConsole.Markup("[bold blue]>[/] ");

        // historyIndex == history.Count means "not currently navigating history"
        // (i.e. showing the user's own in-progress draft).
        var historyIndex = history.Count;
        var draft = string.Empty;

        // While blocked in Console.ReadKey, Ctrl+C is handled inline below rather
        // than via Console.CancelKeyPress: on Windows, the OS delivers the Ctrl+C
        // control event on a separate thread that can deadlock against the console
        // lock held by a pending synchronous ReadKey call. Treating it as ordinary
        // input sidesteps that. RunAsync's CancelKeyPress handler takes over once
        // this method returns and the main thread is only awaiting the agent turn.
        Console.TreatControlCAsInput = true;
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    Console.Write("^C");
                    Console.WriteLine();
                    return string.Empty;
                }

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
                else if (key.Key == ConsoleKey.UpArrow)
                {
                    // Only recall history while on the first (only) line so far.
                    if (lines.Count == 0 && history.Count > 0 && historyIndex > 0)
                    {
                        if (historyIndex == history.Count)
                            draft = current.ToString();

                        historyIndex--;
                        ReplaceCurrentLine(current, history[historyIndex]);
                    }
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    if (lines.Count == 0 && historyIndex < history.Count)
                    {
                        historyIndex++;
                        ReplaceCurrentLine(current, historyIndex == history.Count ? draft : history[historyIndex]);
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
        finally
        {
            Console.TreatControlCAsInput = false;
        }
    }

    /// <summary>
    /// Erases the currently displayed input line on the console and replaces
    /// both the buffer and the visible text with <paramref name="newText"/>.
    /// Used for Up/Down arrow history recall.
    /// </summary>
    private static void ReplaceCurrentLine(System.Text.StringBuilder current, string newText)
    {
        // Erase existing characters: backspace, overwrite with space, backspace again.
        if (current.Length > 0)
        {
            Console.Write(new string('\b', current.Length));
            Console.Write(new string(' ', current.Length));
            Console.Write(new string('\b', current.Length));
        }

        current.Clear();
        current.Append(newText);
        Console.Write(newText);
    }
}
