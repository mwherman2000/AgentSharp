using AgentSharp.Agent.MultiAgent;

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
}
