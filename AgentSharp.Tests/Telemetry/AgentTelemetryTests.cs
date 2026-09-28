using AgentSharpLib.Telemetry;

namespace AgentSharp.Tests.Telemetry;

public class AgentTelemetryTests
{
    [Theory]
    [InlineData(AgentTelemetry.DefaultJaegerEndpoint)]
    [InlineData("https://collector.example.com:4317")]
    public void IsValidEndpoint_AcceptsHttpUrls(string endpoint) =>
        Assert.True(AgentTelemetry.IsValidEndpoint(endpoint));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("localhost:4317")]      // parses as scheme "localhost"
    [InlineData("ftp://localhost:4317")]
    [InlineData("/relative/path")]
    public void IsValidEndpoint_RejectsEverythingElse(string? endpoint) =>
        Assert.False(AgentTelemetry.IsValidEndpoint(endpoint));

    [Fact]
    public void SwitchToJaeger_InvalidEndpoint_ThrowsBeforeTouchingTheProvider()
    {
        var ex = Assert.Throws<ArgumentException>(() => AgentTelemetry.SwitchToJaeger("not a url"));

        Assert.Contains(AgentTelemetry.DefaultJaegerEndpoint, ex.Message);
    }
}
