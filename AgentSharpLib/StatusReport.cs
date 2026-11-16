using AgentSharpLib.Agent;
using AgentSharpLib.Context;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;

namespace AgentSharpLib;

/// <summary>One line of the /status listing: "Label: value (note)".</summary>
public sealed record StatusRow(string Label, string Value, string? Note = null)
{
    /// <summary>The plain-text form, as recorded in the history and shown in transcripts.</summary>
    public override string ToString() => $"{Label}: {Value}{(Note is null ? "" : " " + Note)}";
}

/// <summary>
/// What /status lists, built in one place so every host application shows the same rows in the same order.
/// A host only decides how a row is drawn.
/// </summary>
public static class StatusReport
{
    /// <summary>
    /// The rows for <paramref name="session"/>. When the context size is only estimated, asks the provider for
    /// its exact count first (bounded to a few seconds; it cannot throw).
    /// </summary>
    public static async Task<IReadOnlyList<StatusRow>> BuildAsync(AgentSession session, CancellationToken ct = default)
    {
        var loop = session.Loop;
        var toolNames = string.Join(", ", session.Tools.All.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal));
        if (loop.EstimateContextTokens().Kind != TokenCountKind.Actual)
            await loop.RefreshTokenCountAsync(ct);

        return
        [
            new("Provider", session.Llm.ProviderName),
            new("Model", session.Llm.ModelId),
            new("Superprompt", SystemPromptBuilder.ResolveAgentName(session.SuperPrompt)),
            new("Superprompts", PersonaRegistry.Current.DescribeList(), "(/personas for roles)"),
            new("Handoff stack", session.DescribeHandoffStack()),
            new("Sync mode", AgentFlags.SyncMode ? "on (SendAsync, non-streaming)" : "off (StreamAsync, default)"),
            new("Timeout (streaming)", FormatTimeout(session.Llm.StreamingTimeout)),
            new("Timeout (non-streaming)", FormatTimeout(session.Llm.NonStreamingTimeout)),
            new("Max tokens", session.MaxTokens.ToString()),
            new("Max iterations", session.MaxIterations.ToString()),
            new("Tools", session.Tools.All.Count.ToString(), $"({toolNames})"),
            new("Turns", session.History.TurnCount.ToString()),
            new("Messages", session.History.Count.ToString()),
            new("Context", loop.DescribeContextUsage()),
            new("Tokens", $"{loop.TotalInputTokens} in / {loop.TotalOutputTokens} out"),
            new("Cache", $"{loop.TotalCacheCreationTokens} written / {loop.TotalCacheReadTokens} read{FormatCacheHitRate(loop)}"),
            new("Session", $"{SessionUsage.FormatElapsed(session.Usage.Elapsed)} elapsed, {session.Usage.TotalTokens:N0} tokens", "(all conversations and sub-agents)"),
            new("Directory", session.Project.WorkingDirectory),
            new("Git branch", session.Project.GitBranch ?? "N/A"),
            new("Memory", session.Memory?.FilePath ?? "off"),
        ];
    }

    private static string FormatCacheHitRate(AgentLoop loop)
    {
        var effectiveInput = loop.TotalInputTokens + loop.TotalCacheCreationTokens + loop.TotalCacheReadTokens;
        if (effectiveInput == 0) return "";
        return $" ({100.0 * loop.TotalCacheReadTokens / effectiveInput:F0}% hit rate)";
    }

    /// <summary>A timeout as a compact duration ("1m 40s", "1h", "10h") rather than TimeSpan's default text.</summary>
    internal static string FormatTimeout(TimeSpan timeout)
    {
        if (timeout.TotalHours >= 1)
            return timeout.Minutes == 0 ? $"{timeout.TotalHours:F0}h" : $"{(int)timeout.TotalHours}h {timeout.Minutes}m";
        if (timeout.TotalMinutes >= 1)
            return timeout.Seconds == 0 ? $"{timeout.Minutes}m" : $"{timeout.Minutes}m {timeout.Seconds}s";
        return $"{timeout.TotalSeconds:F0}s";
    }
}
