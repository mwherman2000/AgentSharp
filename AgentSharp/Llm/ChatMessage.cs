using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSharp.Llm;

/// <summary>
/// Roles in the conversation.
/// </summary>
public static class MessageRole
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>
/// A content block within a message. Can be text, tool_use, or tool_result.
/// </summary>
public abstract record ContentBlock
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public record TextBlock : ContentBlock
{
    public override string Type => "text";

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

public record ToolUseBlock : ContentBlock
{
    public override string Type => "tool_use";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("input")]
    public required JsonElement Input { get; init; }

    /// <summary>
    /// Set when the LLM produced invalid JSON for the tool input.
    /// The agent loop returns this as an error result so the LLM can self-correct.
    /// </summary>
    [JsonIgnore]
    public string? ParseError { get; init; }
}

public record ToolResultBlock : ContentBlock
{
    public override string Type => "tool_result";

    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("is_error")]
    public bool IsError { get; init; }
}

/// <summary>
/// A message in the conversation history.
/// </summary>
public record ChatMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required IReadOnlyList<ContentBlock> Content { get; init; }

    /// <summary>
    /// Create a simple user text message.
    /// </summary>
    public static ChatMessage UserText(string text) => new()
    {
        Role = MessageRole.User,
        Content = [new TextBlock { Text = text }]
    };

    /// <summary>
    /// Create a simple assistant text message.
    /// </summary>
    public static ChatMessage AssistantText(string text) => new()
    {
        Role = MessageRole.Assistant,
        Content = [new TextBlock { Text = text }]
    };

    /// <summary>
    /// Create a tool results message (role: user).
    /// </summary>
    public static ChatMessage ToolResults(IEnumerable<ToolResultBlock> results) => new()
    {
        Role = MessageRole.User,
        Content = results.ToList()
    };

    /// <summary>
    /// Get all text content concatenated. A message can hold several text blocks
    /// separated by tool_use blocks (e.g. text, tool_use, more text) -- each later
    /// block is fresh model output that doesn't assume a leading space, so plain
    /// concatenation would glue unrelated sentences together. Insert a newline at
    /// any block boundary that doesn't already have whitespace on one side.
    /// </summary>
    public string GetText()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var block in Content.OfType<TextBlock>())
        {
            if (sb.Length > 0 && block.Text.Length > 0 &&
                !char.IsWhiteSpace(sb[sb.Length - 1]) &&
                !char.IsWhiteSpace(block.Text[0]))
            {
                sb.Append('\n');
            }
            sb.Append(block.Text);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Get all tool use blocks.
    /// </summary>
    public IEnumerable<ToolUseBlock> GetToolUses() => Content.OfType<ToolUseBlock>();

    /// <summary>
    /// Check if this message contains tool use blocks.
    /// </summary>
    public bool HasToolUse => Content.Any(b => b is ToolUseBlock);
}
