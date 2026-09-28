using AgentSharpLib.Agent.MultiAgent;
using AgentSharpLib.Output;

namespace AgentLucyApp;

/// <summary>
/// Renders agent events with plain System.Console and a few colors. Lucy's replies
/// are written as-is; everything else is dimmed so the conversation stands out.
/// </summary>
internal sealed class ConsoleAgentOutput : IAgentOutput
{
    // Parallel sub-agents share this instance; keep each write (and its color) atomic.
    private readonly object _lock = new();

    public void Text(string text) => Write(text, null);
    public void LineBreak() => Write(Environment.NewLine, null);
    public void Trace(string text) => WriteLine(text, ConsoleColor.DarkGray);
    public void Info(string message) => WriteLine(message, ConsoleColor.DarkGray);
    public void Warning(string message) => WriteLine(message, ConsoleColor.Yellow);
    public void Error(string message) => WriteLine($"{Environment.NewLine}Error: {message}", ConsoleColor.Red);

    public void TransientError(Exception exception) =>
        WriteLine($"{Environment.NewLine}Error: {exception.GetType().Name}: {exception.Message}", ConsoleColor.Red);

    public void ToolAutoApproved(string toolName) => WriteLine($"  [auto-approved] {toolName}", ConsoleColor.DarkGray);
    public void ToolSkipped(string toolTypeName, string reason) => WriteLine($"Skipped tool '{toolTypeName}': {reason}", ConsoleColor.DarkYellow);

    public void SubAgentStarted(string name, string task, bool inBatch) =>
        WriteLine(inBatch ? $"  Starting: {name}" : $"Spawning sub-agent: {name}", ConsoleColor.Cyan);

    public void SubAgentFinished(string name, SubAgentStatus status, bool inBatch) =>
        WriteLine($"{(inBatch ? "  " : "")}Sub-agent '{name}' {status}.",
            status == SubAgentStatus.Completed ? ConsoleColor.Green : ConsoleColor.Red);

    public void SubAgentBatchStarted(int count, int maxConcurrent) =>
        WriteLine($"Spawning {count} sub-agents (max {maxConcurrent} at once)...", ConsoleColor.Cyan);

    public void SubAgentBatchFinished(int count) => WriteLine($"All {count} sub-agents finished.", ConsoleColor.Cyan);

    private void WriteLine(string text, ConsoleColor? color) => Write(text + Environment.NewLine, color);

    private void Write(string text, ConsoleColor? color)
    {
        lock (_lock)
        {
            if (color is { } c)
            {
                var previous = Console.ForegroundColor;
                Console.ForegroundColor = c;
                Console.Write(text);
                Console.ForegroundColor = previous;
            }
            else
            {
                Console.Write(text);
            }
        }
    }
}
