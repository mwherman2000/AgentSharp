using System.Text.Json;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Fetch the content of a URL. Returns the response body as text.
/// Useful for checking APIs, documentation, or web pages.
/// </summary>
public class WebFetchTool : ToolBase
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public override string Name => "web_fetch";
    public override string Description =>
        "Fetch the content of a URL. Returns the response body " +
        "as text. Useful for checking APIs, documentation, " +
        "or web pages.";
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
                    "Default: 5000" }
        },
        required = new[] { "url" }
    });

    public override async Task<ToolResult> ExecuteAsync(
        JsonElement input, CancellationToken ct = default)
    {
        var url = GetRequiredString(input, "url");
        var maxLength = GetOptionalInt(input, "max_length", 5000);

        try
        {
            var response = await Http.GetStringAsync(url, ct);
            if (response.Length > maxLength)
                response = response[..maxLength] +
                    "\n\n[Truncated]";
            return ToolResult.Success(response);
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
}
