using AgentSharpApp.Cli;

namespace AgentSharp.Tests.Cli;

public class CommandLineParserTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp-cli-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> _env = new();

    public CommandLineParserTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string MissingConfig => Path.Combine(_tempDir, "none.json");

    private CommandLine Parse(params string[] args) =>
        CommandLineParser.Parse(args, n => _env.GetValueOrDefault(n), MissingConfig);

    [Fact]
    public void NoArgs_DefaultsToAnthropicReplWithNoKey()
    {
        var cl = Parse();

        Assert.Equal("anthropic", cl.Options.Provider);
        Assert.Null(cl.Options.ApiKey);
        Assert.Null(cl.Prompt);
        Assert.False(cl.ShowHelp);
        Assert.False(cl.ShowVersion);
    }

    [Fact]
    public void ValueFlags_AreApplied()
    {
        var cl = Parse("-p", "openai", "-m", "gpt-x", "-k", "key", "--base-url", "http://b",
            "--timeout", "2.5", "--max-tokens", "900", "--max-iterations", "42", "--dir", "/w", "--superprompt", "lucy");

        Assert.Equal("openai", cl.Options.Provider);
        Assert.Equal("gpt-x", cl.Options.Model);
        Assert.Equal("key", cl.Options.ApiKey);
        Assert.Equal("http://b", cl.Options.BaseUrl);
        Assert.Equal(2.5, cl.Options.TimeoutMinutes);
        Assert.Equal(900, cl.Options.MaxTokens);
        Assert.Equal(42, cl.Options.MaxIterations);
        Assert.Equal("/w", cl.Options.WorkingDirectory);
        Assert.Equal("lucy", cl.Options.SuperPrompt);
        Assert.Null(cl.Prompt);
    }

    [Fact]
    public void FlagValues_AreNeverTakenAsThePrompt()
    {
        // Regression: --max-iterations was missing from the old prompt-detection list,
        // so its value became a one-shot prompt.
        var cl = Parse("--max-iterations", "50");

        Assert.Null(cl.Prompt);
        Assert.Equal(50, cl.Options.MaxIterations);
    }

    [Fact]
    public void FirstBareArgument_IsThePrompt()
    {
        var cl = Parse("--model", "m", "fix the bug", "second");

        Assert.Equal("fix the bug", cl.Prompt);
    }

    [Fact]
    public void PromptFlag_SetsThePrompt()
    {
        Assert.Equal("explain this", Parse("--prompt", "explain this").Prompt);
    }

    [Theory]
    [InlineData("--Superprompt")]
    [InlineData("--SUPERPROMPT")]
    [InlineData("--superprompt")]
    public void Flags_AreCaseInsensitive(string flag)
    {
        Assert.Equal("andy", Parse(flag, "andy").Options.SuperPrompt);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpFlag_IsDetected(string flag) => Assert.True(Parse(flag).ShowHelp);

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void VersionFlag_IsDetected(string flag) => Assert.True(Parse(flag).ShowVersion);

    [Fact]
    public void UnparseableNumber_IsIgnoredAndNotTakenAsPrompt()
    {
        var cl = Parse("--max-tokens", "lots");

        Assert.Null(cl.Options.MaxTokens);
        Assert.Null(cl.Prompt);
    }

    [Fact]
    public void EnvironmentVariables_AreApplied()
    {
        _env["AGENT_PROVIDER"] = "grok";
        _env["AGENT_MODEL"] = "grok-9";
        _env["AGENT_BASE_URL"] = "http://e";
        _env["AGENT_TIMEOUT_MINUTES"] = "3";
        _env["AGENT_MAX_TOKENS"] = "700";
        _env["AGENT_MAX_ITERATIONS"] = "7";

        var o = Parse().Options;

        Assert.Equal("grok", o.Provider);
        Assert.Equal("grok-9", o.Model);
        Assert.Equal("http://e", o.BaseUrl);
        Assert.Equal(3, o.TimeoutMinutes);
        Assert.Equal(700, o.MaxTokens);
        Assert.Equal(7, o.MaxIterations);
    }

    [Fact]
    public void Flags_OverrideEnvironment()
    {
        _env["AGENT_MODEL"] = "from-env";

        Assert.Equal("from-flag", Parse("--model", "from-flag").Options.Model);
    }

    [Fact]
    public void ProviderKeyVariable_FollowsProviderFlag()
    {
        _env["ANTHROPIC_API_KEY"] = "anthropic-key";
        _env["OPENAI_API_KEY"] = "openai-key";

        Assert.Equal("openai-key", Parse("--provider", "openai").Options.ApiKey);
        Assert.Equal("anthropic-key", Parse().Options.ApiKey);
    }

    [Fact]
    public void GenericKeyVariable_WinsOverProviderKeyVariable()
    {
        _env["AGENT_API_KEY"] = "generic";
        _env["ANTHROPIC_API_KEY"] = "specific";

        Assert.Equal("generic", Parse().Options.ApiKey);
    }

    [Fact]
    public void GeminiKey_FallsBackToGoogleVariable()
    {
        _env["GOOGLE_API_KEY"] = "google-key";

        Assert.Equal("google-key", Parse("-p", "gemini").Options.ApiKey);
    }

    [Fact]
    public void ConfigFile_IsLoadedAndOverriddenByEnvAndFlags()
    {
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, """{ "provider": "openai", "model": "file-model", "max_iterations": 5, "super_prompt": "connie" }""");
        _env["AGENT_MODEL"] = "env-model";

        var o = CommandLineParser.Parse(["--max-iterations", "9"], n => _env.GetValueOrDefault(n), path).Options;

        Assert.Equal("openai", o.Provider);
        Assert.Equal("env-model", o.Model);
        Assert.Equal(9, o.MaxIterations);
        Assert.Equal("connie", o.SuperPrompt);
    }

    [Fact]
    public void MalformedConfigFile_FallsBackToDefaults()
    {
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, "{ not json");

        var o = CommandLineParser.Parse([], n => null, path).Options;

        Assert.Equal("anthropic", o.Provider);
    }
}
