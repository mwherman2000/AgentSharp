using AgentSharpLib.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class WebSearchToolTests
{
    [Fact]
    public void ResolveRealUrl_UnwrapsDuckDuckGoRedirect()
    {
        var href = "//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.example.com%2Fpage&rut=abc123";

        var url = WebSearchTool.ResolveRealUrl(href);

        Assert.Equal("https://www.example.com/page", url);
    }

    [Fact]
    public void ResolveRealUrl_HandlesRedirectWithNoTrailingParams()
    {
        var href = "//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.example.com%2Fpage";

        var url = WebSearchTool.ResolveRealUrl(href);

        Assert.Equal("https://www.example.com/page", url);
    }

    [Fact]
    public void ResolveRealUrl_PassesThroughAlreadyDirectUrl()
    {
        var url = WebSearchTool.ResolveRealUrl("https://www.example.com/direct");

        Assert.Equal("https://www.example.com/direct", url);
    }

    [Fact]
    public void ResolveRealUrl_AddsSchemeToProtocolRelativeUrl()
    {
        var url = WebSearchTool.ResolveRealUrl("//www.example.com/direct");

        Assert.Equal("https://www.example.com/direct", url);
    }

    [Fact]
    public void ResolveRealUrl_RejectsNonHttpScheme()
    {
        var url = WebSearchTool.ResolveRealUrl("javascript:alert(1)");

        Assert.Null(url);
    }

    [Fact]
    public async Task ParseResultsAsync_ExtractsTitleUrlAndSnippet()
    {
        const string html = """
            <div class="results">
              <div class="result results_links results_links_deep web-result">
                <div class="links_main links_deep result__body">
                  <h2 class="result__title">
                    <a rel="nofollow" class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.fda.gov%2Fsome-real-page&amp;rut=x">FDA: Some Real Page</a>
                  </h2>
                  <a class="result__snippet" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.fda.gov%2Fsome-real-page">A snippet describing the page.</a>
                </div>
              </div>
            </div>
            """;

        var results = await WebSearchTool.ParseResultsAsync(html, default);

        var result = Assert.Single(results);
        Assert.Equal("FDA: Some Real Page", result.Title);
        Assert.Equal("https://www.fda.gov/some-real-page", result.Url);
        Assert.Equal("A snippet describing the page.", result.Snippet);
    }

    [Fact]
    public async Task ParseResultsAsync_ReturnsEmptyListWhenNoResults()
    {
        const string html = "<div class=\"results\"><div class=\"no-results\">No results.</div></div>";

        var results = await WebSearchTool.ParseResultsAsync(html, default);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ParseResultsAsync_SkipsResultWithNoHref()
    {
        const string html = """
            <div class="result__body">
              <h2 class="result__title"><a class="result__a">Title With No Link</a></h2>
            </div>
            """;

        var results = await WebSearchTool.ParseResultsAsync(html, default);

        Assert.Empty(results);
    }
}
