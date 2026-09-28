using System.Net;
using System.Text.Json;
using AngleSharp;

namespace AgentSharpLib.Tools.Implementations;

/// <summary>
/// Searches the web via DuckDuckGo's HTML endpoint (no API key required) and
/// returns a list of (title, url, snippet) results. Exists so the model has a
/// way to discover a real URL before calling web_fetch -- without this, a
/// research task that needs a citation (e.g. "the FDA's approval notice for
/// X") has no option but to type a URL from memory, pattern-matching the
/// target site's typical slug conventions. That reliably produces plausible-
/// looking but wrong URLs (right domain, right style, wrong/nonexistent
/// path), which show up as a wall of 404s rather than a usable citation.
/// </summary>
public class WebSearchTool : ToolBase
{
    private static readonly HttpClient Http = SafeHttpClientFactory.Create(TimeSpan.FromSeconds(15));

    public override string Name => "web_search";
    public override string Description =>
        "Search the web and return a list of results (title, url, snippet). " +
        "Use this to find a real, current URL before calling web_fetch -- " +
        "never type a URL from memory/pattern-guessing (e.g. a plausible-" +
        "looking FDA or news-site slug), since that reliably produces " +
        "404s or the wrong page. Follow up on a promising result with " +
        "web_fetch to read its actual content.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string",
                description = "The search query" },
            max_results = new { type = "integer",
                description = "Max number of results to return. Default: 10, max: 20." }
        },
        required = new[] { "query" }
    });

    public override async Task<ToolResult> ExecuteAsync(
        JsonElement input, CancellationToken ct = default)
    {
        var query = GetRequiredString(input, "query");
        var maxResults = Math.Clamp(GetOptionalInt(input, "max_results", 10), 1, 20);

        var searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

        string html;
        try
        {
            using var response = await Http.GetAsync(searchUrl, ct);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Error("Request timed out (15s).");
        }

        var results = await ParseResultsAsync(html, ct);

        if (results.Count == 0)
            return ToolResult.Success(
                $"No results found for query: '{query}'. Try a different phrasing, or the " +
                "search provider may be rate-limiting -- do not fall back to a guessed URL.");

        var lines = new List<string> { $"Results for '{query}':", "" };
        var i = 0;
        foreach (var (title, url, snippet) in results.Take(maxResults))
        {
            i++;
            lines.Add($"{i}. {title}");
            lines.Add($"   {url}");
            if (!string.IsNullOrWhiteSpace(snippet))
                lines.Add($"   {snippet}");
            lines.Add("");
        }

        return ToolResult.Success(string.Join('\n', lines).TrimEnd());
    }

    internal static async Task<List<(string Title, string Url, string Snippet)>> ParseResultsAsync(
        string html, CancellationToken ct)
    {
        var browsingContext = BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = await browsingContext.OpenAsync(req => req.Content(html), ct);

        var results = new List<(string, string, string)>();
        foreach (var body in document.QuerySelectorAll(".result__body"))
        {
            var titleAnchor = body.QuerySelector("a.result__a");
            var href = titleAnchor?.GetAttribute("href");
            if (titleAnchor is null || string.IsNullOrWhiteSpace(href))
                continue;

            var url = ResolveRealUrl(href);
            if (url is null)
                continue;

            var title = WebUtility.HtmlDecode(titleAnchor.TextContent?.Trim() ?? "");
            var snippet = WebUtility.HtmlDecode(body.QuerySelector(".result__snippet")?.TextContent?.Trim() ?? "");
            results.Add((title, url, snippet));
        }

        return results;
    }

    /// <summary>
    /// DuckDuckGo's HTML endpoint wraps every result link in its own redirect
    /// (`//duckduckgo.com/l/?uddg=&lt;encoded target&gt;&amp;rut=...`) rather than
    /// linking directly to the target -- this unwraps that so callers get the
    /// real destination URL, not a duckduckgo.com link.
    /// </summary>
    internal static string? ResolveRealUrl(string href)
    {
        var uddgIndex = href.IndexOf("uddg=", StringComparison.Ordinal);
        if (uddgIndex >= 0)
        {
            var start = uddgIndex + "uddg=".Length;
            var end = href.IndexOf('&', start);
            var encoded = end >= 0 ? href[start..end] : href[start..];
            var decoded = WebUtility.UrlDecode(encoded);
            return Uri.TryCreate(decoded, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"
                ? decoded
                : null;
        }

        var normalized = href.StartsWith("//", StringComparison.Ordinal) ? "https:" + href : href;
        return Uri.TryCreate(normalized, UriKind.Absolute, out var direct) && direct.Scheme is "http" or "https"
            ? normalized
            : null;
    }
}
