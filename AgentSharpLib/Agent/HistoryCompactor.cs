using AgentSharpLib.Llm;

namespace AgentSharpLib.Agent;

/// <summary>What a /compact did: turns dropped and kept, and the estimated tokens before and after.</summary>
public sealed record CompactResult(int TurnsDropped, int TurnsKept, long TokensBefore, long TokensAfter, bool Calibrated = false);

/// <summary>
/// Shrinks a conversation by dropping its oldest turns. A turn is a typed or checkpoint prompt
/// plus everything the agent did to answer it (tool calls, results, loop nudges, errors), so a
/// tool call is never separated from its result. Sizes are estimated from the characters (by content type): good
/// enough to split a history by percentage, though not an exact token count.
/// </summary>
public static class HistoryCompactor
{
    /// <summary>
    /// Parses a /compact argument. "-50" drops the oldest turns making up at least 50% of the
    /// estimated tokens; "+50" or "50" keeps the most recent turns making up at least 50%.
    /// The percentage must be 1-99.
    /// </summary>
    public static bool TryParse(string? argument, out int percent, out bool dropOldest)
    {
        percent = 0;
        dropOldest = false;
        var text = argument?.Trim() ?? "";
        if (text.EndsWith('%')) text = text[..^1];
        var sign = text.Length > 0 && text[0] is '-' or '+' ? text[0] : '+';
        if (text.Length > 0 && text[0] is '-' or '+') text = text[1..];
        dropOldest = sign == '-';
        return int.TryParse(text, out percent) && percent is >= 1 and <= 99;
    }

    /// <summary>
    /// Drops the oldest turns of <paramref name="history"/>. With <paramref name="dropOldest"/> the dropped turns
    /// are the oldest ones making up at least <paramref name="percent"/>% of the estimated tokens; otherwise the
    /// kept turns are the most recent ones making up at least that share. Either way the most recent turn is kept.
    /// </summary>
    public static CompactResult Compact(ConversationHistory history, int percent, bool dropOldest)
    {
        var starts = TurnStarts(history.Messages);
        if (starts.Count == 0) return new CompactResult(0, 0, 0, 0);

        // Real tokens per estimated token when the provider has reported a count, else 1 (plain estimate).
        var scale = history.TokenScale;
        var factor = scale ?? 1.0;
        var calibrated = scale is not null;

        var sizes = new double[starts.Count];
        for (var t = 0; t < starts.Count; t++)
        {
            var end = t + 1 < starts.Count ? starts[t + 1] : history.Messages.Count;
            for (var i = starts[t]; i < end; i++)
                sizes[t] += EstimateMessageTokens(history.Messages[i]);
        }
        var total = sizes.Sum();

        var dropped = 0;
        if (dropOldest)
        {
            // The oldest turns, until they make up at least `percent` of the total.
            double acc = 0;
            while (dropped < starts.Count - 1 && acc * 100 < total * percent)
                acc += sizes[dropped++];
        }
        else
        {
            // The newest turns, until they make up at least `percent` of the total; the rest go.
            double acc = 0;
            var kept = 0;
            while (kept < starts.Count && acc * 100 < total * percent)
                acc += sizes[starts.Count - 1 - kept++];
            dropped = starts.Count - kept;
        }

        var after = sizes.Skip(dropped).Sum();
        var tokensBefore = (long)(total * factor);
        var tokensAfter = (long)(after * factor);
        if (dropped == 0) return new CompactResult(0, starts.Count, tokensBefore, tokensBefore, calibrated);

        var firstDroppedPrompt = history.PromptOffset + 1;
        history.DropFirst(starts[dropped]);
        history.AddCompactionNote($"Conversation compacted: dropped the {dropped} oldest turns (prompts {firstDroppedPrompt}-{history.PromptOffset}), " +
            $"kept {starts.Count - dropped} ({(calibrated ? "calibrated" : "estimated")} tokens {tokensBefore:N0} -> {tokensAfter:N0}).");
        return new CompactResult(dropped, starts.Count - dropped, tokensBefore, tokensAfter, calibrated);
    }

    /// <summary>Index of the first message of each turn. Anything before the first prompt joins turn one.</summary>
    private static List<int> TurnStarts(IReadOnlyList<ChatMessage> messages)
    {
        var starts = new List<int>();
        for (var i = 0; i < messages.Count; i++)
        {
            var m = messages[i];
            if (m.Role == MessageRole.User && m.Kind != PromptKind.Sys && m.Content.OfType<TextBlock>().Any(b => b.Text.Length > 0))
                starts.Add(starts.Count == 0 ? 0 : i);
        }
        return starts;
    }

    /// <summary>Prose tokenizes at about 4 characters per token.</summary>
    public const double CharsPerProseToken = 4.0;

    /// <summary>
    /// Tool inputs and results (JSON, file contents, web pages, code, URLs) pack more tokens into the same
    /// characters. 2.5 reproduced a real tool-heavy conversation (557 messages) to within 1% of the provider's count.
    /// </summary>
    public const double CharsPerStructuredToken = 2.5;

    /// <summary>The estimate for a whole history, by content type (see the constants): not a real token count.</summary>
    internal static long EstimateTokens(IEnumerable<ChatMessage> messages)
        => (long)Math.Round(messages.Sum(EstimateMessageTokens));

    private static double EstimateMessageTokens(ChatMessage message)
    {
        double tokens = 0;
        foreach (var block in message.Content)
        {
            tokens += block switch
            {
                TextBlock t => t.Text.Length / CharsPerProseToken,
                ToolUseBlock u => (u.Name.Length + u.Input.GetRawText().Length) / CharsPerStructuredToken,
                ToolResultBlock r => r.Content.Length / CharsPerStructuredToken,
                _ => 0,
            };
        }
        return tokens;
    }
}
