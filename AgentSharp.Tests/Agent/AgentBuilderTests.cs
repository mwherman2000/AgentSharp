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
}
