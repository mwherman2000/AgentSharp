using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AgentSharpLib.Network;

/// <summary>
/// What a connectivity check found. <see cref="Link"/> is the kind of network the machine is on ("Wi-Fi",
/// "Ethernet", ...), or null when none is connected.
/// </summary>
public sealed record NetworkStatus(string? Link, string Host, int Port, bool DnsOk, bool TcpOk, string? Failure)
{
    /// <summary>True if a connection to the host was opened just now.</summary>
    public bool Reachable => TcpOk;

    /// <summary>One line for the console, for example "Wi-Fi up; DNS ok; api.anthropic.com:443 timed out".</summary>
    public string Describe()
    {
        if (Link is null)
            return "no network connection is up (no adapter has a gateway)";

        var parts = new List<string> { $"{Link} up" };
        if (!DnsOk)
        {
            parts.Add($"DNS lookup of {Host} failed");
        }
        else
        {
            parts.Add("DNS ok");
            parts.Add(TcpOk ? $"{Host}:{Port} reachable" : $"{Host}:{Port} {Failure ?? "unreachable"}");
        }
        return string.Join("; ", parts);
    }
}

/// <summary>Checks whether a host can be reached right now.</summary>
public interface IConnectivityProbe
{
    Task<NetworkStatus> CheckAsync(string host, int port, CancellationToken ct);
}

/// <summary>
/// The real check: is an adapter connected, does the host's name resolve, and does a TCP connection to it
/// open. Cross-platform, uses nothing but the .NET network classes, and runs separately from the request
/// that failed so it says what the path looks like now. Each step is capped at a few seconds. It never
/// throws, except <see cref="OperationCanceledException"/> when the caller's own token is cancelled.
/// </summary>
public sealed class ConnectivityProbe : IConnectivityProbe
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(3);

    public async Task<NetworkStatus> CheckAsync(string host, int port, CancellationToken ct)
    {
        var link = ActiveLink();
        if (link is null)
            return new NetworkStatus(null, host, port, false, false, null);

        IPAddress[] addresses;
        try
        {
            using var dns = CancellationTokenSource.CreateLinkedTokenSource(ct);
            dns.CancelAfter(StepTimeout);
            addresses = await Dns.GetHostAddressesAsync(host, dns.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return new NetworkStatus(link, host, port, false, false, null);
        }
        if (addresses.Length == 0)
            return new NetworkStatus(link, host, port, false, false, null);

        string? failure = null;
        foreach (var address in addresses.Take(2))
        {
            try
            {
                using var connect = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connect.CancelAfter(StepTimeout);
                using var client = new TcpClient(address.AddressFamily);
                await client.ConnectAsync(address, port, connect.Token);
                return new NetworkStatus(link, host, port, true, true, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (OperationCanceledException) { failure = "timed out"; }
            catch (SocketException ex) { failure = DescribeSocketError(ex.SocketErrorCode); }
            catch (Exception ex) { failure = ex.GetType().Name; }
        }
        return new NetworkStatus(link, host, port, true, false, failure);
    }

    /// <summary>
    /// The kind of the connected adapter that has a gateway, or null if there is none. A virtual
    /// adapter (Hyper-V, WSL, a VPN's tunnel) can be "up" without any route out, so a gateway is required.
    /// </summary>
    internal static string? ActiveLink()
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var hasGateway = adapter.GetIPProperties().GatewayAddresses.Any(g =>
                    !g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.IPv6Any));
                if (hasGateway) return Kind(adapter.NetworkInterfaceType);
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException)
        {
            // Can't enumerate adapters here: say nothing is known rather than that nothing is connected.
            return "network";
        }
        return null;
    }

    private static string Kind(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
            or NetworkInterfaceType.Ethernet3Megabit => "Ethernet",
        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => "Mobile broadband",
        _ => type.ToString(),
    };

    private static string DescribeSocketError(SocketError error) => error switch
    {
        SocketError.ConnectionRefused => "refused the connection",
        SocketError.TimedOut => "timed out",
        SocketError.NetworkUnreachable or SocketError.HostUnreachable => "unreachable",
        SocketError.ConnectionReset => "reset the connection",
        _ => error.ToString(),
    };
}
