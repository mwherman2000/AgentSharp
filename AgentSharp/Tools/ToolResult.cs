namespace AgentSharp.Tools;

/// <summary>
/// Result of executing a tool. Either success with output or an error message.
/// Errors are returned as data (not exceptions) so the LLM can reason about them.
/// </summary>
public record ToolResult
{
    public required string Output { get; init; }
    public required bool IsError { get; init; }

    public static ToolResult Success(string output) => new() { Output = output, IsError = false };
    public static ToolResult Error(string message) => new() { Output = message, IsError = true };
}
