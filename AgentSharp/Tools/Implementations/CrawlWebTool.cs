using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using AngleSharp;
using AngleSharp.Dom;
using ReverseMarkdown;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Crawls a WordPress blog to enumerate every post (paginated) and fetch a
/// single post's body converted to clean Markdown, with images resolved to
/// absolute URLs. Exists because a plain web_fetch of raw HTML leaves
/// pagination, link-following, and HTML-to-Markdown conversion entirely up
/// to the model -- unreliable even for a large model, and a near-guaranteed
/// source of fabricated ("hallucinated") content for a small one, since a
/// raw HTML page is usually too large and noisy (theme chrome, scripts,
/// nav, comments) for the model to extract a clean post body from.
///
/// Primary path: the WordPress REST API (`/wp-json/wp/v2/posts`). It returns
/// clean, structured JSON, its `content.rendered` field is already isolated
/// to just the post body (no site chrome to strip), and its
/// `page`/`per_page` pagination -- with `X-WP-TotalPages` reporting how many
/// pages exist -- means a single unified crawl of `/posts` already covers
/// every post regardless of which archive/category/tag page it would
/// otherwise appear under, no separate per-category/per-tag traversal
/// needed.
///
/// Fallback tier 2: the REST API is commonly blocked outright (security
/// plugins, server config) even on sites that are genuinely WordPress --
/// confirmed against a real site where `/wp-json/` 404s entirely. When that
/// happens, this falls back to the site's XML sitemap (`/wp-sitemap.xml` --
/// WordPress core, present by default since 5.5 regardless of plugins -- or
/// `/sitemap_index.xml` / `/sitemap.xml` for SEO-plugin-generated ones),
/// following a sitemap index down to its post sub-sitemaps. Checked ahead of
/// RSS because it's on a separate rewrite rule from `/wp-json/`, so it's
/// rarely covered by whatever blocked the REST API, and it gives a complete
/// URL count up front rather than an estimate discovered page-by-page.
/// Sitemaps carry URLs only, no content, so titles are unknown until
/// fetch_post fills them in, and fetch_post itself falls back further still
/// to fetching the post's own page directly when a URL was only ever
/// discovered this way.
///
/// Fallback tier 3: if no sitemap could be found either, this falls back to
/// the site's RSS feed (`/feed/`, paginated via `?paged=N`), which, like the
/// REST API, carries full post content per item (`content:encoded`) --
/// useful when it's the only structured source left standing.
///
/// Each tier's availability is probed once and remembered for the lifetime
/// of this tool instance, so later calls don't keep re-probing a route
/// that's already known to be dead.
/// </summary>
public class CrawlWebTool : ToolBase
{
    private static readonly HttpClient Http = SafeHttpClientFactory.Create(TimeSpan.FromSeconds(30));
    private static readonly XNamespace ContentNs = "http://purl.org/rss/1.0/modules/content/";

    /// <summary>Per-site probe/cache state. This tool is discovered once at startup and
    /// reused for the rest of the process's life -- across every site the user crawls in
    /// a session, and (since ToolRegistry entries are copied by reference into each
    /// sub-agent's isolated registry) across every concurrent sub-agent too. Keying this
    /// by base authority instead of holding the fields directly on the tool means
    /// crawling site B can never see site A's REST/RSS/sitemap probe results or cached
    /// posts -- a mixup here previously meant B could silently be served A's sitemap
    /// URLs, presented as B's own posts, with no error.</summary>
    private sealed class SiteState
    {
        /// <summary>null = not yet determined, true = confirmed reachable, false =
        /// confirmed missing (404) -- once false, callers skip straight to RSS.</summary>
        public bool? RestApiAvailable;

        /// <summary>Same tri-state as RestApiAvailable, but for the RSS feed -- only
        /// set false when the feed's own first page 404s, never for a later page (which
        /// just means normal end-of-pagination).</summary>
        public bool? RssAvailable;

        /// <summary>Populated as RSS feed pages are listed, keyed by post URL, so
        /// fetch_post can return a post's content instantly if list_posts already saw
        /// it, instead of re-fetching.</summary>
        public readonly ConcurrentDictionary<string, RssItem> RssItemCache = new();

        /// <summary>Full flattened sitemap URL list, fetched once and sliced in memory
        /// for each list_posts page -- sitemaps aren't paginated per-request the way
        /// REST/RSS are. Null until first attempted; set to an empty list after a
        /// confirmed failed attempt, so it's only ever fetched once per site.</summary>
        public List<(string Url, string? LastMod)>? SitemapEntries;
    }

    private readonly ConcurrentDictionary<string, SiteState> _sites = new(StringComparer.OrdinalIgnoreCase);

    private SiteState GetSiteState(string baseAuthority) => _sites.GetOrAdd(baseAuthority, static _ => new SiteState());

    public override string Name => "crawl_web";
    public override string Description =>
        "Enumerate and fetch posts from a WordPress blog -- the reliable " +
        "way to crawl an entire blog archive, instead of guessing archive/" +
        "category/tag URLs and parsing raw HTML with web_fetch. Uses the " +
        "WordPress REST API when available, and falls back automatically " +
        "(nothing to configure) to the site's XML sitemap, then its RSS " +
        "feed, then a direct page fetch if needed. Two actions:\n" +
        "- 'list_posts': returns one page of posts (title, date, URL) for " +
        "the given base_url, plus 'has_more'. To crawl the WHOLE archive, " +
        "call this repeatedly with an increasing 'page' until 'has_more' " +
        "is false -- never stop after the first page or a fixed count.\n" +
        "- 'fetch_post': given one post's URL (from list_posts), returns " +
        "its title, date, and full body converted to Markdown, with " +
        "images as Markdown image references pointing at absolute URLs. " +
        "Call this once per post you need the full content of.\n" +
        "If this tool reports an error, report it and stop -- never invent " +
        "placeholder posts or content to fill the gap. Requests to " +
        "private/internal network addresses are blocked.";
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
                description = "Posts per page for list_posts. Default: 100 (WordPress's own maximum) -- fewer round trips is better for exhaustive crawling. Ignored when falling back to the RSS feed, which controls its own page size."
            },
            url = new
            {
                type = "string",
                description = "Full post URL to fetch. Required for fetch_post."
            },
            max_length = new
            {
                type = "integer",
                description = "Max converted-markdown length in characters for fetch_post, matching web_fetch. Default: 20000."
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
        var baseAuthority = baseUri.GetLeftPart(UriPartial.Authority);
        // 100 is WordPress's own hard ceiling for REST per_page -- requesting more
        // returns a 400 (rest_post_invalid_per_page), so clamping avoids that round
        // trip. Also reused as the slice size for the sitemap fallback tier.
        var perPage = Math.Clamp(GetOptionalInt(input, "per_page", 100), 1, 100);
        var site = GetSiteState(baseAuthority);

        if (site.RestApiAvailable == false)
            return await ListPostsViaSitemapOrRssAsync(site, baseAuthority, page, perPage, ct);

        var listUrl = $"{baseAuthority}/wp-json/wp/v2/posts" +
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

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            site.RestApiAvailable = false;
            return await ListPostsViaSitemapOrRssAsync(site, baseAuthority, page, perPage, ct);
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
                $"WordPress REST API request failed ({(int)response.StatusCode} {response.ReasonPhrase}) at '{listUrl}'.");
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
            hasMore
                ? totalPages is not null
                    ? $"has_more: true -- pages {page + 1}–{totalPages} remain, call list_posts with page={page + 1} to continue"
                    : $"has_more: true -- call list_posts with page={page + 1} to continue"
                : "has_more: false",
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

    /// <summary>
    /// REST API is already known unavailable by the time this is called. Tries the
    /// sitemap next -- ahead of RSS, since sitemaps are typically unaffected by
    /// whatever blocked /wp-json/ (a separate rewrite rule, rarely covered by the
    /// same security-plugin rule) and give a complete URL count up front rather
    /// than an estimate discovered page-by-page -- falling back to the RSS feed
    /// only if no sitemap could be found at all.
    /// </summary>
    private async Task<ToolResult> ListPostsViaSitemapOrRssAsync(
        SiteState site, string baseAuthority, int page, int perPage, CancellationToken ct)
    {
        site.SitemapEntries ??= await LoadSitemapAsync(baseAuthority, ct) ?? [];

        if (site.SitemapEntries.Count > 0)
            return ListPostsFromSitemapCache(site.SitemapEntries, page, perPage);

        // No sitemap found -- fall back to the RSS feed.
        if (site.RssAvailable != false)
        {
            var (ok, isNotFound, error, items) = await FetchRssPageAsync(baseAuthority, page, ct);

            if (!isNotFound)
            {
                if (!ok)
                    return ToolResult.Error(error!);

                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.Link))
                        site.RssItemCache[item.Link] = item;
                }

                var lines = new List<string>
                {
                    $"Page {page} ({items.Count} posts) [via RSS feed -- REST API and sitemap both unavailable]",
                    // A nonzero page only means THIS page had content, not that another
                    // page exists -- but that's the best signal available without an
                    // extra round trip, so err toward "keep going" and let the true end
                    // show up as the next page's 404 (one extra call at the end, always
                    // correct).
                    items.Count > 0
                        ? $"has_more: true -- call list_posts with page={page + 1} to continue"
                        : "has_more: false",
                    ""
                };

                var i = 0;
                foreach (var item in items)
                {
                    i++;
                    lines.Add($"{i}. {item.Title} — {FormatRssDate(item.PubDate)} — {item.Link}");
                }

                if (items.Count == 0)
                    lines.Add("(no posts on this page)");

                return ToolResult.Success(string.Join('\n', lines));
            }

            if (page > 1)
                // Just the end of RSS pagination, not "RSS is unavailable" -- normal finish.
                return ToolResult.Success($"Page {page}: no posts (past the last page). has_more: false");

            // Page 1 itself 404'd -- no RSS feed at all.
            site.RssAvailable = false;
        }

        return ToolResult.Error(
            $"No WordPress REST API, sitemap, or RSS feed could be found at {baseAuthority}. Cannot enumerate posts.");
    }

    private static ToolResult ListPostsFromSitemapCache(List<(string Url, string? LastMod)> entries, int page, int perPage)
    {
        var pageItems = entries.Skip((page - 1) * perPage).Take(perPage).ToList();
        var hasMore = page * perPage < entries.Count;
        var remaining = entries.Count - page * perPage;
        var totalPageCount = (entries.Count + perPage - 1) / perPage;

        var lines = new List<string>
        {
            $"Page {page} ({entries.Count} URLs total) [via sitemap -- REST API unavailable; " +
            "titles unknown here, fetch_post will fill them in]",
            hasMore
                ? $"has_more: true -- {remaining} URLs remain (pages {page + 1}–{totalPageCount}), call list_posts with page={page + 1} to continue"
                : "has_more: false",
            ""
        };

        var i = (page - 1) * perPage;
        foreach (var (url, lastMod) in pageItems)
        {
            i++;
            lines.Add($"{i}. (title unknown) — {FormatRssDate(lastMod)} — {url}");
        }

        if (pageItems.Count == 0)
            lines.Add("(no more URLs)");

        return ToolResult.Success(string.Join('\n', lines));
    }

    private async Task<ToolResult> FetchPostAsync(JsonElement input, CancellationToken ct)
    {
        var url = GetOptionalString(input, "url");
        if (string.IsNullOrWhiteSpace(url))
            return ToolResult.Error("fetch_post requires 'url' (a post URL from list_posts).");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var postUri) || postUri.Scheme is not ("http" or "https"))
            return ToolResult.Error($"Invalid url: '{url}'. Only http/https URLs are supported.");

        var maxLength = GetOptionalInt(input, "max_length", 20000);
        var baseAuthority = postUri.GetLeftPart(UriPartial.Authority);
        var site = GetSiteState(baseAuthority);

        if (site.RestApiAvailable != false)
        {
            var restResult = await FetchPostViaRestAsync(postUri, baseAuthority, url, maxLength, ct);
            if (restResult is not null)
                return restResult;

            // null means the REST route itself is missing (404) -- fall back to RSS.
            site.RestApiAvailable = false;
        }

        return await FetchPostViaRssAsync(site, postUri, baseAuthority, url, maxLength, ct);
    }

    /// <summary>
    /// Returns null specifically when the REST API route doesn't exist (404),
    /// signaling the caller to fall back to RSS. Any other outcome -- success or a
    /// real error (bad JSON, no matching slug, network failure) -- is returned
    /// directly, since those aren't "try RSS instead" situations.
    /// </summary>
    private async Task<ToolResult?> FetchPostViaRestAsync(
        Uri postUri, string baseAuthority, string url, int maxLength, CancellationToken ct)
    {
        var slug = postUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (slug is null)
            return ToolResult.Error($"Could not derive a post slug from '{url}'.");
        slug = Uri.UnescapeDataString(slug);

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

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

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

        return await BuildFetchResultAsync(title, link, dateOnly, contentHtml, postUri, maxLength, ct);
    }

    private async Task<ToolResult> FetchPostViaRssAsync(
        SiteState site, Uri postUri, string baseAuthority, string url, int maxLength, CancellationToken ct)
    {
        if (site.RssItemCache.TryGetValue(url, out var cached))
            return await BuildFetchResultAsync(
                cached.Title, cached.Link, FormatRssDate(cached.PubDate), cached.ContentHtml, postUri, maxLength, ct);

        // Not seen via a prior list_posts call yet -- search the feed for it (unless
        // it's already confirmed unavailable), bounded so a URL that doesn't belong to
        // this site (or a typo) can't loop forever.
        if (site.RssAvailable != false)
        {
            const int maxPagesToSearch = 50;
            for (var page = 1; page <= maxPagesToSearch; page++)
            {
                var (ok, isNotFound, error, items) = await FetchRssPageAsync(baseAuthority, page, ct);
                if (isNotFound)
                {
                    if (page == 1) site.RssAvailable = false;
                    break; // ran off the end of the feed (or it doesn't exist) without finding it
                }
                if (!ok) return ToolResult.Error(error!);

                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.Link))
                        site.RssItemCache[item.Link] = item;
                }

                if (site.RssItemCache.TryGetValue(url, out var found))
                    return await BuildFetchResultAsync(
                        found.Title, found.Link, FormatRssDate(found.PubDate), found.ContentHtml, postUri, maxLength, ct);
            }
        }

        // Neither REST nor RSS have this post (or either/both are unavailable at all) --
        // last resort: fetch the page itself and extract its content heuristically.
        return await FetchPostViaRawHtmlAsync(postUri, url, maxLength, ct);
    }

    /// <summary>
    /// Last-resort fallback: fetches the post's own page directly and guesses at
    /// its content container using common WordPress theme selectors, since there's
    /// no structured API left to ask. Lower quality than the REST/RSS paths (no
    /// guarantee the isolated block is exactly the post body, no site chrome
    /// leaking in) -- the result says so explicitly rather than presenting it with
    /// the same confidence as a structured fetch.
    /// </summary>
    private static async Task<ToolResult> FetchPostViaRawHtmlAsync(
        Uri postUri, string url, int maxLength, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await Http.GetAsync(postUri, ct);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error($"Could not reach '{postUri.Host}': {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Error("Request timed out (30s).");
        }

        if (!response.IsSuccessStatusCode)
            return ToolResult.Error(
                $"No REST API entry, RSS entry, or reachable page found for '{url}' " +
                $"({(int)response.StatusCode} {response.ReasonPhrase} on direct fetch).");

        var html = await response.Content.ReadAsStringAsync(ct);

        var browsingContext = BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = await browsingContext.OpenAsync(req => req.Content(html), ct);

        var title = WebUtility.HtmlDecode(
            document.QuerySelector("h1.entry-title")?.TextContent?.Trim()
            ?? document.QuerySelector("h1")?.TextContent?.Trim()
            ?? document.Title
            ?? "(title unknown)");

        // Common WordPress theme content-container selectors, most specific first.
        string[] contentSelectors = ["article .entry-content", ".entry-content", ".post-content", "article", "main", "#content"];
        var contentHtml = contentSelectors
            .Select(sel => document.QuerySelector(sel)?.InnerHtml)
            .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h))
            ?? document.Body?.InnerHtml ?? html;

        var result = await BuildFetchResultAsync(title, url, "unknown date", contentHtml, postUri, maxLength, ct);
        return result with
        {
            Output = result.Output + "\n\n[Fetched via direct page HTML with best-effort content extraction -- " +
                     "no REST API or RSS entry was available for this URL. The body above may include leftover " +
                     "page chrome, or be missing content the heuristic didn't capture -- verify before relying on it.]"
        };
    }

    /// <summary>
    /// Fetches and parses one page of the RSS feed. Returns isNotFound=true for a 404
    /// (WordPress's signal for "past the last page" on paginated feed requests, same
    /// role as the REST API's rest_post_invalid_page_number) -- the caller decides
    /// whether that means "no feed at all" (page 1) or "end of pagination" (page > 1).
    /// </summary>
    private static async Task<(bool ok, bool isNotFound, string? error, List<RssItem> items)> FetchRssPageAsync(
        string baseAuthority, int page, CancellationToken ct)
    {
        var feedUrl = page <= 1 ? $"{baseAuthority}/feed/" : $"{baseAuthority}/feed/?paged={page}";

        HttpResponseMessage response;
        try
        {
            response = await Http.GetAsync(feedUrl, ct);
        }
        catch (HttpRequestException ex)
        {
            return (false, false, $"Could not reach '{new Uri(baseAuthority).Host}': {ex.Message}", []);
        }
        catch (TaskCanceledException)
        {
            return (false, false, "Request timed out (30s).", []);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return (false, true, null, []);

        if (!response.IsSuccessStatusCode)
            return (false, false, $"RSS feed request failed ({(int)response.StatusCode} {response.ReasonPhrase}) at '{feedUrl}'.", []);

        var xml = await response.Content.ReadAsStringAsync(ct);

        List<RssItem> items;
        try
        {
            var doc = XDocument.Parse(xml);
            items = doc.Descendants("item").Select(item => new RssItem(
                WebUtility.HtmlDecode(item.Element("title")?.Value ?? ""),
                item.Element("link")?.Value ?? "",
                item.Element("pubDate")?.Value,
                item.Element(ContentNs + "encoded")?.Value ?? item.Element("description")?.Value ?? ""
            )).ToList();
        }
        catch (System.Xml.XmlException)
        {
            return (false, false, $"'{feedUrl}' did not return a valid RSS feed.", []);
        }

        return (true, false, null, items);
    }

    /// <summary>
    /// Tries WordPress's built-in sitemap first (present since core 5.5, independent
    /// of any plugin, and typically not covered by whatever blocked /wp-json/ since
    /// it's a separate rewrite rule), then common SEO-plugin sitemap paths. Follows
    /// a sitemap index down into its sub-sitemaps and flattens everything into one
    /// list, keeping only sub-sitemaps that look like they hold posts (skips pages,
    /// categories, tags, authors, media) so paginated results aren't diluted with
    /// non-post URLs. Returns null only when no candidate path yielded anything.
    /// </summary>
    private static async Task<List<(string Url, string? LastMod)>?> LoadSitemapAsync(string baseAuthority, CancellationToken ct)
    {
        foreach (var path in new[] { "/wp-sitemap.xml", "/sitemap_index.xml", "/sitemap.xml" })
        {
            var rootDoc = await TryFetchXmlAsync($"{baseAuthority}{path}", ct);
            if (rootDoc?.Root is null)
                continue;

            var rootName = rootDoc.Root.Name.LocalName;

            if (rootName == "urlset")
            {
                var entries = ExtractUrlEntries(rootDoc);
                if (entries.Count > 0)
                    return entries;
                continue;
            }

            if (rootName != "sitemapindex")
                continue;

            var subSitemapUrls = rootDoc.Root.Elements()
                .Where(e => e.Name.LocalName == "sitemap")
                .Select(e => e.Elements().FirstOrDefault(c => c.Name.LocalName == "loc")?.Value)
                .Where(u => !string.IsNullOrEmpty(u) && LooksLikePostSitemap(u!))
                .ToList();

            var allEntries = new List<(string, string?)>();
            foreach (var subUrl in subSitemapUrls)
            {
                var subDoc = await TryFetchXmlAsync(subUrl!, ct);
                if (subDoc?.Root?.Name.LocalName == "urlset")
                    allEntries.AddRange(ExtractUrlEntries(subDoc));
            }

            if (allEntries.Count > 0)
                return allEntries;
        }

        return null;
    }

    private static async Task<XDocument?> TryFetchXmlAsync(string url, CancellationToken ct)
    {
        try
        {
            var response = await Http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var xml = await response.Content.ReadAsStringAsync(ct);
            return XDocument.Parse(xml);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException)
        {
            // Any of these just means "this candidate path didn't pan out" -- the
            // caller tries the next one, so there's nothing more useful to do with
            // the specific failure reason here.
            return null;
        }
    }

    private static List<(string Url, string? LastMod)> ExtractUrlEntries(XDocument doc) =>
        doc.Root!.Elements()
            .Where(e => e.Name.LocalName == "url")
            .Select(e => (
                Url: e.Elements().FirstOrDefault(c => c.Name.LocalName == "loc")?.Value ?? "",
                LastMod: e.Elements().FirstOrDefault(c => c.Name.LocalName == "lastmod")?.Value
            ))
            .Where(e => e.Url.Length > 0)
            .ToList();

    /// <summary>
    /// Matches WordPress core's naming (wp-sitemap-posts-post-1.xml) and common SEO
    /// plugins' (post-sitemap1.xml), while excluding sibling sub-sitemaps for pages,
    /// categories, tags, authors, and media that a sitemap index typically also lists.
    /// </summary>
    private static bool LooksLikePostSitemap(string sitemapUrl)
    {
        var lower = sitemapUrl.ToLowerInvariant();
        return lower.Contains("post") &&
               !lower.Contains("page") && !lower.Contains("categor") && !lower.Contains("tag") &&
               !lower.Contains("author") && !lower.Contains("media") && !lower.Contains("attachment");
    }

    private static string FormatRssDate(string? pubDateRaw)
    {
        if (pubDateRaw is not null &&
            DateTimeOffset.TryParse(pubDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return dto.ToString("yyyy-MM-dd");
        return pubDateRaw ?? "unknown date";
    }

    /// <summary>
    /// Shared by both the REST and RSS fetch paths: resolves images, converts to
    /// Markdown, applies max_length, and appends the unresolved-images warning.
    /// </summary>
    private static async Task<ToolResult> BuildFetchResultAsync(
        string title, string link, string dateOnly, string contentHtml, Uri postUri, int maxLength, CancellationToken ct)
    {
        var (markdown, unresolvedImages) = await ConvertToMarkdownAsync(contentHtml, postUri, ct);

        var result = $"Title: {title}\nURL: {link}\nDate: {dateOnly}\n\n{markdown}";
        if (result.Length > maxLength)
            result = result[..maxLength] + "\n\n[Truncated for display]";

        // Appended after truncation, never counted against max_length -- silently
        // truncating this away would defeat the "never dropped silently" requirement
        // on unresolved images right when a long post makes it most likely to fire.
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

        // Never content, regardless of site: analytics/tracking scripts, style rules,
        // noscript fallbacks, embedded frames (tracking pixels are almost always
        // 0x0 iframes), inline SVG icons, and stray head elements a malformed page
        // left in the body. The converter's PassThrough setting below means an
        // unstripped tag like this survives as literal raw HTML in the Markdown
        // output instead of being dropped -- fine for an occasional real embed
        // inside isolated post content, actively harmful when converting a whole
        // arbitrary page body (web_fetch) that's mostly this kind of noise.
        foreach (var noise in document.QuerySelectorAll("script, style, noscript, iframe, svg, link, meta"))
            noise.Remove();

        // Presentational only, never content -- but worth stripping explicitly rather
        // than leaving it for PassThrough to preserve, since an inline style is a
        // second (and easy to miss) place a data: URI shows up, e.g. a CSS
        // mask-image/background-image icon on an otherwise plain element like a
        // <button>, carrying the same base64-bloat problem as an <img src="data:...">.
        foreach (var styled in document.QuerySelectorAll("[style]"))
            styled.RemoveAttribute("style");

        foreach (var img in document.QuerySelectorAll("img"))
        {
            var src = FirstNonEmpty(img.GetAttribute("src"), img.GetAttribute("data-src"), img.GetAttribute("data-lazy-src"));

            if (string.IsNullOrWhiteSpace(src))
            {
                unresolvedImages.Add("(no src attribute)");
                img.Remove();
                continue;
            }

            // A data: URI's payload IS its src -- often tens of thousands of base64
            // characters for a single inline image. Reporting a short placeholder
            // instead of the literal src (as the other unresolved cases do below)
            // avoids embedding that payload in the unresolved-images list, which
            // would be far more wasteful than the noise this list exists to flag.
            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                unresolvedImages.Add($"(inline data: image, {src.Length} chars, omitted)");
                img.Remove();
                continue;
            }

            // Anything that isn't http(s) after resolution (e.g. javascript:, or a
            // data: URI that slipped past the check above via data-src) isn't a
            // stable, fetchable reference either.
            if (!Uri.TryCreate(postUri, src, out var absolute) || absolute.Scheme is not ("http" or "https"))
            {
                unresolvedImages.Add(src);
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

        // Tried preferring the page's first <article>/<main> element here as a
        // generic way to skip nav/header/footer chrome without a per-site selector.
        // Reverted: a real-world page can carry several such elements (e.g. a
        // "related content" or promo widget also marked up as <article>), and
        // QuerySelector silently returns whichever comes first in document order --
        // on a real site this picked a sidebar promo instead of the actual article,
        // with no error or signal that anything had gone wrong. Confidently wrong
        // beats noisy-but-complete for a tool whose whole job is evidence gathering,
        // so this falls back to the full body like FetchPostViaRawHtmlAsync's own
        // best-effort fallback already does -- noisier, but not silently mistaken.
        var markdown = converter.Convert(document.Body?.InnerHtml ?? html);
        return (markdown.Trim(), unresolvedImages);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// WordPress reports "page number beyond the last page" as HTTP 400 with
    /// code "rest_post_invalid_page_number" -- distinguishes that from a real
    /// failure (auth, rate limit, etc.). A missing/disabled REST API route is a
    /// plain 404, handled separately by the caller as a signal to fall back to RSS.
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

    private sealed record RssItem(string Title, string Link, string? PubDate, string ContentHtml);
}
