using System.Net;
using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class WebFetchToolSsrfTests
{
    [Theory]
    [InlineData("127.0.0.1")]          // loopback
    [InlineData("10.0.0.1")]           // 10.0.0.0/8
    [InlineData("172.16.0.1")]         // 172.16.0.0/12
    [InlineData("172.31.255.255")]     // 172.16.0.0/12 (upper bound)
    [InlineData("192.168.1.1")]        // 192.168.0.0/16
    [InlineData("169.254.169.254")]    // link-local -- cloud metadata endpoint
    [InlineData("0.0.0.0")]
    [InlineData("100.64.0.1")]         // carrier-grade NAT
    [InlineData("224.0.0.1")]          // multicast
    [InlineData("::1")]                // IPv6 loopback
    [InlineData("fe80::1")]            // IPv6 link-local
    [InlineData("fc00::1")]            // IPv6 unique local
    public void IsBlockedAddress_BlocksPrivateAndInternalRanges(string ip)
    {
        Assert.True(WebFetchTool.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.15.255.255")]     // just below 172.16.0.0/12
    [InlineData("172.32.0.1")]         // just above 172.16.0.0/12
    [InlineData("2606:4700:4700::1111")] // Cloudflare DNS (public IPv6)
    public void IsBlockedAddress_AllowsPublicAddresses(string ip)
    {
        Assert.False(WebFetchTool.IsBlockedAddress(IPAddress.Parse(ip)));
    }
}
