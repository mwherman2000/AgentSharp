namespace AgentSharpLib.Llm;

/// <summary>
/// Builds the right <see cref="ILlmClient"/> for an <see cref="AgentOptions"/>, and
/// answers the per-provider questions a host needs when populating those options
/// (default model, which environment variables conventionally hold the API key).
/// </summary>
public static class LlmClientFactory
{
    /// <summary>
    /// Create an ILlmClient for <paramref name="options"/>. Throws
    /// <see cref="InvalidOperationException"/> when a provider that needs an API key
    /// has none, or the provider is unknown and no base URL was given.
    /// </summary>
    public static ILlmClient Create(AgentOptions options)
    {
        var model = options.EffectiveModel;
        var provider = options.Provider;
        var providerKey = provider.ToLowerInvariant();
        var apiKey = options.ApiKey;
        var baseUrl = options.BaseUrl;
        TimeSpan? timeout = options.TimeoutMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null;

        // Ollama runs locally and does not require an API key.
        if (providerKey == "ollama")
            return OpenAiCompatibleClient.ForOllama(model, baseUrl ?? "http://localhost:11434/v1", timeout);

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                $"No API key configured for provider '{provider}'. Set {string.Join(" or ", ApiKeyEnvironmentVariables(provider))}.");

        return providerKey switch
        {
            "anthropic" => new AnthropicClient(apiKey, model, timeout),
            "openai" => baseUrl is not null
                ? new OpenAiCompatibleClient(apiKey, model, baseUrl, "OpenAI", timeout)
                : OpenAiCompatibleClient.ForOpenAi(apiKey, model, timeout),
            "grok" or "xai" => OpenAiCompatibleClient.ForGrok(apiKey, model, timeout),
            "gemini" or "google" => OpenAiCompatibleClient.ForGemini(apiKey, model, timeout),
            _ when baseUrl is not null => new OpenAiCompatibleClient(apiKey, model, baseUrl, provider, timeout),
            _ => throw new InvalidOperationException($"Unknown provider: {provider}. Supply a base URL for custom OpenAI-compatible providers.")
        };
    }

    /// <summary>The model used when <see cref="AgentOptions.Model"/> is not set.</summary>
    public static string DefaultModel(string provider) => provider.ToLowerInvariant() switch
    {
        "anthropic" => "claude-sonnet-5",
        "openai" => "gpt-4o",
        "grok" or "xai" => "grok-3",
        "gemini" or "google" => "gemini-2.5-pro",
        "ollama" => "qwen2.5:1.5b",
        _ => "gpt-4o" // sensible fallback for custom OpenAI-compatible providers
    };

    /// <summary>
    /// Environment variables that conventionally hold <paramref name="provider"/>'s API
    /// key, in lookup order. Empty for Ollama (no key needed); AGENT_API_KEY for
    /// custom providers.
    /// </summary>
    public static IReadOnlyList<string> ApiKeyEnvironmentVariables(string provider) => provider.ToLowerInvariant() switch
    {
        "anthropic" => ["ANTHROPIC_API_KEY"],
        "openai" => ["OPENAI_API_KEY"],
        "grok" or "xai" => ["XAI_API_KEY"],
        "gemini" or "google" => ["GEMINI_API_KEY", "GOOGLE_API_KEY"],
        "ollama" => [],
        _ => ["AGENT_API_KEY"]
    };
}
