using AgentSharpLib.Agent.MultiAgent;

namespace AgentSharpLib.Output;

/// <summary>
/// Everything the library reports while it works -- model text as it arrives,
/// status/warning/error lines, and sub-agent lifecycle -- expressed as semantic
/// events rather than console calls, so the host decides how (or whether) to render
/// them. The CLI renders these with Spectre.Console; a library consumer that doesn't
/// care can pass nothing and get <see cref="NullAgentOutput"/>.
///
/// Implementations must tolerate concurrent calls: parallel sub-agents share one
/// instance.
/// </summary>
public interface IAgentOutput
{
    /// <summary>A chunk of model response text (a streamed delta, or a whole
    /// non-streamed response). May contain newlines and braces; render literally.</summary>
    void Text(string text);

    /// <summary>Ends the current line -- after a response's text, and as a separator
    /// before tool execution output.</summary>
    void LineBreak();

    /// <summary>Raw diagnostic dump (the RequestTrace/ToolsTrace/HistoryTrace
    /// payloads in <see cref="AgentFlags"/>).</summary>
    void Trace(string text);

    /// <summary>Low-emphasis status line (retry notices, recovery hints).</summary>
    void Info(string message);

    /// <summary>Something the user should notice but the turn continues or stops
    /// cleanly (token-limit nudges, stall detection, iteration cap).</summary>
    void Warning(string message);

    /// <summary>A non-retryable failure (e.g. auth or bad request from the LLM).</summary>
    void Error(string message);

    /// <summary>A transient failure the loop is about to retry, with full exception
    /// detail for diagnosis.</summary>
    void TransientError(Exception exception);

    /// <summary>A Write-risk tool was run without prompting.</summary>
    void ToolAutoApproved(string toolName);

    /// <summary>Tool discovery found a tool type whose constructor threw.</summary>
    void ToolSkipped(string toolTypeName, string reason);

    /// <summary>A sub-agent is starting. <paramref name="inBatch"/> is true when it
    /// is one of a parallel batch (see <see cref="SubAgentBatchStarted"/>).</summary>
    void SubAgentStarted(string name, string task, bool inBatch);

    /// <summary>A sub-agent has stopped, successfully or not.</summary>
    void SubAgentFinished(string name, SubAgentStatus status, bool inBatch);

    /// <summary>A parallel batch of sub-agents is about to run.</summary>
    void SubAgentBatchStarted(int count, int maxConcurrent);

    /// <summary>Every sub-agent in a parallel batch has stopped.</summary>
    void SubAgentBatchFinished(int count);
}
