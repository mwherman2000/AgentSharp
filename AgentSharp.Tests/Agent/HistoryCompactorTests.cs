using AgentSharpLib.Agent;
using AgentSharpLib.Llm;

namespace AgentSharp.Tests.Agent;

public class HistoryCompactorTests
{
    /// <summary>Five turns of 11, 21, 31, 41 and 97 estimated characters (the last has a loop nudge and a tool result): 201 in all.</summary>
    private static ConversationHistory Sample()
    {
        var h = new ConversationHistory();
        foreach (var (prompt, answer) in new[] { ("q1", 9), ("q2", 19), ("q3", 29), ("q4", 39) })
        {
            h.AddUserMessage(prompt);
            h.AddAssistantMessage([new TextBlock { Text = new string('a', answer) }]);
        }
        h.AddUserMessage("q5");
        h.AddAssistantMessage([new TextBlock { Text = new string('a', 20) }]);
        h.AddUserMessage("nudge", PromptKind.Sys);
        h.AddAssistantMessage([new TextBlock { Text = new string('a', 20) }]);
        h.AddToolResults([new ToolResultBlock { ToolUseId = "t", Content = new string('r', 50) }]);
        return h;
    }

    [Theory]
    [InlineData("-50", 50, true)]
    [InlineData("+50", 50, false)]
    [InlineData("50", 50, false)]
    [InlineData(" 25% ", 25, false)]
    public void TryParse_AcceptsSignedAndUnsignedPercentages(string arg, int percent, bool dropOldest)
    {
        Assert.True(HistoryCompactor.TryParse(arg, out var p, out var d));
        Assert.Equal((percent, dropOldest), (p, d));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData("-100")]
    public void TryParse_RejectsMissingOrOutOfRange(string? arg)
        => Assert.False(HistoryCompactor.TryParse(arg, out _, out _));

    [Fact]
    public void Drop50_RemovesTheOldestTurnsMakingUpAtLeastHalf()
    {
        var h = Sample(); // half of 201 is 100.5

        var result = HistoryCompactor.Compact(h, 50, dropOldest: true);

        Assert.Equal(4, result.TurnsDropped); // 11+21+31 = 63 is under half; adding 41 makes 104
        Assert.Equal(1, result.TurnsKept);
        Assert.Equal("q5", h.Messages[0].GetText());
        Assert.Equal(1, h.TurnCount);
    }

    [Fact]
    public void Keep50_KeepsTheMostRecentTurnsMakingUpAtLeastHalf()
    {
        var h = Sample();

        var result = HistoryCompactor.Compact(h, 50, dropOldest: false);

        Assert.Equal(3, result.TurnsDropped); // the newest turn is 97, just under half; adding q4 makes 138
        Assert.Equal(2, result.TurnsKept);
        Assert.Equal("q4", h.Messages[0].GetText());
    }

    [Fact]
    public void AlwaysKeepsTheMostRecentTurn_AndNeverSplitsToolCallsFromResults()
    {
        var h = Sample();

        HistoryCompactor.Compact(h, 99, dropOldest: true);

        Assert.Equal("q5", h.Messages[0].GetText());
        Assert.Equal(6, h.Messages.Count); // q5, answer, nudge, answer, tool results: the whole last turn, then the compaction record
        Assert.Equal(PromptKind.Cmp, h.Messages[^1].Kind);
    }

    [Fact]
    public void EmptyHistory_IsANoOp()
    {
        var result = HistoryCompactor.Compact(new ConversationHistory(), 50, dropOldest: true);
        Assert.Equal(0, result.TurnsDropped);
    }

    [Fact]
    public async Task CompactionRecord_SurvivesSaveAndLoad_AndShowsAsMInTheTranscript_NeverSentToTheModel()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentsharp-cmp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var h = Sample();
            HistoryCompactor.Compact(h, 50, dropOldest: true);

            await new AgentSharpLib.Memory.SessionManager(dir).SaveAsync(h, "cmp");
            Assert.Contains("\"cmp\"", await File.ReadAllTextAsync(Path.Combine(dir, "sessions", "cmp.json")));
            var loaded = await new AgentSharpLib.Memory.SessionManager(dir).LoadAsync("cmp");
            Assert.Equal(PromptKind.Cmp, loaded!.Messages[^1].Kind);
            Assert.Equal(1, loaded.TurnCount);
            Assert.Equal(2, loaded.QuestionCount); // q5 and its loop nudge; the record takes no prompt number

            var md = File.ReadAllText(AgentSharpLib.Transcripts.ConversationTranscript.Write(loaded, "prompt", dir, "t")).Replace("\r\n", "\n");
            Assert.Contains("## Q1\n", md);
            Assert.Contains("## M2\n", md); // shares the number of the last prompt (the nudge, S2)
            Assert.Contains("Conversation compacted: dropped the 4 oldest turns", md);
            Assert.DoesNotContain("## A3", md);

            var llm = new FakeLlmClient().Enqueue(new LlmResponse { Message = ChatMessage.AssistantText("ok"), StopReason = "end_turn" });
            var loop = new AgentLoop(llm, new AgentSharpLib.Tools.ToolRegistry(), new AgentSharpLib.Safety.ApprovalGate(), "sys", history: loaded);
            await loop.RunTurnNonStreamingAsync("next");
            Assert.DoesNotContain(llm.Requests[0].Messages, m => m.Kind == PromptKind.Cmp);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
