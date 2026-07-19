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
        AnsiConsole.MarkupLine($"[dim]Base Url: {config.BaseUrl}[/]");

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
        if (string.IsNullOrEmpty(ApiKey))
            throw new InvalidOperationException(
                $"No API key configured. Set {GetApiKeyEnvVar()} or use --api-key.");

        var model = EffectiveModel;
        return Provider.ToLowerInvariant() switch
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
