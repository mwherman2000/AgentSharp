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
        Assert.Equal(5, h.TurnCount); // q1-q4 dropped, so numbering carries on from turn 5
    }

    [Fact]
    public void Keep50_KeepsTheMostRecentTurnsMakingUpAtLeastHalf()
    {
        var h = Sample();

        var result = HistoryCompactor.Compact(h, 50, dropOldest: false);

        Assert.Equal(4, result.TurnsDropped); // the newest turn alone is ~32 of ~58 estimated tokens (its tool result weighs more per character), over half
        Assert.Equal(1, result.TurnsKept);
        Assert.Equal("q5", h.Messages[0].GetText());
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
    public void RealTokenCount_CalibratesTheReportedSizes()
    {
        var h = Sample();
        h.RecordPromptUsage(1_004_382); // the provider's count for this history
        var estimatedAll = HistoryCompactor.EstimateTokens(h.Messages);

        var result = HistoryCompactor.Compact(h, 50, dropOldest: true);

        Assert.True(result.Calibrated);
        Assert.InRange(result.TokensBefore, 1_004_382 * 99 / 100, 1_004_382 * 101 / 100); // matches the real count (the sample is tiny, so rounding is visible)
        Assert.True(result.TokensAfter < result.TokensBefore / 2 + result.TokensBefore / 10);
        Assert.True(estimatedAll < result.TokensBefore);
        Assert.False(HistoryCompactor.Compact(Sample(), 50, dropOldest: true).Calibrated);
    }

    [Fact]
    public async Task PromptAndTurnNumbers_NeverGoBackwards_AcrossCompactAndSaveAndLoad()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentsharp-num-" + Guid.NewGuid().ToString("N"));
        try
        {
            var h = Sample();
            var promptsBefore = h.QuestionCount; // q1-q4, q5, nudge = 6
            var turnsBefore = h.TurnCount;       // 5

            HistoryCompactor.Compact(h, 50, dropOldest: true);

            Assert.Equal(promptsBefore, h.QuestionCount);
            Assert.Equal(turnsBefore, h.TurnCount);

            await new AgentSharpLib.Memory.SessionManager(dir).SaveAsync(h, "n");
            var loaded = await new AgentSharpLib.Memory.SessionManager(dir).LoadAsync("n");
            Assert.Equal(promptsBefore, loaded!.QuestionCount);
            Assert.Equal(turnsBefore, loaded.TurnCount);

            loaded.AddUserMessage("next");
            Assert.Equal(promptsBefore + 1, loaded.QuestionCount);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void ContextTokens_IsEstimated_ThenActual_ThenCalibratedOnceMessagesChange()
    {
        var h = Sample();
        var (estimate, kind) = h.ContextTokens();
        Assert.Equal(TokenCountKind.Estimated, kind);
        Assert.Equal(HistoryCompactor.EstimateTokens(h.Messages), estimate);

        h.RecordPromptUsage(1_000);
        Assert.Equal((1_000L, TokenCountKind.Actual), h.ContextTokens());

        h.AddUserMessage("more");
        var (grown, grownKind) = h.ContextTokens();
        Assert.Equal(TokenCountKind.Calibrated, grownKind);
        Assert.True(grown > 1_000);
    }

    [Fact]
    public void DescribeContextUsage_SaysWhetherTheFigureIsActualOrEstimated()
    {
        var h = Sample();
        var loop = new AgentLoop(new FakeLlmClient(), new AgentSharpLib.Tools.ToolRegistry(), new AgentSharpLib.Safety.ApprovalGate(), "sys", history: h);
        Assert.Contains("estimated from message text", loop.DescribeContextUsage());

        h.RecordPromptUsage(250_000);
        Assert.Contains("250,000 tokens, 25% of 1,000,000 (actual", loop.DescribeContextUsage());
    }

    [Fact]
    public async Task PromptTooLong_RecordsTheProvidersCount_AndABreakdownOfWhatWasSent()
    {
        var h = Sample();
        var llm = new FakeLlmClient().EnqueueThrow(new HttpRequestException(
            "Anthropic API error 400 Bad Request: prompt is too long: 1004798 tokens > 1000000 maximum", null, System.Net.HttpStatusCode.BadRequest));
        var loop = new AgentLoop(llm, new AgentSharpLib.Tools.ToolRegistry(), new AgentSharpLib.Safety.ApprovalGate(), "system prompt", history: h);

        await loop.RunTurnNonStreamingAsync("hi");

        var record = h.Messages[^1];
        Assert.Equal(PromptKind.Err, record.Kind);
        Assert.Contains("prompt is too long: 1004798 tokens", record.GetText());
        Assert.Contains("Request sent: ~", record.GetText());
        Assert.Contains("of messages (", record.GetText());
        var (tokens, kind) = h.ContextTokens(); // the error record was added after the count, so it is scaled from it
        Assert.Equal(TokenCountKind.Calibrated, kind);
        Assert.True(tokens >= 1_004_798);
    }

    [Fact]
    public async Task AfterACompact_TheModelIsToldInTheFirstMessage_ButTheHistoryIsUntouched()
    {
        var h = Sample();
        var llm = new FakeLlmClient()
            .Enqueue(new LlmResponse { Message = ChatMessage.AssistantText("one"), StopReason = "end_turn" })
            .Enqueue(new LlmResponse { Message = ChatMessage.AssistantText("two"), StopReason = "end_turn" });
        var loop = new AgentLoop(llm, new AgentSharpLib.Tools.ToolRegistry(), new AgentSharpLib.Safety.ApprovalGate(), "sys", history: h);

        // Before: nothing was removed, so the first message goes through as written.
        await loop.RunTurnNonStreamingAsync("hello");
        Assert.Equal("q1", llm.Requests[0].Messages[0].GetText());

        HistoryCompactor.Compact(h, 50, dropOldest: true); // drops q1-q4: 4 prompts, leaving q5 first
        await loop.RunTurnNonStreamingAsync("again");

        var first = llm.Requests[1].Messages[0].GetText();
        Assert.StartsWith("[Note from the application:", first);
        Assert.Contains($"oldest {h.PromptOffset} prompts", first);
        Assert.True(h.PromptOffset > 0);
        Assert.EndsWith(h.Messages[0].GetText(), first);
        Assert.DoesNotContain("[Note from the application:", h.Messages[0].GetText()); // the stored history has no note
    }

    [Fact]
    public void TraceJson_IncludesTheTextAndToolFields_NotJustTheBlockTypes()
    {
        var request = new LlmRequest
        {
            SystemPrompt = "sys",
            Messages = [ChatMessage.UserText("what is two plus two"), new ChatMessage { Role = MessageRole.Assistant, Content = [new TextBlock { Text = "four" }] }],
        };

        var json = AgentLoop.TraceJson(request);

        Assert.Contains("what is two plus two", json);
        Assert.Contains("four", json);
        Assert.Contains("\"type\":\"text\"", json);
    }

    [Theory]
    [InlineData("notes", "notes.md")]
    [InlineData("notes.md", "notes.md")]
    [InlineData("notes.docx", "notes.docx")]
    [InlineData("NOTES.DOCX", "NOTES.DOCX")]
    [InlineData("../../escape", "escape.md")]
    [InlineData("a/b/c.docx", "c.docx")]
    public void TranscriptPathFor_MatchesWhereWriteWouldPutTheFile(string name, string expectedFileName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentsharp-pathfor");
        Assert.Equal(Path.Combine(dir, expectedFileName), AgentSharpLib.Transcripts.ConversationTranscript.PathFor(dir, name));
    }

    [Fact]
    public void TranscriptPathFor_IsNullForANameWithNoFileName()
    {
        Assert.Null(AgentSharpLib.Transcripts.ConversationTranscript.PathFor("dir", ""));
        Assert.Null(AgentSharpLib.Transcripts.ConversationTranscript.PathFor("dir", "a/"));
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
            Assert.Equal(5, loaded.TurnCount);
            Assert.Equal(6, loaded.QuestionCount); // 4 dropped + q5 and its loop nudge; the record takes no prompt number

            var md = File.ReadAllText(AgentSharpLib.Transcripts.ConversationTranscript.Write(loaded, "prompt", dir, "t")).Replace("\r\n", "\n");
            Assert.Contains("## Q5\n", md); // numbering carries on past the 4 dropped prompts
            Assert.Contains("## M6\n", md); // shares the number of the last prompt (the nudge, S6)
            Assert.Contains("Conversation compacted: dropped the 4 oldest turns", md);
            Assert.DoesNotContain("## A7", md);

            var llm = new FakeLlmClient().Enqueue(new LlmResponse { Message = ChatMessage.AssistantText("ok"), StopReason = "end_turn" });
            var loop = new AgentLoop(llm, new AgentSharpLib.Tools.ToolRegistry(), new AgentSharpLib.Safety.ApprovalGate(), "sys", history: loaded);
            await loop.RunTurnNonStreamingAsync("next");
            Assert.DoesNotContain(llm.Requests[0].Messages, m => m.Kind == PromptKind.Cmp);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
