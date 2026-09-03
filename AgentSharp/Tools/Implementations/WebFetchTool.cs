using System.Text.Json;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Fetch the content of a URL. HTML responses are converted to clean Markdown text
/// (script/style/nav noise stripped, images resolved to absolute URLs) using the same
/// extraction path as CrawlWebTool's fetch_post -- an HTML page's raw markup is mostly
/// tag/script/style noise around a small amount of actual content, and every fetch's
/// full result is kept in conversation history for the rest of the session (nothing is
/// ever trimmed), so returning it raw wastes several times the context a fetch
/// actually needs to. Non-HTML responses (JSON APIs, XML/RSS feeds, plain text) are
/// already structured and readable, so those are returned unchanged -- converting them
/// would corrupt them.
/// </summary>
public class WebFetchTool : ToolBase
{
    private static readonly HttpClient Http = SafeHttpClientFactory.Create(TimeSpan.FromSeconds(15));

    public override string Name => "web_fetch";
    public override string Description =>
        "Fetch the content of a URL. HTML pages are converted to clean Markdown " +
        "text (images resolved to absolute URLs); JSON/XML/RSS/plain-text " +
        "responses are returned as-is. Useful for checking APIs, documentation, " +
        "or web pages. Requests to private/internal network addresses " +
        "are blocked.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            url = new { type = "string",
                description = "The URL to fetch" },
            max_length = new { type = "integer",
                description = "Max response length in characters. " +
                    "Default: 20000" }
        },
        required = new[] { "url" }
    });

    public override async Task<ToolResult> ExecuteAsync(
        JsonElement input, CancellationToken ct = default)
    {
        var url = GetRequiredString(input, "url");
        var maxLength = GetOptionalInt(input, "max_length", 20000);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            return ToolResult.Error($"Invalid URL: '{url}'. Only http/https URLs are supported.");

        try
        {
            using var response = await Http.GetAsync(uri, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            var mediaType = response.Content.Headers.ContentType?.MediaType;

            string result;
            List<string> unresolvedImages = [];
            if (LooksLikeHtml(mediaType, body))
                (result, unresolvedImages) = await CrawlWebTool.ConvertToMarkdownAsync(body, uri, ct);
            else
                result = body;

            if (result.Length > maxLength)
                result = result[..maxLength] + "\n\n[Truncated for display]";

            // Appended after truncation, never counted against max_length -- same
            // "never dropped silently" contract as CrawlWebTool's fetch_post.
            if (unresolvedImages.Count > 0)
            {
                result += "\n\n[UNRESOLVED IMAGES -- flag these, do not drop them silently]\n" +
                          string.Join('\n', unresolvedImages.Select(src => $"- {src}"));
            }

            return ToolResult.Success(result);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Error("Request timed out (15s).");
        }
    }

    /// <summary>
    /// Only HTML responses go through Markdown extraction -- JSON/XML/RSS/plain-text
    /// bodies (e.g. SEC EDGAR's data.sec.gov endpoints, RSS feeds, sitemaps) are
    /// already structured and directly readable by the model, and running them
    /// through an HTML-to-Markdown converter would corrupt them. Falls back to
    /// sniffing the body's own opening tag when the server sends no Content-Type
    /// (or a generic one like application/octet-stream), which is common on
    /// smaller or misconfigured sites.
    /// </summary>
    private static bool LooksLikeHtml(string? mediaType, string body)
    {
        if (mediaType is not null)
            return mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);

        var trimmed = body.TrimStart();
        return trimmed.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }
}
