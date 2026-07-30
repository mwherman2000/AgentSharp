using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AgentSharp.Tools.Implementations;

/// <summary>
/// Fetch the content of a URL. Returns the response body as text.
/// Useful for checking APIs, documentation, or web pages.
/// </summary>
public class WebFetchTool : ToolBase
{
    private static readonly HttpClient Http = new(CreateHandler())
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public override string Name => "web_fetch";
    public override string Description =>
        "Fetch the content of a URL. Returns the response body " +
        "as text. Useful for checking APIs, documentation, " +
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
                    "Default: 5000" }
        },
        required = new[] { "url" }
    });

    public override async Task<ToolResult> ExecuteAsync(
        JsonElement input, CancellationToken ct = default)
    {
        var url = GetRequiredString(input, "url");
        var maxLength = GetOptionalInt(input, "max_length", 5000);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            return ToolResult.Error($"Invalid URL: '{url}'. Only http/https URLs are supported.");

        try
        {
            var response = await Http.GetStringAsync(uri, ct);
            if (response.Length > maxLength)
                response = response[..maxLength] +
                    "\n\n[Truncated for display]";
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

    /// <summary>
    /// This tool is auto-approved (ReadOnly) since fetching public web pages has no
    /// side effects, but that also means nothing prompts the user before a request
    /// goes out -- so the SSRF protection has to live at the network layer instead
    /// of relying on approval. A ConnectCallback validates the actual IP address
    /// the client is about to connect to (not just the pre-resolved DNS answer),
    /// which also closes the DNS-rebinding gap a pre-flight-only check would leave:
    /// an attacker's DNS could return a public IP for a first lookup and a private
    /// one for the real connection if the check and the connect were separate steps.
    /// </summary>
    private static SocketsHttpHandler CreateHandler() => new()
    {
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            var address = Array.Find(addresses, a => !IsBlockedAddress(a))
                ?? throw new HttpRequestException(
                    $"Blocked: '{context.DnsEndPoint.Host}' does not resolve to a public address (SSRF protection).");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(address, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };

    /// <summary>
    /// Blocks loopback, private, link-local (including the 169.254.169.254 cloud
    /// metadata address used by AWS/GCP/Azure), and other reserved/special-use
    /// ranges so an auto-approved fetch can't be used for SSRF against internal
    /// infrastructure.
    /// </summary>
    internal static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] switch
            {
                0 => true,                                  // 0.0.0.0/8
                10 => true,                                  // 10.0.0.0/8
                127 => true,                                  // 127.0.0.0/8
                169 when b[1] == 254 => true,                 // 169.254.0.0/16 (link-local, incl. cloud metadata)
                172 when b[1] is >= 16 and <= 31 => true,      // 172.16.0.0/12
                192 when b[1] == 168 => true,                  // 192.168.0.0/16
                192 when b[1] == 0 && b[2] is 0 or 2 => true,  // 192.0.0.0/24, 192.0.2.0/24 (TEST-NET-1)
                198 when b[1] is 18 or 19 => true,             // 198.18.0.0/15 (benchmarking)
                198 when b[1] == 51 && b[2] == 100 => true,    // 198.51.100.0/24 (TEST-NET-2)
                203 when b[1] == 0 && b[2] == 113 => true,     // 203.0.113.0/24 (TEST-NET-3)
                100 when b[1] is >= 64 and <= 127 => true,     // 100.64.0.0/10 (carrier-grade NAT)
                >= 224 => true,                                // 224.0.0.0/4 multicast, 240.0.0.0/4 reserved
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                return true;

            // fc00::/7 -- unique local addresses (IPv6's equivalent of RFC 1918)
            var b = address.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC)
                return true;
        }

        return false;
    }
}
