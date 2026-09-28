using AgentSharpLib.Agent.MultiAgent;

namespace AgentSharpLib.Output;

/// <summary>Discards all agent output. The default when a host supplies none.</summary>
public sealed class NullAgentOutput : IAgentOutput
{
    public static readonly NullAgentOutput Instance = new();

    private NullAgentOutput() { }

    public void Text(string text) { }
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
