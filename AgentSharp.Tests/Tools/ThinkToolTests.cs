using System.Text.Json;
using AgentSharpLib.Tools.Implementations;

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

    [Theory]
    [InlineData("<invoke name=write_file>")]
    [InlineData("<parameter name=path>x</parameter>")]
    [InlineData("antml:function_calls")]
    public async Task ReturnsError_WhenThoughtContainsToolCallMarkup(string thought)
    {
        var tool = new ThinkTool();
        var input = JsonDocument.Parse($$"""{"thought": "Writing now. {{thought}}"}""").RootElement;

        var result = await tool.ExecuteAsync(input);

        Assert.True(result.IsError);
        Assert.Contains("NOT executed", result.Output);
    }

    [Fact]
    public void IsReadOnly()
    {
        var tool = new ThinkTool();
        Assert.Equal(AgentSharpLib.Tools.ToolRiskLevel.ReadOnly, tool.RiskLevel);
    }
}
