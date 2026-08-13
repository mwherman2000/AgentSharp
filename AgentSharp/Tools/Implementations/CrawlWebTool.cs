using System.Net;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using ReverseMarkdown;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Crawls a WordPress blog's REST API to enumerate every post (paginated) and
/// fetch a single post's body converted to clean Markdown, with images
/// resolved to absolute URLs. Exists because a plain web_fetch of raw HTML
/// leaves pagination, link-following, and HTML-to-Markdown conversion
/// entirely up to the model -- unreliable even for a large model, and a
/// near-guaranteed source of fabricated ("hallucinated") content for a small
/// one, since a raw HTML page is usually too large and noisy (theme chrome,
/// scripts, nav, comments) for the model to extract a clean post body from.
///
/// The WordPress REST API (`/wp-json/wp/v2/posts`) sidesteps all of that: it
/// returns clean, structured JSON, its `content.rendered` field is already
/// isolated to just the post body (no site chrome to strip), and its
/// `page`/`per_page` pagination -- with `X-WP-TotalPages` reporting how many
/// pages exist -- means a single unified crawl of `/posts` already covers
/// every post regardless of which archive/category/tag page it would
/// otherwise appear under, no separate per-category/per-tag traversal
/// needed.
/// </summary>
public class CrawlWebTool : ToolBase
{
    private static readonly HttpClient Http = SafeHttpClientFactory.Create(TimeSpan.FromSeconds(30));

    public override string Name => "crawl_web";
    public override string Description =>
        "Enumerate and fetch posts from a WordPress blog via its REST API " +
        "(/wp-json/wp/v2/posts) -- the reliable way to crawl an entire blog " +
        "archive, instead of guessing archive/category/tag URLs and " +
        "parsing raw HTML with web_fetch. Two actions:\n" +
        "- 'list_posts': returns one page of posts (title, date, URL) for " +
        "the given base_url, plus 'has_more'. To crawl the WHOLE archive, " +
        "call this repeatedly with an increasing 'page' until 'has_more' " +
        "is false -- never stop after the first page or a fixed count.\n" +
        "- 'fetch_post': given one post's URL (from list_posts), returns " +
        "its title, date, and full body converted to Markdown, with " +
        "images as Markdown image references pointing at absolute URLs. " +
        "Call this once per post you need the full content of.\n" +
        "Requests to private/internal network addresses are blocked.";
    public override ToolRiskLevel RiskLevel => ToolRiskLevel.ReadOnly;

    protected override JsonElement BuildInputSchema() => SchemaFrom(new
    {
        type = "object",
        properties = new
        {
            action = new
            {
                type = "string",
                @enum = new[] { "list_posts", "fetch_post" },
                description = "'list_posts' to enumerate a page of posts, 'fetch_post' to fetch one post's full body"
            },
            base_url = new
            {
                type = "string",
                description = "Site base URL, e.g. https://hyperonomy.com. Required for list_posts."
            },
            page = new
            {
                type = "integer",
                description = "1-based page number for list_posts. Default: 1."
            },
            per_page = new
            {
                type = "integer",
                description = "Posts per page for list_posts. Default: 20, max: 100."
            },
            url = new
            {
                type = "string",
                description = "Full post URL to fetch. Required for fetch_post."
            }
        },
        required = new[] { "action" }
    });

    public override async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var action = GetRequiredString(input, "action");

        return action switch
        {
            "list_posts" => await ListPostsAsync(input, ct),
            "fetch_post" => await FetchPostAsync(input, ct),
            _ => ToolResult.Error($"Unknown action: '{action}'. Expected 'list_posts' or 'fetch_post'.")
        };
    }

    private async Task<ToolResult> ListPostsAsync(JsonElement input, CancellationToken ct)
    {
        var baseUrl = GetOptionalString(input, "base_url");
        if (string.IsNullOrWhiteSpace(baseUrl))
            return ToolResult.Error("list_posts requires 'base_url', e.g. https://hyperonomy.com");

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
            return ToolResult.Error($"Invalid base_url: '{baseUrl}'. Only http/https URLs are supported.");

        var page = GetOptionalInt(input, "page", 1);
        var perPage = Math.Clamp(GetOptionalInt(input, "per_page", 20), 1, 100);

        var listUrl = $"{baseUri.GetLeftPart(UriPartial.Authority)}/wp-json/wp/v2/posts" +
                      $"?page={page}&per_page={perPage}&_fields=id,link,title,date&orderby=date&order=asc";

        HttpResponseMessage response;
        try
        {
            response = await Http.GetAsync(listUrl, ct);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error($"Could not reach '{baseUri.Host}': {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Error("Request timed out (30s).");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // A page number past the end is WordPress's own "you're done paginating"
            // signal (rest_post_invalid_page_number), not a real failure -- surface it
            // as an ordinary end-of-list result so the model's pagination loop can just
            // check has_more instead of needing to special-case an error response.
            if (IsInvalidPageNumberError(body))
                return ToolResult.Success($"Page {page}: no posts (past the last page). has_more: false");

            return ToolResult.Error(
                $"WordPress REST API request failed ({(int)response.StatusCode} {response.ReasonPhrase}) " +
                $"at '{listUrl}'. This tool requires the standard WP REST API to be enabled at " +
                $"{baseUri.GetLeftPart(UriPartial.Authority)}/wp-json/wp/v2/posts.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return ToolResult.Error(
                $"'{listUrl}' did not return valid JSON -- this site may not expose the WordPress REST API.");
        }

        var totalPages = response.Headers.TryGetValues("X-WP-TotalPages", out var tpValues) &&
                          int.TryParse(tpValues.FirstOrDefault(), out var tp) ? tp : (int?)null;
        var totalPosts = response.Headers.TryGetValues("X-WP-Total", out var tValues) &&
                          int.TryParse(tValues.FirstOrDefault(), out var t) ? t : (int?)null;

        var posts = doc.RootElement.EnumerateArray().ToList();
        var hasMore = totalPages is { } tpVal ? page < tpVal : posts.Count == perPage;

        var lines = new List<string>
        {
            totalPages is not null
                ? $"Page {page} of {totalPages} ({totalPosts} posts total)"
                : $"Page {page} ({posts.Count} posts)",
            $"has_more: {(hasMore ? "true" : "false")}",
            ""
        };

        var i = 0;
        foreach (var post in posts)
        {
            i++;
            var title = WebUtility.HtmlDecode(post.GetProperty("title").GetProperty("rendered").GetString() ?? "");
            var link = post.GetProperty("link").GetString() ?? "";
            var date = post.TryGetProperty("date", out var dateEl) ? dateEl.GetString() : null;
            var dateOnly = date is { Length: >= 10 } ? date[..10] : date ?? "unknown date";
            lines.Add($"{i}. {title} — {dateOnly} — {link}");
        }

        if (posts.Count == 0)
            lines.Add("(no posts on this page)");

        return ToolResult.Success(string.Join('\n', lines));
    }

    private async Task<ToolResult> FetchPostAsync(JsonElement input, CancellationToken ct)
    {
        var url = GetOptionalString(input, "url");
        if (string.IsNullOrWhiteSpace(url))
            return ToolResult.Error("fetch_post requires 'url' (a post URL from list_posts).");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var postUri) || postUri.Scheme is not ("http" or "https"))
            return ToolResult.Error($"Invalid url: '{url}'. Only http/https URLs are supported.");

        var slug = postUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (slug is null)
            return ToolResult.Error($"Could not derive a post slug from '{url}'.");
        slug = Uri.UnescapeDataString(slug);

        var baseAuthority = postUri.GetLeftPart(UriPartial.Authority);
        var lookupUrl = $"{baseAuthority}/wp-json/wp/v2/posts" +
                         $"?slug={Uri.EscapeDataString(slug)}&_fields=link,title,date,content";

        HttpResponseMessage response;
        try
        {
            response = await Http.GetAsync(lookupUrl, ct);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error($"Could not reach '{postUri.Host}': {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Error("Request timed out (30s).");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return ToolResult.Error(
                $"WordPress REST API request failed ({(int)response.StatusCode} {response.ReasonPhrase}) at '{lookupUrl}'.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return ToolResult.Error(
                $"'{lookupUrl}' did not return valid JSON -- this site may not expose the WordPress REST API.");
        }

        var matches = doc.RootElement.EnumerateArray().ToList();
        if (matches.Count == 0)
            return ToolResult.Error($"No post found with slug '{slug}' at {baseAuthority}.");

        var post = matches[0];
        var title = WebUtility.HtmlDecode(post.GetProperty("title").GetProperty("rendered").GetString() ?? "");
        var link = post.TryGetProperty("link", out var linkEl) ? linkEl.GetString() ?? url : url;
        var date = post.TryGetProperty("date", out var dateEl) ? dateEl.GetString() : null;
        var dateOnly = date is { Length: >= 10 } ? date[..10] : date ?? "unknown date";
        var contentHtml = post.GetProperty("content").GetProperty("rendered").GetString() ?? "";

        var (markdown, unresolvedImages) = await ConvertToMarkdownAsync(contentHtml, postUri, ct);

        var result = $"Title: {title}\nURL: {link}\nDate: {dateOnly}\n\n{markdown}";
        if (unresolvedImages.Count > 0)
        {
            result += "\n\n[UNRESOLVED IMAGES -- flag these, do not drop them silently]\n" +
                      string.Join('\n', unresolvedImages.Select(src => $"- {src}"));
        }

        return ToolResult.Success(result);
    }

    /// <summary>
    /// Resolves every &lt;img&gt; src (falling back to data-src/data-lazy-src for
    /// lazy-loaded images) to an absolute URL against the post's own URL, then
    /// converts the resulting HTML to Markdown. Images whose src can't be resolved
    /// to a stable absolute URL are left out of the Markdown body and reported
    /// back separately, per the "never dropped silently" requirement.
    /// </summary>
    internal static async Task<(string markdown, List<string> unresolvedImages)> ConvertToMarkdownAsync(
        string html, Uri postUri, CancellationToken ct)
    {
        var unresolvedImages = new List<string>();

        var browsingContext = BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = await browsingContext.OpenAsync(req => req.Content(html), ct);

        foreach (var img in document.QuerySelectorAll("img"))
        {
            var src = FirstNonEmpty(img.GetAttribute("src"), img.GetAttribute("data-src"), img.GetAttribute("data-lazy-src"));

            if (string.IsNullOrWhiteSpace(src) || !Uri.TryCreate(postUri, src, out var absolute))
            {
                unresolvedImages.Add(src ?? "(no src attribute)");
                img.Remove();
                continue;
            }

            img.SetAttribute("src", absolute.ToString());
        }

        var converter = new Converter(new Config
        {
            GithubFlavored = true,
            Tags = { Unknown = Config.UnknownTagsOption.PassThrough },
            Formatting = { RemoveComments = true },
            Links = { SmartHref = true }
        });

        var markdown = converter.Convert(document.Body?.InnerHtml ?? html);
        return (markdown.Trim(), unresolvedImages);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// WordPress reports "page number beyond the last page" as HTTP 400 with
    /// code "rest_post_invalid_page_number" -- distinguishes that from a real
    /// failure (auth, rate limit, REST API disabled, etc.).
    /// </summary>
    internal static bool IsInvalidPageNumberError(string responseBody)
    {
        try
        {
            var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.TryGetProperty("code", out var code) &&
                   code.GetString() == "rest_post_invalid_page_number";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
