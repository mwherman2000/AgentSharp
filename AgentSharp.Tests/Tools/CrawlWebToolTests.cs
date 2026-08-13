using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class CrawlWebToolTests
{
    private static readonly Uri PostUri = new("https://hyperonomy.com/some-post-slug/");

    [Fact]
    public async Task ConvertToMarkdownAsync_ResolvesRelativeImageSrcToAbsolute()
    {
        var html = "<p>Hello</p><img src=\"/wp-content/uploads/photo.png\" alt=\"a photo\">";

        var (markdown, unresolved) = await CrawlWebTool.ConvertToMarkdownAsync(html, PostUri, default);

        Assert.Empty(unresolved);
        Assert.Contains("https://hyperonomy.com/wp-content/uploads/photo.png", markdown);
        Assert.Contains("Hello", markdown);
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_LeavesAlreadyAbsoluteImageSrcUnchanged()
    {
        var html = "<img src=\"https://cdn.example.com/pic.jpg\">";

        var (markdown, unresolved) = await CrawlWebTool.ConvertToMarkdownAsync(html, PostUri, default);

        Assert.Empty(unresolved);
        Assert.Contains("https://cdn.example.com/pic.jpg", markdown);
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_FallsBackToDataSrcWhenSrcIsMissing()
    {
        // No src attribute at all -- common for lazy-loaded WordPress images, where the
        // real URL only lives in data-src until JavaScript swaps it in client-side.
        var html = "<img data-src=\"/lazy.png\" alt=\"a lazy image\">";

        var (markdown, unresolved) = await CrawlWebTool.ConvertToMarkdownAsync(html, PostUri, default);

        Assert.Empty(unresolved);
        Assert.Contains("https://hyperonomy.com/lazy.png", markdown);
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_FlagsAndDropsImageWithNoResolvableSrc()
    {
        var html = "<p>Text before</p><img alt=\"broken\"><p>Text after</p>";

        var (markdown, unresolved) = await CrawlWebTool.ConvertToMarkdownAsync(html, PostUri, default);

        Assert.Single(unresolved);
        Assert.DoesNotContain("![broken]", markdown);
        Assert.Contains("Text before", markdown);
        Assert.Contains("Text after", markdown);
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_ConvertsBasicFormatting()
    {
        var html = "<p>Some <strong>bold</strong> and <em>italic</em> text.</p>";

        var (markdown, unresolved) = await CrawlWebTool.ConvertToMarkdownAsync(html, PostUri, default);

        Assert.Empty(unresolved);
        Assert.Contains("**bold**", markdown);
        Assert.Contains("*italic*", markdown);
    }

    [Fact]
    public void IsInvalidPageNumberError_TrueForWordPressPastLastPageResponse()
    {
        const string body = """
            {"code":"rest_post_invalid_page_number","message":"The page number requested is larger than the number of pages available.","data":{"status":400}}
            """;

        Assert.True(CrawlWebTool.IsInvalidPageNumberError(body));
    }

    [Theory]
    [InlineData("""{"code":"rest_forbidden","message":"Not allowed.","data":{"status":401}}""")]
    [InlineData("not json at all")]
    [InlineData("""{"code":null}""")]
    public void IsInvalidPageNumberError_FalseForOtherResponses(string body)
    {
        Assert.False(CrawlWebTool.IsInvalidPageNumberError(body));
    }
}
