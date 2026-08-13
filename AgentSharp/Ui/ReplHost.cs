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
    private readonly int _maxTokens;
    private int _turnCount;
    private readonly List<string> _inputHistory = new();
    private CancellationTokenSource? _turnCts;

    public ReplHost(
        ILlmClient llm,
        ToolRegistry tools,
        ApprovalGate approval,
        ProjectContext project,
        SessionManager sessions,
        MemoryManager memory,
        int maxTokens = AgentLoop.DefaultMaxTokens)
    {
        _llm = llm;
        _tools = tools;
        _approval = approval;
        _project = project;
        _sessions = sessions;
        _memory = memory;
        _maxTokens = maxTokens;

        var promptBuilder = new SystemPromptBuilder(_project, _memory);
        _agent = new AgentLoop(_llm, _tools, _approval, promptBuilder.Build(), maxTokens: _maxTokens);
        WireEvents(_agent);
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
            _turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var historyCountBeforeTurn = _agent.History.Count;
            try
            {
                _turnCount++;
                AnsiConsole.Write(new Rule($"[dim]Turn {_turnCount}[/]").RuleStyle("dim"));
                AnsiConsole.WriteLine();

                if (Program.SyncMode)
                    await _agent.RunTurnNonStreamingAsync(input, _turnCts.Token);
                else
                    await _agent.RunTurnStreamingAsync(input, _turnCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Roll back so the interrupted user message and any partial
                // assistant/tool-result messages don't linger in history.
                _agent.History.TruncateTo(historyCountBeforeTurn);
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
                Program.HistoryTrace = !Program.HistoryTrace;
                Console.WriteLine($"HistoryTrace: {Program.HistoryTrace.ToString()}");
                break;

            case CommandType.Tools:
                Program.ToolsTrace = !Program.ToolsTrace;
                Console.WriteLine($"ToolsTrace: {Program.ToolsTrace.ToString()}");
                break;

            case CommandType.Request:
                Program.RequestTrace = !Program.RequestTrace;
                Console.WriteLine($"RequestTrace: {Program.RequestTrace.ToString()}");
                break;

            case CommandType.Sync:
                Program.SyncMode = !Program.SyncMode;
                AnsiConsole.MarkupLine(Program.SyncMode
                    ? "[bold]SyncMode:[/] on (using SendAsync, non-streaming)"
                    : "[bold]SyncMode:[/] off (using StreamAsync, default)");
                break;

            case CommandType.Clear:
                _agent = new AgentLoop(_llm, _tools, _approval,
                    new SystemPromptBuilder(_project, _memory).Build(), maxTokens: _maxTokens);
                WireEvents(_agent);
                _turnCount = 0;
                AnsiConsole.Clear();
                PrintWelcome();
                AnsiConsole.MarkupLine("[green]Conversation cleared.[/]");
                break;

            case CommandType.Save:
                var sessionId = await _sessions.SaveAsync(_agent.History, command.Argument);
                if (sessionId is not null)
                    AnsiConsole.MarkupLine($"[green]Session saved:[/] {sessionId}");
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
                _agent = new AgentLoop(_llm, _tools, _approval,
                    new SystemPromptBuilder(_project, _memory).Build(), history, _maxTokens);
                WireEvents(_agent);
                AnsiConsole.MarkupLine($"[green]Session loaded:[/] {command.Argument} ({history.Count} messages)");
                break;

            case CommandType.Sessions:
                PrintSessions();
                break;

            case CommandType.Status:
                AnsiConsole.MarkupLine($"[bold]Provider:[/] {_llm.ProviderName}");
                AnsiConsole.MarkupLine($"[bold]Model:[/] {_llm.ModelId}");
                AnsiConsole.MarkupLine($"[bold]Sync mode:[/] {(Program.SyncMode ? "on (SendAsync, non-streaming)" : "off (StreamAsync, default)")}");
                AnsiConsole.MarkupLine($"[bold]Timeout (streaming):[/] {FormatTimeout(_llm.StreamingTimeout)}");
                AnsiConsole.MarkupLine($"[bold]Timeout (non-streaming):[/] {FormatTimeout(_llm.NonStreamingTimeout)}");
                AnsiConsole.MarkupLine($"[bold]Max tokens:[/] {_maxTokens}");
                AnsiConsole.MarkupLine($"[bold]Tools:[/] {_tools.All.Count}");
                AnsiConsole.MarkupLine($"[bold]Turns:[/] {_turnCount}");
                AnsiConsole.MarkupLine($"[bold]Messages:[/] {_agent.History.Count}");
                AnsiConsole.MarkupLine($"[bold]Tokens:[/] {_agent.TotalInputTokens} in / {_agent.TotalOutputTokens} out");
                AnsiConsole.MarkupLine($"[bold]Cache:[/] {_agent.TotalCacheCreationTokens} written / {_agent.TotalCacheReadTokens} read{FormatCacheHitRate()}");
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

            case CommandType.Transcript:
                if (string.IsNullOrWhiteSpace(command.Argument))
                {
                    AnsiConsole.MarkupLine("[yellow]Usage: /transcript <name>[/]");
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

    private void WireEvents(AgentLoop agent)
    {
        agent.OnToolStart += (name, summary) =>
        {
            AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(name)}[/]").RuleStyle("dim"));
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(summary)}[/]");
        };

        agent.OnToolEnd += (name, result) =>
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
    /// Writes a clean Q&amp;A transcript of the conversation to <paramref name="name"/>.md
    /// -- just the user's typed prompts and the assistant's full text replies, with none
    /// of the tool-call/tool-result/trace noise that fills the live console output.
    /// A single user turn can span several history entries (assistant text, tool calls,
    /// tool results, more assistant text), so consecutive assistant text messages are
    /// merged into one answer until the next real user prompt starts a new pair.
    /// </summary>
    private string? WriteTranscript(string name)
    {
        // Path.GetFileName strips any directory portion, so a name like "/trump14020"
        // or "../elsewhere" can't Path.Combine its way outside the working directory
        // (a leading '/' makes the second Path.Combine argument rooted, which silently
        // discards the working directory and resolves to the drive root instead).
        var safeName = Path.GetFileName(name);
        if (string.IsNullOrEmpty(safeName))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] '{Markup.Escape(name)}' is not a valid file name.");
            return null;
        }

        var fileName = safeName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? safeName : $"{safeName}.md";
        var path = Path.Combine(_project.WorkingDirectory, fileName);

        var qaPairs = new List<(string Question, string Answer)>();
        string? currentQuestion = null;
        var answer = new System.Text.StringBuilder();

        foreach (var message in _agent.History.Messages)
        {
            if (message.Role == MessageRole.User)
            {
                // Tool-result messages are also role "user" but carry no TextBlock --
                // only messages with actual typed text are real prompts.
                var text = string.Join("\n\n", message.Content.OfType<TextBlock>().Select(b => b.Text));
                if (text.Length == 0) continue;

                if (currentQuestion is not null)
                    qaPairs.Add((currentQuestion, answer.ToString().Trim()));

                currentQuestion = CapitalizeFirstLetter(text);
                answer.Clear();
            }
            else if (message.Role == MessageRole.Assistant)
            {
                var text = message.GetText();
                if (text.Length == 0) continue;

                if (answer.Length > 0) answer.Append("\n\n");
                answer.Append(text);
            }
        }
        if (currentQuestion is not null)
            qaPairs.Add((currentQuestion, answer.ToString().Trim()));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {name}");
        sb.AppendLine();
        var systemPromptIntro = GetFirstParagraph(_agent.SystemPrompt);
        if (systemPromptIntro.Length > 0)
        {
            foreach (var line in systemPromptIntro.Split('\n'))
                sb.AppendLine($"> {line}");
            sb.AppendLine();
        }
        sb.AppendLine($"_Transcript generated {DateTime.Now:yyyy-MM-dd HH:mm}_");
        sb.AppendLine();
        for (int i = 0; i < qaPairs.Count; i++)
        {
            sb.AppendLine($"## Q{i + 1}");
            sb.AppendLine();
            sb.AppendLine(qaPairs[i].Question);
            sb.AppendLine();
            sb.AppendLine($"## A{i + 1}");
            sb.AppendLine();
            sb.AppendLine(qaPairs[i].Answer.Length > 0 ? qaPairs[i].Answer : "_(no response)_");
            sb.AppendLine();
        }

        try
        {
            File.WriteAllText(path, sb.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AnsiConsole.MarkupLine($"[red]Error writing transcript:[/] {Markup.Escape(ex.Message)}");
            return null;
        }
        return path;
    }

    /// <summary>
    /// Uppercases the first letter of a prompt for the transcript file -- users
    /// often type prompts lowercase in the REPL, which reads oddly as the "Q" in
    /// a Q&amp;A document. Leaves the text untouched if it has no lowercase first letter
    /// (already capitalized, or starts with punctuation/a digit).
    /// </summary>
    private static string CapitalizeFirstLetter(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i])) continue;
            if (char.IsLower(text[i]))
                return text[..i] + char.ToUpper(text[i]) + text[(i + 1)..];
            return text;
        }
        return text;
    }

    /// <summary>
    /// Extracts just the first line of the system prompt, so the transcript records
    /// which persona/instructions produced the replies without dumping the entire --
    /// often very long -- prompt.
    /// </summary>
    private static string GetFirstParagraph(string text)
    {
        var trimmed = text.TrimStart();
        var newlineIndex = trimmed.IndexOf('\n');
        var firstLine = newlineIndex >= 0 ? trimmed[..newlineIndex] : trimmed;
        return firstLine.Trim();
    }

    private string FormatCacheHitRate()
    {
        var effectiveInput = _agent.TotalInputTokens + _agent.TotalCacheCreationTokens + _agent.TotalCacheReadTokens;
        if (effectiveInput == 0) return "";
        var hitRate = 100.0 * _agent.TotalCacheReadTokens / effectiveInput;
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
        AnsiConsole.MarkupLine("[bold]AI Agent[/] - Built with patterns from Claude Code");
        AnsiConsole.MarkupLine($"[dim]Provider: {_llm.ProviderName} | Model: {_llm.ModelId} | Max tokens: {_maxTokens} | Tools: {_tools.All.Count}[/]");
        if (_project.IsGitRepo)
            AnsiConsole.MarkupLine($"[dim]Git: {_project.GitBranch} | Dir: {_project.WorkingDirectory}[/]");
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
            .AddRow("/memory clear", "Clear persistent memory")
            .AddRow("/transcript <name>", "Write a Q&A transcript of this conversation to <name>.md")
            .AddRow("/request", "Toggle request trace")
            .AddRow("/history", "Toggle history trace")
            .AddRow("/tools", "Toggle tools trace")
            .AddRow("/sync", "Toggle SendAsync (non-streaming) vs StreamAsync (default)");

        AnsiConsole.Write(table);
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
