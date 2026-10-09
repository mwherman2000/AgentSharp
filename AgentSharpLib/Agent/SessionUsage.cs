using System.Diagnostics;

namespace AgentSharpLib.Agent;

/// <summary>
/// Session-wide usage: elapsed wall-clock time since the session started and the
/// tokens consumed by every LLM call made during it. Unlike <see cref="AgentLoop"/>'s
/// own totals, which start over whenever a conversation is replaced (/clear, /load),
/// this spans the whole session and also counts sub-agents' calls. Thread-safe, since
/// concurrent sub-agents report into the same instance.
/// </summary>
public sealed class SessionUsage
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _inputTokens;
    private long _outputTokens;
    private long _cacheCreationTokens;
    private long _cacheReadTokens;

    /// <summary>When the session started (local time).</summary>
    public DateTime StartedAt { get; } = DateTime.Now;

    /// <summary>Wall-clock time since the session started.</summary>
    public TimeSpan Elapsed => _clock.Elapsed;

    public long InputTokens => Interlocked.Read(ref _inputTokens);
    public long OutputTokens => Interlocked.Read(ref _outputTokens);
    public long CacheCreationTokens => Interlocked.Read(ref _cacheCreationTokens);
    public long CacheReadTokens => Interlocked.Read(ref _cacheReadTokens);

    /// <summary>Every token billed: input, output, and cache writes and reads.</summary>
    public long TotalTokens => InputTokens + OutputTokens + CacheCreationTokens + CacheReadTokens;

    /// <summary>Record one LLM call's usage.</summary>
    public void Add(int inputTokens, int outputTokens, int cacheCreationTokens, int cacheReadTokens)
    {
        Interlocked.Add(ref _inputTokens, inputTokens);
        Interlocked.Add(ref _outputTokens, outputTokens);
        Interlocked.Add(ref _cacheCreationTokens, cacheCreationTokens);
        Interlocked.Add(ref _cacheReadTokens, cacheReadTokens);
    }

    /// <summary>Elapsed time as h:mm:ss (days folded into the hours).</summary>
    public static string FormatElapsed(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
}
