using AgentLucyApp;
using AgentSharpLib;
using AgentSharpLib.Agent;
using AgentSharpLib.Context;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;
using AgentSharpLib.Telemetry;

// ============================================================================
// AgentLucyApp - a minimal chat with Lucy, built only on AgentSharpLib.
//
// Everything agent-related (LLM client, tools, memory, sub-agents, the Lucy
// persona, saved sessions, transcripts) comes from the library; this file only
// supplies the console I/O.
// ============================================================================

var options = ParseArgs(args);

// Tracing is off unless AGENT_ENABLE_OTEL is set, or /jaeger turns it on mid-session.
AgentTelemetry.Initialize();

AgentSession lucy;
try
{
    lucy = await new AgentBuilder()
        .WithOptions(options)
        .WithOutput(new ConsoleAgentOutput())
        .WithApprovalPrompt(new ConsoleApprovalPrompt())
        .BuildAsync();
}
catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Usage: AgentLucyApp [--provider <name>] [--model <name>] [--api-key <key>] [--max-tokens <n>] [--Superprompt <name>]");
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

var running = true;
while (running)
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
            case "/exit" or "/quit" or "/q":
                running = false;
                break;
            case "/help" or "/h" or "/?":
                PrintHelp(agentName);
                break;
            case "/clear" or "/cls":
                lucy.Reset();
                Console.WriteLine("(new conversation)");
                break;
            case "/save":
                await SaveAsync(argument);
                break;
            case "/load" or "/resume":
                await LoadAsync(argument);
                break;
            case "/sessions" or "/ls":
                PrintSessions();
                break;
            case "/status":
                PrintStatus();
                break;
            case "/model":
                Console.WriteLine($"Current model: {lucy.Llm.ProviderName} / {lucy.Llm.ModelId}");
                Console.WriteLine("To change the model, restart with --model <name>.");
                break;
            case "/memory" or "/mem":
                HandleMemory(argument);
                break;
            case "/sync":
                AgentFlags.SyncMode = !AgentFlags.SyncMode;
                Console.WriteLine(AgentFlags.SyncMode
                    ? "SyncMode: on (non-streaming replies)"
                    : "SyncMode: off (streaming replies, default)");
                break;
            case "/request":
                AgentFlags.RequestTrace = !AgentFlags.RequestTrace;
                Console.WriteLine($"RequestTrace: {AgentFlags.RequestTrace}");
                break;
            case "/history":
                AgentFlags.HistoryTrace = !AgentFlags.HistoryTrace;
                Console.WriteLine($"HistoryTrace: {AgentFlags.HistoryTrace}");
                break;
            case "/tools":
                AgentFlags.ToolsTrace = !AgentFlags.ToolsTrace;
                Console.WriteLine($"ToolsTrace: {AgentFlags.ToolsTrace}");
                break;
            case "/jaeger":
                var endpoint = argument ?? AgentTelemetry.DefaultJaegerEndpoint;
                if (!AgentTelemetry.IsValidEndpoint(endpoint))
                {
                    Console.WriteLine($"Not a valid endpoint URL: {endpoint} (expected e.g. {AgentTelemetry.DefaultJaegerEndpoint})");
                    break;
                }
                AgentTelemetry.SwitchToJaeger(endpoint);
                Console.WriteLine($"OTel export switched to Jaeger (OTLP @ {endpoint}).");
                Console.WriteLine($"View traces at {AgentTelemetry.DefaultJaegerUiUrl} (assumes Jaeger is running locally).");
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

// Flush whichever trace exporter ended up active (console, or Jaeger via /jaeger).
AgentTelemetry.Shutdown();
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

void PrintStatus()
{
    var loop = lucy.Loop;
    var tools = string.Join(", ", lucy.Tools.All.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal));
    var effectiveInput = loop.TotalInputTokens + loop.TotalCacheCreationTokens + loop.TotalCacheReadTokens;
    var hitRate = effectiveInput == 0 ? "" : $" ({100.0 * loop.TotalCacheReadTokens / effectiveInput:F0}% hit rate)";

    Console.WriteLine($"""
        Agent:          {agentName}
        Provider:       {lucy.Llm.ProviderName}
        Model:          {lucy.Llm.ModelId}
        Max tokens:     {lucy.MaxTokens}
        Max iterations: {lucy.MaxIterations}
        Tools:          {lucy.Tools.All.Count} ({tools})
        Messages:       {lucy.History.Count}
        Tokens:         {loop.TotalInputTokens} in / {loop.TotalOutputTokens} out (this conversation)
        Cache:          {loop.TotalCacheCreationTokens} written / {loop.TotalCacheReadTokens} read{hitRate}
        Session:        {SessionUsage.FormatElapsed(lucy.Usage.Elapsed)} elapsed, {lucy.Usage.TotalTokens:N0} tokens (all conversations and sub-agents)
        Directory:     {lucy.Project.WorkingDirectory}
        Git branch:     {lucy.Project.GitBranch ?? "N/A"}
        Memory:         {lucy.Memory?.FilePath ?? "off"}
        """);
}

// /memory shows MEMORY.md; /memory clear deletes it.
void HandleMemory(string? argument)
{
    if (lucy.Memory is not { } memory)
    {
        Console.WriteLine("Memory is turned off.");
        return;
    }
    if (argument is null)
    {
        var content = memory.Read();
        Console.WriteLine(content ?? $"No memory yet ({memory.FilePath}).");
        return;
    }
    if (argument.Equals("clear", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine(memory.Clear() ? "Memory cleared." : "No memory to clear.");
        return;
    }
    Console.WriteLine("Usage: /memory [clear]");
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
          /help, /h, /?        Show this help
          /clear, /cls         Start a new conversation with {agentName}
          /save [id]           Save this conversation (and a .docx transcript)
          /load, /resume <id>  Continue a saved conversation
          /sessions, /ls       List saved conversations
          /status              Model, tools, token usage, directory
          /model               Show the current provider and model
          /memory, /mem [clear]  Show {agentName}'s MEMORY.md, or delete it
          /transcribe <name>   Write a Q&A transcript (<name>.md, or <name>.docx)
          /exit, /quit, /q     Quit
          Ctrl+C               Interrupt {agentName} mid-reply (at the prompt: quit)

        Diagnostics:
          /sync                Toggle streaming vs. non-streaming replies
          /request             Toggle dumping each request sent to the model
          /history             Toggle dumping the conversation history with each request
          /tools               Toggle dumping the tool definitions with each request
          /jaeger [endpoint]   Send OpenTelemetry traces to Jaeger (default {AgentTelemetry.DefaultJaegerEndpoint})
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
            case "--superprompt": options.SuperPrompt = args[++i]; break;
        }
    }
    // Lucy unless --Superprompt picks another persona (SystemPromptBuilder.AvailableSuperPrompts).
    options.SuperPrompt ??= "lucy";
    options.ApiKey ??= LlmClientFactory.ApiKeyEnvironmentVariables(options.Provider)
        .Select(Environment.GetEnvironmentVariable)
        .FirstOrDefault(v => v is not null);
    return options;
}
