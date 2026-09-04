using System.Diagnostics;
using System.Text.Json;
using AgentSharp.Agent;
using AgentSharp.Llm;
using AgentSharp.Safety;
using AgentSharp.Telemetry;
using AgentSharp.Tools;

namespace AgentSharp.Tests.Agent;

/// <summary>
/// Verifies AgentLoop emits OpenTelemetry spans (via AgentTelemetry.Source) at the
/// turn/LLM-call/tool-call boundaries, using a raw ActivityListener rather than the
/// OTel SDK's TracerProvider -- ActivitySource.StartActivity only produces non-null
/// Activities when *something* is listening, so this is also what proves the
/// instrumentation is live rather than silently a no-op.
///
/// Test classes run in parallel by default in xUnit, and AgentTelemetry.Source is a
/// process-wide static, so an active listener here could observe activities from
/// unrelated concurrently-running tests. Each test below sidesteps that by tagging
/// its FakeLlmClient response with a near-unique token count and filtering the
/// captured activities down to that value before asserting, then following the
/// real parent/child Activity relationship (which Activity.Current/AsyncLocal keeps
/// correctly isolated per logical call context even under parallelism) rather than
/// assuming there's only one "agent.turn"/"llm.request" activity in the whole run.
/// </summary>
public class AgentLoopTelemetryTests
{
    private static LlmResponse TextResponse(string text, int inputTokens, int outputTokens) => new()
    {
        Message = ChatMessage.AssistantText(text),
        StopReason = "end_turn",
        InputTokens = inputTokens,
        OutputTokens = outputTokens
    };

    private static LlmResponse ToolUseResponse(string toolCallId, string toolName, object input, int inputTokens, int outputTokens) => new()
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
        OutputTokens = outputTokens
    };

    private static ActivityListener StartListening(List<Activity> sink)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = sink.Add
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_EmitsTurnAndLlmRequestActivities()
    {
        var activities = new List<Activity>();
        using var listener = StartListening(activities);

        const int inputTokens = 987651;
        const int outputTokens = 111651;
        var llm = new FakeLlmClient().Enqueue(TextResponse("Hello!", inputTokens, outputTokens));
        var loop = new AgentLoop(llm, new ToolRegistry(), new ApprovalGate(), "system prompt");

        await loop.RunTurnNonStreamingAsync("hi");

        var llmActivity = Assert.Single(activities, a =>
            a.OperationName == "llm.request" &&
            inputTokens.Equals(a.GetTagItem("llm.usage.input_tokens")));

        Assert.Equal(outputTokens, llmActivity.GetTagItem("llm.usage.output_tokens"));
        Assert.Equal("end_turn", llmActivity.GetTagItem("llm.stop_reason"));
        Assert.Equal(ActivityStatusCode.Unset, llmActivity.Status);

        var turnActivity = llmActivity.Parent;
        Assert.NotNull(turnActivity);
        Assert.Equal("agent.turn", turnActivity!.OperationName);
        Assert.Equal("non-streaming", turnActivity.GetTagItem("turn.mode"));
        Assert.Equal("end_turn", turnActivity.GetTagItem("turn.stop_reason"));
        Assert.Equal(inputTokens, turnActivity.GetTagItem("turn.total_input_tokens"));
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_EmitsToolActivityWithStatus()
    {
        var activities = new List<Activity>();
        using var listener = StartListening(activities);

        const string toolCallId = "call_otel_test_987652";
        var tool = new FakeTool("otel_test_tool", ToolRiskLevel.ReadOnly, _ => ToolResult.Success("ok"));
        var tools = new ToolRegistry();
        tools.Register(tool);

        var llm = new FakeLlmClient()
            .Enqueue(ToolUseResponse(toolCallId, "otel_test_tool", new { }, inputTokens: 1, outputTokens: 1))
            .Enqueue(TextResponse("Done", inputTokens: 1, outputTokens: 1));

        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "system prompt");

        await loop.RunTurnNonStreamingAsync("do the thing");

        var toolActivity = Assert.Single(activities, a =>
            a.OperationName == $"tool.{tool.Name}" &&
            toolCallId.Equals(a.GetTagItem("tool.id")));

        Assert.Equal(tool.Name, toolActivity.GetTagItem("tool.name"));
        Assert.Equal(false, toolActivity.GetTagItem("tool.is_error"));
        Assert.Equal(ActivityStatusCode.Unset, toolActivity.Status);
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_ToolError_SetsErrorStatus()
    {
        var activities = new List<Activity>();
        using var listener = StartListening(activities);

        const string toolCallId = "call_otel_test_987653";
        var tool = new FakeTool("otel_failing_tool", ToolRiskLevel.ReadOnly, _ => ToolResult.Error("boom"));
        var tools = new ToolRegistry();
        tools.Register(tool);

        var llm = new FakeLlmClient()
            .Enqueue(ToolUseResponse(toolCallId, "otel_failing_tool", new { }, inputTokens: 1, outputTokens: 1))
            .Enqueue(TextResponse("Done", inputTokens: 1, outputTokens: 1));

        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "system prompt");

        await loop.RunTurnNonStreamingAsync("do the thing");

        var toolActivity = Assert.Single(activities, a =>
            a.OperationName == $"tool.{tool.Name}" &&
            toolCallId.Equals(a.GetTagItem("tool.id")));

        Assert.Equal(true, toolActivity.GetTagItem("tool.is_error"));
        Assert.Equal(ActivityStatusCode.Error, toolActivity.Status);
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_MultipleToolCalls_CountsExecutionsPerTurnAndTotal()
    {
        var activities = new List<Activity>();
        using var listener = StartListening(activities);

        const string callId1 = "call_count_test_1_987654";
        const string callId2 = "call_count_test_2_987654";
        var tool = new FakeTool("count_test_tool_a", ToolRiskLevel.ReadOnly, _ => ToolResult.Success("ok"));
        var tools = new ToolRegistry();
        tools.Register(tool);

        // A single non-streaming response can carry more than one tool_use block --
        // both execute within the same turn, so both should count against it.
        var llm = new FakeLlmClient()
            .Enqueue(new LlmResponse
            {
                Message = new ChatMessage
                {
                    Role = MessageRole.Assistant,
                    Content =
                    [
                        new ToolUseBlock { Id = callId1, Name = tool.Name, Input = JsonSerializer.SerializeToElement(new { }) },
                        new ToolUseBlock { Id = callId2, Name = tool.Name, Input = JsonSerializer.SerializeToElement(new { }) }
                    ]
                },
                StopReason = "tool_use",
                InputTokens = 1,
                OutputTokens = 1
            })
            .Enqueue(TextResponse("Done", inputTokens: 1, outputTokens: 1));

        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "system prompt");

        await loop.RunTurnNonStreamingAsync("do two things");

        var first = Assert.Single(activities, a => callId1.Equals(a.GetTagItem("tool.id")));
        var second = Assert.Single(activities, a => callId2.Equals(a.GetTagItem("tool.id")));
        Assert.Equal(1, first.GetTagItem("tool.execution.turn_count"));
        Assert.Equal(1, first.GetTagItem("tool.execution.total_count"));
        Assert.Equal(2, second.GetTagItem("tool.execution.turn_count"));
        Assert.Equal(2, second.GetTagItem("tool.execution.total_count"));

        // tool.* -> llm.request -> agent.turn, same parent-chain pattern the other
        // tests in this file follow (e.g. llmActivity.Parent above).
        var turnActivity = first.Parent?.Parent;
        Assert.NotNull(turnActivity);
        Assert.Equal("agent.turn", turnActivity!.OperationName);
        Assert.Equal(2, turnActivity.GetTagItem("turn.tool_executions"));
        Assert.Equal(2, loop.TotalToolExecutions);
    }

    [Fact]
    public async Task RunTurnNonStreamingAsync_SecondTurn_ResetsPerTurnCountButKeepsAccumulatingTotal()
    {
        var activities = new List<Activity>();
        using var listener = StartListening(activities);

        const string turn1CallId = "call_count_test_turn1_987655";
        const string turn2CallId = "call_count_test_turn2_987655";
        var tool = new FakeTool("count_test_tool_b", ToolRiskLevel.ReadOnly, _ => ToolResult.Success("ok"));
        var tools = new ToolRegistry();
        tools.Register(tool);

        var llm = new FakeLlmClient()
            .Enqueue(ToolUseResponse(turn1CallId, tool.Name, new { }, inputTokens: 1, outputTokens: 1))
            .Enqueue(TextResponse("Done 1", inputTokens: 1, outputTokens: 1))
            .Enqueue(ToolUseResponse(turn2CallId, tool.Name, new { }, inputTokens: 1, outputTokens: 1))
            .Enqueue(TextResponse("Done 2", inputTokens: 1, outputTokens: 1));

        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "system prompt");

        await loop.RunTurnNonStreamingAsync("first turn");
        await loop.RunTurnNonStreamingAsync("second turn");

        var turn1Tool = Assert.Single(activities, a => turn1CallId.Equals(a.GetTagItem("tool.id")));
        var turn2Tool = Assert.Single(activities, a => turn2CallId.Equals(a.GetTagItem("tool.id")));

        // Per-turn count resets each turn...
        Assert.Equal(1, turn1Tool.GetTagItem("tool.execution.turn_count"));
        Assert.Equal(1, turn2Tool.GetTagItem("tool.execution.turn_count"));
        // ...but the grand total keeps accumulating across turns.
        Assert.Equal(1, turn1Tool.GetTagItem("tool.execution.total_count"));
        Assert.Equal(2, turn2Tool.GetTagItem("tool.execution.total_count"));
        Assert.Equal(2, loop.TotalToolExecutions);
    }

    [Fact]
    public async Task RunTurnStreamingAsync_MultipleToolUseBlocksInOneResponse_ProjectsExecutionCountsOnToolUseEndEvent()
    {
        var activities = new List<Activity>();
        using var listener = StartListening(activities);

        const string callId1 = "call_stream_count_test_1_987656";
        const string callId2 = "call_stream_count_test_2_987656";
        var tool = new FakeTool("stream_count_test_tool", ToolRiskLevel.ReadOnly, _ => ToolResult.Success("ok"));
        var tools = new ToolRegistry();
        tools.Register(tool);

        // Both tool_use blocks arrive in the same streamed response, so neither has
        // been executed (and _turnToolExecutions/_totalToolExecutions haven't been
        // incremented) by the time either tool_use_end event fires -- the tags must
        // still reflect each block's eventual ordinal position once executed.
        var llm = new FakeStreamingLlmClient()
            .Enqueue(
                new ToolUseStart(callId1, tool.Name),
                new ToolInputDelta("{}"),
                new ToolUseEnd(),
                new ToolUseStart(callId2, tool.Name),
                new ToolInputDelta("{}"),
                new ToolUseEnd(),
                new StreamDone("tool_use"))
            .Enqueue(
                new TextDelta("Done"),
                new StreamDone("end_turn"));

        var loop = new AgentLoop(llm, tools, new ApprovalGate(), "system prompt");

        await loop.RunTurnStreamingAsync("do two things");

        var llmActivity = Assert.Single(activities, a =>
            a.OperationName == "llm.request" && a.Events.Any(e => e.Name == "tool_use_end"));
        var toolUseEndEvents = llmActivity.Events.Where(e => e.Name == "tool_use_end").ToList();
        Assert.Equal(2, toolUseEndEvents.Count);

        var first = toolUseEndEvents[0].Tags.ToDictionary(t => t.Key, t => t.Value);
        var second = toolUseEndEvents[1].Tags.ToDictionary(t => t.Key, t => t.Value);

        Assert.Equal(callId1, first["tool.id"]);
        Assert.Equal(1, first["tool.execution.turn_count"]);
        Assert.Equal(1, first["tool.execution.total_count"]);

        Assert.Equal(callId2, second["tool.id"]);
        Assert.Equal(2, second["tool.execution.turn_count"]);
        Assert.Equal(2, second["tool.execution.total_count"]);

        // The values projected during streaming must match what ExecuteToolCallsAsync
        // actually assigns once these tool calls run.
        var toolActivity1 = Assert.Single(activities, a => callId1.Equals(a.GetTagItem("tool.id")));
        var toolActivity2 = Assert.Single(activities, a => callId2.Equals(a.GetTagItem("tool.id")));
        Assert.Equal(1, toolActivity1.GetTagItem("tool.execution.turn_count"));
        Assert.Equal(2, toolActivity2.GetTagItem("tool.execution.turn_count"));
    }
}
