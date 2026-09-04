using AgentSharp.Agent.MultiAgent;
using AgentSharp.Safety;
using AgentSharp.Tests.Agent;
using AgentSharp.Tools;

namespace AgentSharp.Tests.Agent.MultiAgent;

public class SubAgentTests
{
    [Fact]
    public void SubAgentResult_IsSuccess_WhenCompleted()
    {
        var result = new SubAgentResult("test", "id-1", "output", SubAgentStatus.Completed);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SubAgentResult_IsNotSuccess_WhenFailed()
    {
        var result = new SubAgentResult("test", "id-1", "error", SubAgentStatus.Failed);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void SubAgentResult_IsNotSuccess_WhenCancelled()
    {
        var result = new SubAgentResult("test", "id-1", "cancelled", SubAgentStatus.Cancelled);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task RunAsync_ParentTokenCancelled_PropagatesAsExceptionInsteadOfSwallowing()
    {
        // Regression test: linkedCts (parentCt + the sub-agent's own _cts) used to be
        // caught and converted into a normal "Sub-agent was cancelled." return value
        // regardless of which of the two actually fired. When it's the *parent's* own
        // token -- the whole turn being interrupted (e.g. Ctrl+C) -- that must
        // propagate as a real exception so the parent agent loop aborts the turn
        // cleanly, instead of recording a misleadingly "successful" tool result.
        var subAgent = new SubAgent(
            "test-sub", "do the thing",
            new FakeStreamingLlmClient(), new ToolRegistry(), new ApprovalGate(), "system prompt");

        using var parentCts = new CancellationTokenSource();
        parentCts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => subAgent.RunAsync("do the thing", parentCts.Token));

        Assert.Equal(SubAgentStatus.Cancelled, subAgent.Status);
    }

    [Fact]
    public async Task RunAsync_OwnCancelCalled_StillReturnsGracefulResultInsteadOfThrowing()
    {
        // The other half of the same fix: a deliberate, scoped Cancel() (e.g.
        // AgentOrchestrator.CancelAll stopping sibling sub-agents after one fails)
        // with the parent token untouched must keep reporting gracefully, not throw.
        var subAgent = new SubAgent(
            "test-sub", "do the thing",
            new FakeStreamingLlmClient(), new ToolRegistry(), new ApprovalGate(), "system prompt");

        subAgent.Cancel();

        var result = await subAgent.RunAsync("do the thing");

        Assert.Equal("Sub-agent was cancelled.", result);
        Assert.Equal(SubAgentStatus.Cancelled, subAgent.Status);
    }
}
