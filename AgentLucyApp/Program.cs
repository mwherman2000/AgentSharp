using AgentLucyApp;
using AgentSharpLib;
using AgentSharpLib.Llm;

// ============================================================================
// AgentLucyApp - a minimal chat with Lucy, built only on AgentSharpLib.
//
// Everything agent-related (LLM client, tools, memory, sub-agents, the Lucy
// persona) comes from AgentBuilder; this file only supplies the console I/O.
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

Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine($"Lucy  ({lucy.Llm.ProviderName} / {lucy.Llm.ModelId})");
Console.ResetColor();
Console.WriteLine("Type a message. /clear starts over, /exit quits, Ctrl+C interrupts Lucy mid-reply.");

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
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("\nyou> ");
    Console.ResetColor();

    var input = Console.ReadLine();
    if (input is null)
        break; // end of input
    input = input.Trim();
    if (input.Length == 0)
        continue;

    if (input is "/exit" or "/quit")
        break;
    if (input == "/clear")
    {
        lucy.Reset();
        Console.WriteLine("(new conversation)");
        continue;
    }

    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.Write("lucy> ");
    Console.ResetColor();

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
