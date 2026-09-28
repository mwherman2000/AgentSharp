using AgentSharpLib.Llm;

namespace AgentSharpLib;

/// <summary>
/// Settings for building an agent: which LLM to talk to and how, plus per-turn
/// limits and persona selection. A plain settings bag -- the library never reads
/// the environment, command line, or config files itself; hosts populate this
/// however they like (the CLI does it in AgentSharpApp.Cli.CommandLineParser).
/// Property names double as the snake_case keys of ~/.agentsharp/config.json.
/// </summary>
public class AgentOptions
{
    public string Provider { get; set; } = "anthropic";
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }

    /// <summary>Request timeout in minutes, applied to whichever provider is active. Null means
    /// "use the provider's default" -- <see cref="OpenAiCompatibleClient.DefaultOllamaTimeout"/>
    /// for Ollama (local inference can run far longer than a typical hosted-API timeout),
    /// or each client's own <c>DefaultTimeout</c> otherwise.</summary>
    public double? TimeoutMinutes { get; set; }

    /// <summary>Max output tokens per LLM request. Null means "use the default",
    /// i.e. <see cref="AgentSharpLib.Agent.AgentLoop.DefaultMaxTokens"/>.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Cap on LLM&lt;-&gt;tool round-trips within a single turn. Null means "use
    /// the default", i.e. <see cref="AgentSharpLib.Agent.AgentLoop.DefaultMaxIterations"/>.
    /// Raise this for deep, multi-phase tasks (e.g. a sourced report that fetches and
    /// verifies dozens of URLs individually) that legitimately need more round-trips
    /// than the default budgets for.</summary>
    public int? MaxIterations { get; set; }

    /// <summary>Directory to treat as the project root: where relative tool paths
    /// (write_file, read_file, run_shell, ...) resolve, and what ProjectContext scans.
    /// Null means "use the process's actual current directory" -- the default, and
    /// what every path resolution already falls back to on its own.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Selects which base persona/prompt <see cref="Context.SystemPromptBuilder"/>
    /// uses. Null means "use the default" -- Andy, i.e.
    /// <see cref="Context.SystemPromptBuilder.ResolveSuperPrompt"/> with a null argument.</summary>
    public string? SuperPrompt { get; set; }

    /// <summary>
    /// Returns the effective model, using a provider-specific default if none was explicitly set.
    /// </summary>
    public string EffectiveModel => Model ?? LlmClientFactory.DefaultModel(Provider);
}
