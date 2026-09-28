namespace AgentSharpLib;

/// <summary>
/// Process-wide diagnostic and execution-mode switches. Hosts (e.g. the REPL's
/// /historytrace, /toolstrace, /requesttrace and /sync commands) toggle these;
/// the agent loop and sub-agents read them.
/// </summary>
public static class AgentFlags
{
    public static bool HistoryTrace;
    public static bool ToolsTrace;
    public static bool RequestTrace;

    /// <summary>When true, use AgentLoop.RunTurnNonStreamingAsync (SendAsync) instead
    /// of the default RunTurnAsync (StreamAsync). Toggled via the /sync REPL command.</summary>
    public static bool SyncMode;
}
