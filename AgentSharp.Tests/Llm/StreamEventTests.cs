using AgentSharp.Llm;

namespace AgentSharp.Tests.Llm;

public class StreamEventTests
{
    [Fact]
    public void TextDelta_StoresText()
    {
        var evt = new TextDelta("Hello");
        Assert.Equal("Hello", evt.Text);
    }

    [Fact]
    public void ToolUseStart_StoresIdAndName()
    {
        var evt = new ToolUseStart("tool-123", "read_file");
        Assert.Equal("tool-123", evt.Id);
        Assert.Equal("read_file", evt.Name);
    }

    [Fact]
    public void ToolInputDelta_StoresPartialJson()
    {
        var evt = new ToolInputDelta("{\"path\":");
        Assert.Equal("{\"path\":", evt.PartialJson);
    }

    [Fact]
    public void StreamDone_StoresStopReason()
    {
        var evt = new StreamDone("end_turn");
        Assert.Equal("end_turn", evt.StopReason);
    }

    [Fact]
    public void UsageInfo_StoresTokenCounts()
    {
        var evt = new UsageInfo(100, 50);
        Assert.Equal(100, evt.InputTokens);
        Assert.Equal(50, evt.OutputTokens);
    }

    [Fact]
    public void AllEvents_AreStreamEvents()
    {
        StreamEvent[] events =
        [
            new TextDelta("text"),
            new ToolUseStart("id", "name"),
            new ToolInputDelta("json"),
            new ToolUseEnd(),
            new StreamDone("end_turn"),
            new UsageInfo(10, 20)
        ];

        Assert.All(events, e => Assert.IsAssignableFrom<StreamEvent>(e));
    }
}
