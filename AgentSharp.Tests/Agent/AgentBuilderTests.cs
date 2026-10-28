using System.Text;
using AgentSharpLib;
using AgentSharpLib.Agent;
using AgentSharpLib.Agent.MultiAgent;
using AgentSharpLib.Llm;
using AgentSharpLib.Output;
using AgentSharpLib.Tools;

namespace AgentSharp.Tests.Agent;

public class AgentBuilderTests
{
    private sealed class RecordingOutput : IAgentOutput
    {
        public StringBuilder Text { get; } = new();
        void IAgentOutput.Text(string text) => Text.Append(text);
        public void LineBreak() { }
        public void Trace(string text) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) { }
        public void TransientError(Exception exception) { }
        public void ToolAutoApproved(string toolName) { }
        public void ToolSkipped(string toolTypeName, string reason) { }
        public void SubAgentStarted(string name, string task, bool inBatch) { }
        public void SubAgentFinished(string name, SubAgentStatus status, bool inBatch) { }
        public void SubAgentBatchStarted(int count, int maxConcurrent) { }
        public void SubAgentBatchFinished(int count) { }
    }

    private static StreamEvent[] Reply(string text) => [new TextDelta(text), new StreamDone("end_turn")];

    /// <summary>A builder that touches neither the network, the filesystem scan, nor MEMORY.md.</summary>
    private static AgentBuilder Isolated(ILlmClient llm) =>
        new AgentBuilder().WithLlmClient(llm).WithoutProjectScan().WithoutMemory();

    [Fact]
    public async Task SendAsync_RunsATurnAndRendersToOutput()
    {
        var llm = new FakeStreamingLlmClient().Enqueue(Reply("Hello!"));
        var output = new RecordingOutput();
        var session = await Isolated(llm).WithOutput(output).BuildAsync();

        var answer = await session.SendAsync("hi");

        Assert.Equal("Hello!", answer);
        Assert.Equal("Hello!", output.Text.ToString());
        Assert.Equal(2, session.History.Count);
    }

    [Fact]
    public async Task Defaults_RegisterBuiltInsSubAgentAndMemoryTools()
    {
        var session = await new AgentBuilder()
            .WithLlmClient(new FakeStreamingLlmClient())
            .WithoutProjectScan()
            .BuildAsync();

        var names = session.Tools.All.Select(t => t.Name).ToList();
        Assert.Contains("read_file", names);
        Assert.Contains("run_shell", names);
        Assert.Contains("sub_agent", names);
        Assert.Contains("remember", names);
        Assert.NotNull(session.Memory);
    }

    [Fact]
    public async Task Without_Options_LeaveOnlyCustomTools()
    {
        var custom = new FakeTool("custom", ToolRiskLevel.ReadOnly, _ => ToolResult.Success("ok"));

        var session = await Isolated(new FakeStreamingLlmClient())
            .WithoutBuiltInTools()
            .WithoutSubAgents()
            .WithoutHandoff()
            .WithTool(custom)
            .BuildAsync();

        Assert.Equal(["custom"], session.Tools.All.Select(t => t.Name));
        Assert.Null(session.Memory);
    }

    [Fact]
    public async Task WithTool_ReplacesBuiltInOfSameName()
    {
        var replacement = new FakeTool("read_file", ToolRiskLevel.ReadOnly, _ => ToolResult.Success("fake"));

        var session = await Isolated(new FakeStreamingLlmClient()).WithTool(replacement).BuildAsync();

        Assert.Same(replacement, session.Tools.Get("read_file"));
    }

    [Fact]
    public async Task WithSystemPrompt_IsUsedVerbatim()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSystemPrompt("You are a test.").BuildAsync();

        Assert.Equal("You are a test.", session.SystemPrompt);
    }

    [Fact]
    public async Task WithSuperPrompt_OverridesOptions()
    {
        var session = await Isolated(new FakeStreamingLlmClient())
            .WithOptions(new AgentOptions { SuperPrompt = "andy" })
            .WithSuperPrompt("lucy")
            .BuildAsync();

        Assert.Equal("lucy", session.SuperPrompt);
        Assert.Contains("Lucy", session.SystemPrompt);
    }

    [Fact]
    public async Task Options_LimitsAreApplied()
    {
        var session = await Isolated(new FakeStreamingLlmClient())
            .WithOptions(new AgentOptions { MaxTokens = 1234, MaxIterations = 7 })
            .BuildAsync();

        Assert.Equal(1234, session.MaxTokens);
        Assert.Equal(7, session.MaxIterations);
    }

    [Fact]
    public async Task UnknownSuperPrompt_FailsTheBuild()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Isolated(new FakeStreamingLlmClient()).WithSuperPrompt("nobody").BuildAsync());
    }

    [Fact]
    public async Task NoClientAndNoKey_FailsTheBuild()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AgentBuilder().WithOptions(new AgentOptions { Provider = "anthropic" })
                .WithoutProjectScan().WithoutMemory().BuildAsync());

        Assert.Contains("API key", ex.Message);
    }

    [Fact]
    public async Task Reset_StartsFreshConversationAndCanSwitchPersona()
    {
        var llm = new FakeStreamingLlmClient().Enqueue(Reply("one"));
        var session = await Isolated(llm).BuildAsync();
        await session.SendAsync("hi");

        session.Reset("lucy");

        Assert.Equal(0, session.History.Count);
        Assert.Equal("lucy", session.SuperPrompt);
        Assert.Contains("Lucy", session.SystemPrompt);
    }

    [Fact]
    public async Task Reset_WithUnknownPersona_ThrowsAndKeepsConversation()
    {
        var llm = new FakeStreamingLlmClient().Enqueue(Reply("one"));
        var session = await Isolated(llm).BuildAsync();
        await session.SendAsync("hi");
        var loop = session.Loop;

        Assert.Throws<ArgumentException>(() => session.Reset("nobody"));

        Assert.Same(loop, session.Loop);
        Assert.Equal(2, session.History.Count);
    }

    [Fact]
    public async Task Reset_WithoutPersona_KeepsCustomSystemPrompt()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSystemPrompt("custom").BuildAsync();

        session.Reset();

        Assert.Equal("custom", session.SystemPrompt);
    }

    [Fact]
    public async Task Restore_ContinuesSavedHistory()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).BuildAsync();
        var saved = new ConversationHistory();
        saved.AddUserMessage("earlier");

        session.Restore(saved);

        Assert.Same(saved, session.History);
    }

    [Fact]
    public async Task ToolEvents_AreForwardedAcrossResets()
    {
        var llm = new FakeStreamingLlmClient()
            .Enqueue(new ToolUseStart("t1", "echo"), new ToolInputDelta("{}"), new ToolUseEnd(), new StreamDone("tool_use"))
            .Enqueue(Reply("done"));
        var echo = new FakeTool("echo", ToolRiskLevel.ReadOnly, _ => ToolResult.Success("echoed"));
        var session = await Isolated(llm).WithoutBuiltInTools().WithoutSubAgents().WithTool(echo).BuildAsync();
        var started = new List<string>();
        var ended = new List<string>();
        session.OnToolStart += (name, _) => started.Add(name);
        session.OnToolEnd += (name, _) => ended.Add(name);

        session.Reset(); // events subscribed before a reset must still fire afterwards
        await session.SendAsync("go");

        Assert.Equal(["echo"], started);
        Assert.Equal(["echo"], ended);
        Assert.Single(echo.Calls);
    }

    [Fact]
    public async Task HandoffTool_SwitchesPersonaAfterTheTurn_KeepingHistory()
    {
        var llm = new FakeStreamingLlmClient()
            .Enqueue(new ToolUseStart("h1", "handoff"), new ToolInputDelta("""{"persona":"donald","reason":"needs a deal-maker"}"""), new ToolUseEnd(), new StreamDone("tool_use"))
            .Enqueue(Reply("Handing you to Donald."));
        var session = await Isolated(llm).WithSuperPrompt("andy").BuildAsync();

        await session.SendAsync("negotiate this");

        Assert.Equal("donald", session.SuperPrompt);
        Assert.Equal(5, session.History.Count); // user, tool_use, tool_result, final answer: nothing lost, plus the handoff record
        Assert.Equal(PromptKind.Hnd, session.History.Messages[^1].Kind);
        Assert.Contains("Donald", session.SystemPrompt);
        Assert.Contains("handed over to you by andy", session.SystemPrompt);
        Assert.Contains("Reason: needs a deal-maker. Assistant turns", session.SystemPrompt); // the reason ends its own sentence
    }

    [Fact]
    public async Task HandoffTool_DoesNotSwitchMidTurn()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSuperPrompt("andy").BuildAsync();
        var tool = session.Tools.Get("handoff")!;

        var result = await tool.ExecuteAsync(System.Text.Json.JsonDocument.Parse("""{"persona":"ray","reason":"r"}""").RootElement);

        Assert.False(result.IsError);
        Assert.Equal("andy", session.SuperPrompt); // applied only after the turn
    }

    [Theory]
    [InlineData("rosie")]   // a real persona, but not a handoff target
    [InlineData("nobody")]  // not a persona
    [InlineData("andy")]    // the current persona
    public async Task HandoffTool_RejectsUnavailableTargets(string persona)
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSuperPrompt("andy").BuildAsync();
        var tool = session.Tools.Get("handoff")!;

        var result = await tool.ExecuteAsync(System.Text.Json.JsonDocument.Parse($$"""{"persona":"{{persona}}","reason":"r"}""").RootElement);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task SystemPrompt_ListsOnlyTheOtherHandoffPersonas()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSuperPrompt("andy").BuildAsync();

        var catalog = session.SystemPrompt[session.SystemPrompt.IndexOf("# Handing the conversation", StringComparison.Ordinal)..];
        foreach (var name in new[] { "ray", "donald", "lucy", "connie" })
            Assert.Contains($"- {name}:", catalog);
        Assert.DoesNotContain("- andy:", catalog);
        Assert.DoesNotContain("- rosie:", catalog);
    }

    [Fact]
    public async Task Handoff_NamesTheNewPersonaAndFromWhom_AndResetClearsIt()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSuperPrompt("ray").BuildAsync();

        session.Handoff("lucy", "wants a gentler tone");
        Assert.Contains("handed over to you by ray", session.SystemPrompt);

        session.Reset();
        Assert.DoesNotContain("Handoff received", session.SystemPrompt);
    }

    [Fact]
    public async Task Return_PopsTheStackAndKeepsHistory()
    {
        var llm = new FakeStreamingLlmClient().Enqueue(Reply("one"));
        var session = await Isolated(llm).WithSuperPrompt("andy").BuildAsync();
        await session.SendAsync("hi");

        session.Handoff("ray");
        session.Handoff("donald");
        Assert.Equal(["andy", "ray"], session.HandoffStack);

        session.Return("done here");

        Assert.Equal("ray", session.SuperPrompt);
        Assert.Equal(["andy"], session.HandoffStack);
        Assert.Equal(5, session.History.Count); // the 2 messages of the turn + a record each for 2 handoffs and 1 return
        Assert.Contains("returned to you by donald", session.SystemPrompt);
        Assert.Contains("done here", session.SystemPrompt);
        Assert.Contains("- return: hand the conversation back to andy, who handed it to you", session.SystemPrompt);
    }

    [Fact]
    public async Task HandoffRecord_NamesBothPersonasAndTheStack_AndIsNeverSentToTheProvider()
    {
        var llm = new FakeStreamingLlmClient().Enqueue(Reply("one")).Enqueue(Reply("two"));
        var session = await Isolated(llm).WithSuperPrompt("andy").BuildAsync();
        await session.SendAsync("hi");

        session.Handoff("ray", "needs the principles");
        var record = session.History.Messages[^1];
        Assert.Equal(PromptKind.Hnd, record.Kind);
        Assert.Equal("Handoff: Andy Pando -> Ray Pando (needs the principles). Handoff stack: Andy Pando > Ray Pando (current)", record.GetText());

        await session.SendAsync("go on");

        Assert.DoesNotContain(llm.Requests[^1].Messages, m => m.Kind == PromptKind.Hnd);
        Assert.Equal("two", session.History.GetLastAssistantText()); // the record is not "the last answer"
    }

    [Fact]
    public async Task Transcript_ShowsTheHandoffAsItsOwnEntryWithThePromptsNumber()
    {
        var llm = new FakeStreamingLlmClient().Enqueue(Reply("one")).Enqueue(Reply("two"));
        var session = await Isolated(llm).WithSuperPrompt("andy").BuildAsync();
        await session.SendAsync("hi");
        session.Handoff("ray");
        session.Handoff("donald");
        await session.SendAsync("go on");
        var path = session.WriteTranscript($"handoff_{Guid.NewGuid():N}.md"); // lands in the working directory
        try
        {
            var text = File.ReadAllText(path);

            Assert.Contains("## Q1", text);
            Assert.Contains("## H1", text);                       // shares the number of the prompt whose turn it ended
            Assert.Contains("Handoff: Andy Pando -> Ray Pando", text);
            Assert.Contains("Handoff stack: Andy Pando > Ray Pando > Donald J. Pando (current)", text);
            Assert.Contains("## Q2", text);                       // handoff records don't use up a prompt number
            Assert.DoesNotContain("## Q3", text);
            Assert.DoesNotContain("## A1\n\n\nHandoff", text);    // no answer part under a handoff entry
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SystemPrompt_NamesTheWholeStack_AndKeepsItAfterARestore()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSuperPrompt("andy").BuildAsync();
        session.Handoff("ray");
        session.Handoff("donald");

        Assert.Contains("andy > ray > donald (you)", session.SystemPrompt);
        Assert.Contains("do not need to work out which turn was whose", session.SystemPrompt);

        var state = session.HandoffState;
        session.Reset();
        Assert.DoesNotContain("# Handoff stack", session.SystemPrompt);

        session.Restore(new ConversationHistory(), state); // a /load: the stack comes back, the handoff note does not
        Assert.Contains("andy > ray > donald (you)", session.SystemPrompt);
        Assert.DoesNotContain("# Handoff received", session.SystemPrompt);
    }

    [Fact]
    public async Task PromptPrefix_FollowsTheCurrentPersona()
    {
        var session = await Isolated(new FakeStreamingLlmClient().Enqueue(Reply("one"))).WithSuperPrompt("ray").BuildAsync();
        await session.SendAsync("hi");
        Assert.Equal("2 Ray Pando>", AgentSharpApp.Ui.ReplHost.PromptPrefix(session.History.QuestionCount + 1, session.SuperPrompt));

        session.Handoff("donald");
        Assert.Equal("2 Donald J. Pando>", AgentSharpApp.Ui.ReplHost.PromptPrefix(session.History.QuestionCount + 1, session.SuperPrompt));

        session.Return();
        Assert.Equal("2 Ray Pando>", AgentSharpApp.Ui.ReplHost.PromptPrefix(session.History.QuestionCount + 1, session.SuperPrompt));
    }

    [Fact]
    public async Task HandoffToolDescription_AgreesWithTheCatalog_OnWhenToHandOff()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSuperPrompt("andy").BuildAsync();
        var description = session.Tools.Get("handoff")!.Description;

        // An explicit request from the user always counts, in both places.
        Assert.Contains("asks for a persona by name", description);
        Assert.Contains("asks for a persona by name", session.SystemPrompt);
        // Says return needs a prior handoff, the switch is deferred, and one call per turn.
        Assert.Contains("only after the conversation was handed to you", description);
        Assert.Contains("next prompt", description);
        Assert.Contains("at most once per turn", description);
        foreach (var persona in new[] { "ray", "donald", "andy", "lucy", "connie" })
            Assert.Contains(persona, description);
    }

    [Fact]
    public async Task Return_WithNothingToReturnTo_Throws()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).BuildAsync();

        Assert.Throws<InvalidOperationException>(() => session.Return());
    }

    [Fact]
    public async Task HandoffTool_ReturnTarget_ReturnsAfterTheTurn()
    {
        var llm = new FakeStreamingLlmClient()
            .Enqueue(new ToolUseStart("h1", "handoff"), new ToolInputDelta("""{"persona":"return","reason":"finished"}"""), new ToolUseEnd(), new StreamDone("tool_use"))
            .Enqueue(Reply("Handing back."));
        var session = await Isolated(llm).WithSuperPrompt("andy").BuildAsync();
        session.Handoff("ray");

        await session.SendAsync("thanks, that's all I needed from you");

        Assert.Equal("andy", session.SuperPrompt);
        Assert.Empty(session.HandoffStack);
    }

    [Fact]
    public async Task HandoffTool_ReturnTarget_WithNothingToReturnTo_IsRejected()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSuperPrompt("andy").BuildAsync();

        var result = await session.Tools.Get("handoff")!.ExecuteAsync(
            System.Text.Json.JsonDocument.Parse("""{"persona":"return","reason":"r"}""").RootElement);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ResetAndRestore_ClearTheStack_ButRestoreWithStateBringsItBack()
    {
        var session = await Isolated(new FakeStreamingLlmClient()).WithSuperPrompt("andy").BuildAsync();
        session.Handoff("ray");
        var state = session.HandoffState;
        Assert.Equal(["andy"], state!.Stack);
        Assert.Equal("ray", state.Persona);

        session.Reset();
        Assert.Empty(session.HandoffStack);
        Assert.Null(session.HandoffState);

        session.Restore(new ConversationHistory(), state);
        Assert.Equal("ray", session.SuperPrompt);
        Assert.Equal(["andy"], session.HandoffStack);

        session.Restore(new ConversationHistory()); // a session saved without a stack
        Assert.Empty(session.HandoffStack);
    }

    [Fact]
    public async Task Handoff_WithoutHandoffOrWithCustomPrompt_OffersNoTool()
    {
        var plain = await Isolated(new FakeStreamingLlmClient()).WithoutHandoff().BuildAsync();
        var custom = await Isolated(new FakeStreamingLlmClient()).WithSystemPrompt("custom").BuildAsync();

        Assert.Null(plain.Tools.Get("handoff"));
        Assert.Null(custom.Tools.Get("handoff"));
        Assert.DoesNotContain("Handing the conversation", plain.SystemPrompt);
    }
}
