using AgentLucyApp;
using AgentSharpLib;
using AgentSharpLib.Context;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;

// ============================================================================
// AgentLucyApp - a minimal chat with Lucy, built only on AgentSharpLib.
//
// Everything agent-related (LLM client, tools, memory, sub-agents, the Lucy
// persona, saved sessions, transcripts) comes from the library; this file only
// supplies the console I/O.
// ============================================================================

var options = ParseArgs(args);

AgentSession lucy;
try
{
    lucy = await new AgentBuilder()
        .WithOptions(options)
        .WithSuperPrompt("lucy")
        .WithOutput(new ConsoleAgentOutput())
        .WithApprovalPrompt(new ConsoleApprovalPrompt())
        .BuildAsync();
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Usage: AgentLucyApp [--provider <name>] [--model <name>] [--api-key <key>] [--max-tokens <n>]");
    return 1;
}

var agentName = SystemPromptBuilder.ResolveAgentName(lucy.SuperPrompt);

// Lucy's saved conversations live apart from the main CLI's (~/.agentsharp/sessions),
// so /load and /sessions only ever show Lucy conversations.
var sessions = new SessionManager(Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentsharp", "lucy"));

Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine($"{agentName}  ({lucy.Llm.ProviderName} / {lucy.Llm.ModelId})");
Console.ResetColor();
Console.WriteLine("Type a message, or /help for commands.");

// Ctrl+C during a reply cancels just that turn; at the prompt it exits as usual.
CancellationTokenSource? turnCts = null;
Console.CancelKeyPress += (_, e) =>
{
    if (turnCts is { } cts)
    {
        e.Cancel = true;
        cts.Cancel();
    }
};

while (true)
{
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.Write($"\n{agentName}> ");
    Console.ResetColor();

    var input = Console.ReadLine();
    if (input is null)
        break; // end of input
    input = input.Trim();
    if (input.Length == 0)
        continue;

    if (input.StartsWith('/'))
    {
        var parts = input.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var argument = parts.Length > 1 ? parts[1] : null;
        switch (parts[0].ToLowerInvariant())
        {
            case "/exit" or "/quit":
                return 0;
            case "/help":
                PrintHelp(agentName);
                break;
            case "/clear":
                lucy.Reset();
                Console.WriteLine("(new conversation)");
                break;
            case "/save":
                await SaveAsync(argument);
                break;
            case "/load":
                await LoadAsync(argument);
                break;
            case "/sessions":
                PrintSessions();
                break;
            case "/transcribe":
                if (argument is null)
                    Console.WriteLine("Usage: /transcribe <name>   (.md by default, or <name>.docx)");
                else if (TryWriteTranscript(argument) is { } path)
                    Console.WriteLine($"Transcript written: {path}");
                break;
            default:
                // Don't send a mistyped command to Lucy as a chat message.
                Console.WriteLine($"Unknown command '{parts[0]}'. Type /help for commands.");
                break;
        }
        continue;
    }

    var historyBeforeTurn = lucy.History.Count;
    turnCts = new CancellationTokenSource();
    try
    {
        await lucy.SendAsync(input, turnCts.Token);
    }
    catch (OperationCanceledException)
    {
        // Drop the interrupted exchange so the next turn starts clean.
        lucy.History.TruncateTo(historyBeforeTurn);
        Console.WriteLine("\n(interrupted)");
    }
    finally
    {
        turnCts.Dispose();
        turnCts = null;
    }
}

return 0;

// Saves the conversation, plus a .docx transcript of it next to the project (same
// as the main CLI's /save).
async Task SaveAsync(string? id)
{
    var savedId = await sessions.SaveAsync(lucy.History, id);
    if (savedId is null)
    {
        Console.WriteLine($"Could not save session '{id}'.");
        return;
    }
    Console.WriteLine($"Session saved: {savedId}");
    if (TryWriteTranscript($"{savedId}.docx") is { } path)
        Console.WriteLine($"Transcript written: {path}");
}

async Task LoadAsync(string? id)
{
    if (id is null)
    {
        Console.WriteLine("Usage: /load <session-id>   (see /sessions)");
        return;
    }
    var history = await sessions.LoadAsync(id);
    if (history is null)
    {
        Console.WriteLine($"Session not found: {id}");
        return;
    }
    lucy.Restore(history);
    Console.WriteLine($"Session loaded: {id} ({history.Count} messages)");
}

void PrintSessions()
{
    var saved = sessions.ListSessions();
    if (saved.Count == 0)
    {
        Console.WriteLine("No saved sessions.");
        return;
    }
    Console.WriteLine($"{"ID",-24} {"Created",-16} Messages");
    foreach (var s in saved)
        Console.WriteLine($"{s.Id,-24} {s.CreatedAt:yyyy-MM-dd HH:mm} {s.MessageCount,8}");
}

string? TryWriteTranscript(string name)
{
    try
    {
        return lucy.WriteTranscript(name);
    }
    catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
    {
        Console.WriteLine($"Could not write transcript: {ex.Message}");
        return null;
    }
}

static void PrintHelp(string agentName)
{
    Console.WriteLine($"""
        Commands:
          /help                Show this help
          /clear               Start a new conversation with {agentName}
          /save [id]           Save this conversation (and a .docx transcript)
          /load <id>           Continue a saved conversation
          /sessions            List saved conversations
          /transcribe <name>   Write a Q&A transcript (<name>.md, or <name>.docx)
          /exit, /quit         Quit
          Ctrl+C               Interrupt {agentName} mid-reply (at the prompt: quit)
        """);
}

// Minimal flags; the provider's usual key variable (e.g. ANTHROPIC_API_KEY) is used
// when --api-key isn't given.
static AgentOptions ParseArgs(string[] args)
{
    var options = new AgentOptions();
    for (int i = 0; i + 1 < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--provider" or "-p": options.Provider = args[++i]; break;
            case "--model" or "-m": options.Model = args[++i]; break;
            case "--api-key" or "-k": options.ApiKey = args[++i]; break;
            case "--max-tokens" when int.TryParse(args[i + 1], out var n): options.MaxTokens = n; i++; break;
        }
    }
    options.ApiKey ??= LlmClientFactory.ApiKeyEnvironmentVariables(options.Provider)
        .Select(Environment.GetEnvironmentVariable)
        .FirstOrDefault(v => v is not null);
    return options;
}
