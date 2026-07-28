namespace AgentSharp.Llm;

/// <summary>
/// Request to send to an LLM provider.
/// Provider-agnostic -- each provider translates this to its own API format.
/// </summary>
public record LlmRequest
{
    public required string SystemPrompt { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }
    public int MaxTokens { get; init; } = 8192;
    public double Temperature { get; init; } = 0.7;
}

/// <summary>
/// Non-streaming response from the LLM.
/// </summary>
public record LlmResponse
{
    public required ChatMessage Message { get; init; }
    public required string StopReason { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
}
