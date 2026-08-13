using System.Text.Json;
using AgentSharp.Llm;
using Spectre.Console;

namespace AgentSharp;

/// <summary>
/// Application configuration. Loaded from environment variables,
/// config file (~/.agentsharp/config.json), and CLI arguments.
/// </summary>
public class Configuration
{
    public string Provider { get; set; } = "anthropic";
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }

    /// <summary>Request timeout in minutes, currently only applied to the Ollama client (local
    /// inference can run far longer than a typical hosted-API timeout). Null means "use the
    /// provider's default", i.e. <see cref="OpenAiCompatibleClient.DefaultOllamaTimeout"/>.</summary>
    public double? TimeoutMinutes { get; set; }

    /// <summary>Max output tokens per LLM request. Null means "use the default",
    /// i.e. <see cref="AgentSharp.Agent.AgentLoop.DefaultMaxTokens"/>.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Directory to treat as the project root: where relative tool paths
    /// (write_file, read_file, run_shell, ...) resolve, and what ProjectContext scans.
    /// Null means "use the process's actual current directory" -- the default, and
    /// what every path resolution already falls back to on its own.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Returns the effective model, using a provider-specific default if none was explicitly set.
    /// </summary>
    public string EffectiveModel => Model ?? GetDefaultModel(Provider);

    private static string GetDefaultModel(string provider) => provider.ToLowerInvariant() switch
    {
        "anthropic" => "claude-sonnet-5",
        "openai" => "gpt-4o",
        "grok" or "xai" => "grok-3",
        "gemini" or "google" => "gemini-2.5-pro",
        "ollama" => "qwen2.5:1.5b",
        _ => "gpt-4o" // sensible fallback for custom OpenAI-compatible providers
    };

    /// <summary>
    /// Load configuration from environment variables and optional config file.
    /// CLI arguments override everything.
    /// </summary>
    public static Configuration Load(string[] args)
    {
        var config = new Configuration();

        // Load from config file
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agentsharp", "config.json");

        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var fileConfig = JsonSerializer.Deserialize<Configuration>(json,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                if (fileConfig is not null)
                    config = fileConfig;
            }
            catch { }
        }

        // Environment variables override
        config.Provider = Environment.GetEnvironmentVariable("AGENT_PROVIDER") ?? config.Provider;
        config.ApiKey = Environment.GetEnvironmentVariable("AGENT_API_KEY") ?? config.ApiKey;
        var envModel = Environment.GetEnvironmentVariable("AGENT_MODEL");
        if (envModel is not null)
            config.Model = envModel;
        config.BaseUrl = Environment.GetEnvironmentVariable("AGENT_BASE_URL") ?? config.BaseUrl;
        var envTimeout = Environment.GetEnvironmentVariable("AGENT_TIMEOUT_MINUTES");
        if (envTimeout is not null && double.TryParse(envTimeout, System.Globalization.CultureInfo.InvariantCulture, out var envTimeoutMinutes))
            config.TimeoutMinutes = envTimeoutMinutes;
        var envMaxTokens = Environment.GetEnvironmentVariable("AGENT_MAX_TOKENS");
        if (envMaxTokens is not null && int.TryParse(envMaxTokens, System.Globalization.CultureInfo.InvariantCulture, out var envMaxTokensValue))
            config.MaxTokens = envMaxTokensValue;

        // CLI argument overrides (must run before provider-specific key lookup
        // so that --provider is known before we pick which env var to read)
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--provider" or "-p" when i + 1 < args.Length:
                    config.Provider = args[++i];
                    break;
                case "--model" or "-m" when i + 1 < args.Length:
                    config.Model = args[++i];
                    break;
                case "--api-key" or "-k" when i + 1 < args.Length:
                    config.ApiKey = args[++i];
                    break;
                case "--base-url" when i + 1 < args.Length:
                    config.BaseUrl = args[++i];
                    break;
                case "--timeout" when i + 1 < args.Length && double.TryParse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture, out var timeoutMinutes):
                    config.TimeoutMinutes = timeoutMinutes;
                    i++;
                    break;
                case "--max-tokens" when i + 1 < args.Length && int.TryParse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture, out var maxTokensValue):
                    config.MaxTokens = maxTokensValue;
                    i++;
                    break;
                case "--dir" when i + 1 < args.Length:
                    config.WorkingDirectory = args[++i];
                    break;
            }
        }

        // Provider-specific API key env vars (after CLI args so --provider is applied)
        config.ApiKey ??= config.Provider.ToLowerInvariant() switch
        {
            "anthropic" => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
            "openai" => Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            "grok" or "xai" => Environment.GetEnvironmentVariable("XAI_API_KEY"),
            "gemini" or "google" => Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                                    ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY"),
            _ => null
        };

        return config;
    }

    /// <summary>
    /// Create an ILlmClient based on the configuration.
    /// </summary>
    public ILlmClient CreateLlmClient()
    {
        var model = EffectiveModel;
        var providerKey = Provider.ToLowerInvariant();

        // Ollama runs locally and does not require an API key.
        if (providerKey == "ollama")
            return OpenAiCompatibleClient.ForOllama(model, BaseUrl ?? "http://localhost:11434/v1",
                TimeoutMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null);

        if (string.IsNullOrEmpty(ApiKey))
            throw new InvalidOperationException(
                $"No API key configured. Set {GetApiKeyEnvVar()} or use --api-key.");

        return providerKey switch
        {
            "anthropic" => new AnthropicClient(ApiKey, model),
            "openai" => BaseUrl is not null
                ? new OpenAiCompatibleClient(ApiKey, model, BaseUrl, "OpenAI")
                : OpenAiCompatibleClient.ForOpenAi(ApiKey, model),
            "grok" or "xai" => OpenAiCompatibleClient.ForGrok(ApiKey, model),
            "gemini" or "google" => OpenAiCompatibleClient.ForGemini(ApiKey, model),
            _ when BaseUrl is not null => new OpenAiCompatibleClient(ApiKey, model, BaseUrl, Provider),
            _ => throw new InvalidOperationException($"Unknown provider: {Provider}. Use --base-url for custom providers.")
        };
    }

    private string GetApiKeyEnvVar() => Provider.ToLowerInvariant() switch
    {
        "anthropic" => "ANTHROPIC_API_KEY",
        "openai" => "OPENAI_API_KEY",
        "grok" or "xai" => "XAI_API_KEY",
        "gemini" or "google" => "GEMINI_API_KEY or GOOGLE_API_KEY",
        _ => "AGENT_API_KEY"
    };
}
