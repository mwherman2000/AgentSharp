using AgentSharpLib.Agent;

namespace AgentSharpLib;

/// <summary>
/// What <see cref="AgentSession.Compact"/> did. <see cref="Result"/> is null when the compact was not
/// run because the save before it failed.
/// </summary>
/// <param name="Before">The save taken before compacting (with a transcript), or null if the session has no <see cref="AgentSession.Sessions"/>.</param>
/// <param name="After">The save taken after (without a transcript); null if nothing was dropped or there is no <see cref="AgentSession.Sessions"/>.</param>
public sealed record CompactOutcome(CompactResult? Result, AutoSaveResult? Before, AutoSaveResult? After);

/// <summary>
/// What <see cref="AgentSession.AutoSave"/> did: the saved session's id, the transcript written
/// with it (if one was asked for), or why a part failed.
/// </summary>
/// <param name="SessionId">The saved session's id (its file name without ".json"), or null if the save failed.</param>
/// <param name="TranscriptPath">The .docx transcript, or null if none was asked for or it failed.</param>
/// <param name="Error">Set when the session or the transcript could not be written.</param>
public sealed record AutoSaveResult(string? SessionId, string? TranscriptPath, string? Error)
{
    /// <summary>True if the session itself was saved (a failed transcript does not undo that).</summary>
    public bool Saved => SessionId is not null;

    /// <summary>One line for the console: "Auto-saved ray-handoff-donald-... (transcript: C:\...docx)".</summary>
    public string Describe() =>
        Error is not null ? Error
        : TranscriptPath is null ? $"Auto-saved {SessionId}"
        : $"Auto-saved {SessionId} (transcript: {TranscriptPath})";
}
