using System.Globalization;
using System.Text.Json;
using AgentSharpLib;
using AgentSharpLib.Llm;

namespace AgentSharpApp.Cli;

/// <summary>What the CLI was asked to do.</summary>
/// <param name="Prompt">One-shot prompt (<c>--prompt X</c>, or the first bare argument);
/// null means start the interactive REPL.</param>
internal sealed record CommandLine(AgentOptions Options, string? Prompt, bool ShowHelp, bool ShowVersion);

/// <summary>
/// Builds <see cref="AgentOptions"/> for the CLI. Later sources override earlier ones:
/// <list type="number">
/// <item>~/.agentsharp/config.json (snake_case keys matching AgentOptions)</item>
/// <item>AGENT_* environment variables</item>
/// <item>command-line flags</item>
/// <item>the provider's own API key variable (e.g. ANTHROPIC_API_KEY), only if no key
/// was set above -- read last so a --provider flag picks which variable applies</item>
/// </list>
/// Flags are matched case-insensitively.
/// </summary>
internal static class CommandLineParser
{
    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentsharp", "config.json");

    /// <summary>Flags that take a value, and how each applies it. The same table
    /// decides which arguments are flag values rather than a one-shot prompt, so a new
    /// flag can't be mistaken for a prompt (as --max-iterations once was).</summary>
    private static readonly Dictionary<string, Action<AgentOptions, string>> ValueFlags =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["--provider"] = (o, v) => o.Provider = v,
            ["-p"] = (o, v) => o.Provider = v,
            ["--model"] = (o, v) => o.Model = v,
            ["-m"] = (o, v) => o.Model = v,
            ["--api-key"] = (o, v) => o.ApiKey = v,
            ["-k"] = (o, v) => o.ApiKey = v,
            ["--base-url"] = (o, v) => o.BaseUrl = v,
            ["--timeout"] = (o, v) => { if (TryParseDouble(v, out var d)) o.TimeoutMinutes = d; },
            ["--max-tokens"] = (o, v) => { if (TryParseInt(v, out var n)) o.MaxTokens = n; },
            ["--max-iterations"] = (o, v) => { if (TryParseInt(v, out var n)) o.MaxIterations = n; },
            ["--dir"] = (o, v) => o.WorkingDirectory = v,
            ["--superprompt"] = (o, v) => o.SuperPrompt = v,
        };

    /// <param name="getEnv">Environment lookup; defaults to the real environment.</param>
    /// <param name="configPath">Config file to read; defaults to <see cref="DefaultConfigPath"/>.</param>
    public static CommandLine Parse(string[] args, Func<string, string?>? getEnv = null, string? configPath = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var options = LoadConfigFile(configPath ?? DefaultConfigPath) ?? new AgentOptions();

        // Environment variables
        options.Provider = getEnv("AGENT_PROVIDER") ?? options.Provider;
        options.ApiKey = getEnv("AGENT_API_KEY") ?? options.ApiKey;
        options.Model = getEnv("AGENT_MODEL") ?? options.Model;
        options.BaseUrl = getEnv("AGENT_BASE_URL") ?? options.BaseUrl;
        if (TryParseDouble(getEnv("AGENT_TIMEOUT_MINUTES"), out var envTimeout))
            options.TimeoutMinutes = envTimeout;
        if (TryParseInt(getEnv("AGENT_MAX_TOKENS"), out var envMaxTokens))
            options.MaxTokens = envMaxTokens;
        if (TryParseInt(getEnv("AGENT_MAX_ITERATIONS"), out var envMaxIterations))
            options.MaxIterations = envMaxIterations;

        // Command-line flags
        string? prompt = null;
        bool showHelp = false, showVersion = false;
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (Is(arg, "--help") || Is(arg, "-h"))
                showHelp = true;
            else if (Is(arg, "--version") || Is(arg, "-v"))
                showVersion = true;
            else if (Is(arg, "--prompt"))
            {
                if (i + 1 < args.Length)
                    prompt ??= args[++i];
            }
            else if (ValueFlags.TryGetValue(arg, out var apply))
            {
                if (i + 1 < args.Length)
                    apply(options, args[++i]);
            }
            else if (!arg.StartsWith('-'))
                prompt ??= arg; // agentsharp "do something"
            // Unknown flags are ignored.
        }

        // Provider-specific key variable, now that --provider is known.
        options.ApiKey ??= LlmClientFactory.ApiKeyEnvironmentVariables(options.Provider)
            .Select(getEnv)
            .FirstOrDefault(v => v is not null);

        return new CommandLine(options, prompt, showHelp, showVersion);
    }

    private static AgentOptions? LoadConfigFile(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<AgentOptions>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        }
        catch
        {
            return null; // A malformed config file falls back to defaults, as it always has.
        }
    }

    private static bool Is(string arg, string flag) => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase);

    private static bool TryParseInt(string? s, out int value) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseDouble(string? s, out double value) =>
        double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
}
