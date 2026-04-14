using System.Text.Json;
using AgentSharp.Tools.Implementations;

namespace AgentSharp.Tests.Tools;

public class ThinkToolTests
{
    [Fact]
    public async Task ReturnsSuccess_WithAnyThought()
    {
        var tool = new ThinkTool();
        var input = JsonDocument.Parse("""{"thought": "I should read the file first"}""").RootElement;

        var result = await tool.ExecuteAsync(input);

        Assert.False(result.IsError);
        Assert.Contains("Thought recorded", result.Output);
    }

    [Fact]
    public void IsReadOnly()
    {
        var tool = new ThinkTool();
        Assert.Equal(AgentSharp.Tools.ToolRiskLevel.ReadOnly, tool.RiskLevel);
    }
}
