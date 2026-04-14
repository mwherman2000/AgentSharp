namespace AgentSharp.Llm;

/// <summary>
/// Abstraction over LLM providers (Anthropic, OpenAI, etc.).
/// Each provider implements this interface to translate between
/// our domain types and the provider's API format.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Provider name for display purposes.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Model identifier being used.
    /// </summary>
    string ModelId { get; }

    /// <summary>
    /// Send a request and get a complete response (non-streaming).
    /// </summary>
    Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>
    /// Send a request and stream the response as events.
    /// This is the primary method used by the agent loop for real-time rendering.
    /// </summary>
    IAsyncEnumerable<StreamEvent> StreamAsync(LlmRequest request, CancellationToken ct = default);
}
