using AgentSharp.Tools;

namespace AgentSharp.Tests.Tools;

public class ToolResultTests
{
    [Fact]
    public void Success_CreatesNonError()
    {
        var result = ToolResult.Success("output");
        Assert.False(result.IsError);
        Assert.Equal("output", result.Output);
    }

    [Fact]
    public void Error_CreatesError()
    {
        var result = ToolResult.Error("something failed");
        Assert.True(result.IsError);
        Assert.Equal("something failed", result.Output);
    }
}
