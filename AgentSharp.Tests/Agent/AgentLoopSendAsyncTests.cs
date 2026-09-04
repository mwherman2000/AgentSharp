using System.Net;
using System.Text.Json;
using AgentSharp.Agent;
using AgentSharp.Llm;
using AgentSharp.Safety;
using AgentSharp.Tools;

namespace AgentSharp.Tests.Agent;

/// <summary>
/// Tests for AgentLoop.RunTurnNonStreamingAsync -- the SendAsync-based counterpart
/// to RunTurnAsync, exercised via the /sync REPL command. These verify the actual
/// tool-processing loop (history updates, tool execution, retries), not just
/// response parsing (covered separately in AnthropicClientTests/OpenAiCompatibleClientTests).
/// </summary>
public class AgentLoopSendAsyncTests
{
    private static LlmResponse TextResponse(
        string text,
        string stopReason = "end_turn",
        int inputTokens = 10,
        int outputTokens = 5,
        int cacheCreationTokens = 0,
        int cacheReadTokens = 0) => new()
    {
        Message = ChatMessage.AssistantText(text),
        StopReason = stopReason,
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        CacheCreationInputTokens = cacheCreationTokens,
        CacheReadInputTokens = cacheReadTokens
    };

    private static LlmResponse ToolUseResponse(
        string toolCallId,
        string toolName,
        object input,
        int inputTokens = 10,
        int outputTokens = 5,
        int cacheCreationTokens = 0,
        int cacheReadTokens = 0) => new()
    {
        Message = new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = [new ToolUseBlock
            {
                Id = toolCallId,
                Name = toolName,
                Input = JsonSerializer.SerializeToElement(input)
            }]
        },
        StopReason = "tool_use",
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        CacheCreationInputTokens = cacheCreationTokens,
        CacheReadInputTokens = cacheReadTokens
    };

    [Fact]
    public async Task RunTurnNonStreamingAsync_TextOnlyResponse_ReturnsTextAndUpdatesHistory()
    {
        var llm = new FakeLlmClient().Enqueue(TextResponse("Hello!", inputTokens: 10, outputTokens: 5));
        var loop = new AgentLoop(llm, new ToolRegistry(), new ApprovalGate(), "system prompt");

        var result = await loop.RunTurnNonStreamingAsync("hi");

        Assert.Equal("Hello!", result);
        Assert.Equal(1, llm.CallCount);
        Assert.Equal(2, loop.History.Count); // user, assistant
        Assert.Equal(10, loop.TotalInputTokens);
        Assert.Equal(5, loop.TotalOutputTokens);
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_ExecutesToolAndFeedsResultBack()
    {
        var tool = new FakeTool("test_tool", ToolRiskLevel.ReadOnly, _ => ToolResult.Success("42"));
        var tools = new ToolRegistry();
        tools.Register(tool);

        var llm = new FakeLlmClient()
            .Enqueue(ToolUseResponse("call_1", "test_tool", new { x = 1 }))
            .Enqueue(TextResponse("Done"));

        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "system prompt");

        var result = await loop.RunTurnNonStreamingAsync("do the thing");

        Assert.Equal("Done", result);
        Assert.Equal(2, llm.CallCount);

        var call = Assert.Single(tool.Calls);
        Assert.Equal(1, call.GetProperty("x").GetInt32());

        // user, assistant(tool_use), tool-result, assistant(text)
        Assert.Equal(4, loop.History.Count);
        var toolResultBlock = Assert.IsType<ToolResultBlock>(Assert.Single(loop.History.Messages[2].Content));
        Assert.Equal("call_1", toolResultBlock.ToolUseId);
        Assert.Equal("42", toolResultBlock.Content);
        Assert.False(toolResultBlock.IsError);
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_MaxTokensWithNoToolUse_NudgesAndContinues()
    {
        var llm = new FakeLlmClient()
            .Enqueue(TextResponse("partial thought", stopReason: "max_tokens"))
            .Enqueue(TextResponse(" continued and done"));

        var loop = new AgentLoop(llm, new ToolRegistry(), new ApprovalGate(), "system prompt");

        var result = await loop.RunTurnNonStreamingAsync("go");

        Assert.Equal("partial thought continued and done", result);
        Assert.Equal(2, llm.CallCount);

        // user, assistant(partial), user(nudge), assistant(final)
        Assert.Equal(4, loop.History.Count);
        Assert.Equal(MessageRole.User, loop.History.Messages[2].Role);
        Assert.Contains("cut off", loop.History.Messages[2].GetText());
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_NonRetryableError_BreaksImmediately()
    {
        var llm = new FakeLlmClient()
            .EnqueueThrow(new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized));

        var loop = new AgentLoop(llm, new ToolRegistry(), new ApprovalGate(), "system prompt");

        var result = await loop.RunTurnNonStreamingAsync("hi");

        Assert.Equal("", result);
        Assert.Equal(1, llm.CallCount);
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_TransientError_RetriesThenSucceeds()
    {
        var llm = new FakeLlmClient()
            .EnqueueThrow(new InvalidOperationException("transient hiccup"))
            .Enqueue(TextResponse("recovered"));

        var loop = new AgentLoop(llm, new ToolRegistry(), new ApprovalGate(), "system prompt");

        var result = await loop.RunTurnNonStreamingAsync("hi");

        Assert.Equal("recovered", result);
        Assert.Equal(2, llm.CallCount);
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_OperationCanceledExceptionNotFromCallerToken_RetriesInsteadOfAbortingTurn()
    {
        // AnthropicClient enforces its own per-request timeout via a CancellationTokenSource
        // linked to (but distinct from) the token passed into SendAsync/StreamAsync -- a
        // stalled connection throws the exact same OperationCanceledException type a real
        // Ctrl+C does. The caller's own token (ct, passed to RunTurnNonStreamingAsync below)
        // is never cancelled here, so this must be treated as a retryable transient failure,
        // not an immediate turn abort -- regression test for that misclassification.
        var llm = new FakeLlmClient()
            .EnqueueThrow(new OperationCanceledException("simulated per-request timeout"))
            .Enqueue(TextResponse("recovered"));

        var loop = new AgentLoop(llm, new ToolRegistry(), new ApprovalGate(), "system prompt");

        var result = await loop.RunTurnNonStreamingAsync("hi", CancellationToken.None);

        Assert.Equal("recovered", result);
        Assert.Equal(2, llm.CallCount);
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_RetryAttempt_GetsAShorterCancellationBudgetThanFirstAttempt()
    {
        // The first attempt must use the caller's own token unmodified -- it should
        // get the LLM client's full configured timeout, not a wrapped/shortened one.
        // Only a *retry* (after a failure) should be capped by RetryAttemptTimeout, so
        // a persistently dead connection is discovered fast instead of waiting out the
        // full timeout on every one of maxRetries attempts.
        var llm = new FakeLlmClient()
            .EnqueueThrow(new InvalidOperationException("transient hiccup"))
            .Enqueue(TextResponse("recovered"));

        var loop = new AgentLoop(llm, new ToolRegistry(), new ApprovalGate(), "system prompt");

        await loop.RunTurnNonStreamingAsync("hi", CancellationToken.None);

        Assert.Equal(2, llm.ReceivedTokens.Count);
        Assert.Equal(CancellationToken.None, llm.ReceivedTokens[0]);
        Assert.NotEqual(CancellationToken.None, llm.ReceivedTokens[1]);
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_AccumulatesUsageAcrossIterations()
    {
        var tool = new FakeTool("test_tool", ToolRiskLevel.ReadOnly, _ => ToolResult.Success("ok"));
        var tools = new ToolRegistry();
        tools.Register(tool);

        var llm = new FakeLlmClient()
            .Enqueue(ToolUseResponse("call_1", "test_tool", new { }, inputTokens: 100, outputTokens: 20, cacheCreationTokens: 50))
            .Enqueue(TextResponse("done", inputTokens: 30, outputTokens: 10, cacheReadTokens: 150));

        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "system prompt");

        await loop.RunTurnNonStreamingAsync("go");

        Assert.Equal(130, loop.TotalInputTokens);
        Assert.Equal(30, loop.TotalOutputTokens);
        Assert.Equal(50, loop.TotalCacheCreationTokens);
        Assert.Equal(150, loop.TotalCacheReadTokens);
    }
}
