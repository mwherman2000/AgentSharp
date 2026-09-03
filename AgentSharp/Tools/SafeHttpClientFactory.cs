using System.Net;
using System.Net.Sockets;

namespace AgentSharp.Tools;

/// <summary>
/// Builds HttpClients for tools that fetch arbitrary model-supplied URLs
/// (WebFetchTool, CrawlWebTool). Centralized so the SSRF protection only has
/// to be reviewed and gotten right once, rather than per tool.
/// </summary>
internal static class SafeHttpClientFactory
{
    /// <summary>Sent with every request AgentSharp makes -- both these tools' fetches
    /// and (via a direct reference to this constant) the LLM provider clients in
    /// AgentSharp.Llm -- since a bare HttpClient sends no User-Agent at all. For
    /// arbitrary web pages, many real-world sites (SEC EDGAR, which explicitly
    /// requires one per its fair-access policy, plus Wikipedia, G2, Crunchbase,
    /// Cloudflare-fronted sites, etc.) return 403 to requests with no/empty
    /// User-Agent rather than serving them, which otherwise reads as "page doesn't
    /// exist" instead of "request was blocked." For the LLM provider APIs the risk of
    /// outright blocking is much lower (authenticated, purpose-built endpoints, not
    /// arbitrary sites behind bot-detection), but identifying the client is still
    /// correct HTTP hygiene and useful for the provider's own rate-limiting/support
    /// diagnostics.</summary>
    internal const string UserAgent = "Mozilla/5.0 (compatible; AgentSharp/0.1; +https://github.com/mwherman2000/AgentSharp)";

    /// <summary>
    /// Creates an HttpClient whose ConnectCallback validates the actual IP
    /// address it's about to connect to (not just the pre-resolved DNS
    /// answer) before allowing the connection -- since these tools are
    /// ReadOnly-risk and auto-approved, nothing prompts the user before a
    /// request goes out, so SSRF protection has to live at the network layer.
    /// Checking at connect time (not just pre-flight) also closes the
    /// DNS-rebinding gap a separate check-then-connect would leave: an
    /// attacker's DNS could return a public IP for a first lookup and a
    /// private one for the real connection.
    /// </summary>
    public static HttpClient Create(TimeSpan timeout)
    {
        var client = new HttpClient(CreateHandler()) { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    private static SocketsHttpHandler CreateHandler() => new()
    {
        // A bare SocketsHttpHandler defaults to DecompressionMethods.None -- no
        // Accept-Encoding is sent and every fetched page arrives uncompressed, real
        // bandwidth/latency cost for HTML pages specifically, which compress well and
        // are already getting truncated at a fixed character budget (WebFetchTool/
        // CrawlWebTool's max_length) -- faster transfer means more of the real page
        // fits before that cutoff.
        AutomaticDecompression = DecompressionMethods.All,
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
