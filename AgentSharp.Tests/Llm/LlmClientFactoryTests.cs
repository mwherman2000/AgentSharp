using AgentSharpLib;
using AgentSharpLib.Llm;

namespace AgentSharp.Tests.Llm;

public class LlmClientFactoryTests
{
    [Theory]
    [InlineData("anthropic", typeof(AnthropicClient))]
    [InlineData("openai", typeof(OpenAiCompatibleClient))]
    [InlineData("grok", typeof(OpenAiCompatibleClient))]
    [InlineData("xai", typeof(OpenAiCompatibleClient))]
    [InlineData("gemini", typeof(OpenAiCompatibleClient))]
    [InlineData("Anthropic", typeof(AnthropicClient))]
    public void Create_KnownProviderWithKey_ReturnsMatchingClient(string provider, Type expected)
    {
        var client = LlmClientFactory.Create(new AgentOptions { Provider = provider, ApiKey = "k" });

        Assert.IsType(expected, client);
    }

    [Fact]
    public void Create_Ollama_NeedsNoKey()
    {
        var client = LlmClientFactory.Create(new AgentOptions { Provider = "ollama" });

        Assert.IsType<OpenAiCompatibleClient>(client);
    }

    [Fact]
    public void Create_MissingKey_ThrowsNamingTheKeyVariable()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LlmClientFactory.Create(new AgentOptions { Provider = "anthropic" }));

        Assert.Contains("API key", ex.Message);
        Assert.Contains("ANTHROPIC_API_KEY", ex.Message);
    }

    [Fact]
    public void Create_UnknownProviderWithoutBaseUrl_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LlmClientFactory.Create(new AgentOptions { Provider = "acme", ApiKey = "k" }));
    }

    [Fact]
    public void Create_UnknownProviderWithBaseUrl_ReturnsOpenAiCompatibleClient()
    {
        var client = LlmClientFactory.Create(new AgentOptions { Provider = "acme", ApiKey = "k", BaseUrl = "http://localhost:1/v1" });

        Assert.IsType<OpenAiCompatibleClient>(client);
    }

    [Fact]
    public void EffectiveModel_UsesProviderDefaultUnlessSet()
    {
        Assert.Equal(LlmClientFactory.DefaultModel("ollama"), new AgentOptions { Provider = "ollama" }.EffectiveModel);
        Assert.Equal("custom", new AgentOptions { Provider = "ollama", Model = "custom" }.EffectiveModel);
    }

    [Fact]
    public void ApiKeyEnvironmentVariables_GeminiChecksBothNames()
    {
        Assert.Equal(["GEMINI_API_KEY", "GOOGLE_API_KEY"], LlmClientFactory.ApiKeyEnvironmentVariables("gemini"));
        Assert.Empty(LlmClientFactory.ApiKeyEnvironmentVariables("ollama"));
    }
}
