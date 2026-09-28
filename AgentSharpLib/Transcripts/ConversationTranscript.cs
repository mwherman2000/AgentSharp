using AgentSharpLib.Agent;
using AgentSharpLib.Llm;

namespace AgentSharpLib.Transcripts;

/// <summary>
/// Writes a clean Q&amp;A transcript of a conversation -- the user's typed prompts, the
/// assistant's full text replies, and any "think" tool calls (rendered as
/// blockquoted/indented thoughts), with none of the other tool-call/tool-result noise.
/// </summary>
public static class ConversationTranscript
{
    /// <summary>
    /// Write <paramref name="history"/> to <paramref name="name"/> inside
    /// <paramref name="directory"/> and return the full path. Written as Word (.docx)
    /// when the name ends in ".docx", otherwise Markdown (".md" is appended if missing).
    /// Any directory portion of <paramref name="name"/> is ignored, so it can't escape
    /// <paramref name="directory"/>.
    /// </summary>
    /// <param name="systemPrompt">Its first line is recorded so the transcript shows
    /// which persona produced the replies.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> has no usable file name.</exception>
    /// <exception cref="IOException">The file couldn't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The file couldn't be written.</exception>
    public static string Write(ConversationHistory history, string systemPrompt, string directory, string name)
    {
        // Path.GetFileName strips any directory portion, so a name like "/trump14020"
        // or "../elsewhere" can't Path.Combine its way outside the directory (a leading
        // '/' makes the second Path.Combine argument rooted, which silently discards
        // the directory and resolves to the drive root instead).
        var safeName = Path.GetFileName(name);
        if (string.IsNullOrEmpty(safeName))
            throw new ArgumentException($"'{name}' is not a valid file name.", nameof(name));

        // Format is chosen by the requested file's own extension -- anything else
        // (no extension, or one we don't recognize) defaults to Markdown.
        var isDocx = safeName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);
        var fileName = isDocx || safeName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? safeName
            : $"{safeName}.md";
        var path = Path.Combine(directory, fileName);

        var qaPairs = BuildQaPairs(history);
        var systemPromptIntro = GetFirstParagraph(systemPrompt);
        var generatedAt = DateTime.Now;

        if (isDocx)
            File.WriteAllBytes(path, TranscriptWriter.BuildDocx(name, systemPromptIntro, generatedAt, qaPairs));
        else
            File.WriteAllText(path, TranscriptWriter.BuildMarkdown(name, systemPromptIntro, generatedAt, qaPairs));
        return path;
    }

    /// <summary>
    /// A single user turn can span several history entries (assistant text, tool
    /// calls, tool results, more assistant text), so consecutive assistant messages
    /// are merged into one answer, in original order, until the next real user
    /// prompt starts a new pair.
    /// </summary>
    internal static List<(string Question, List<AnswerSegment> Segments)> BuildQaPairs(ConversationHistory history)
    {
        var qaPairs = new List<(string Question, List<AnswerSegment> Segments)>();
        string? currentQuestion = null;
        var segments = new List<AnswerSegment>();

        foreach (var message in history.Messages)
        {
            if (message.Role == MessageRole.User)
            {
                // Tool-result messages are also role "user" but carry no TextBlock --
                // only messages with actual typed text are real prompts.
                var text = string.Join("\n\n", message.Content.OfType<TextBlock>().Select(b => b.Text));
                if (text.Length == 0) continue;

                if (currentQuestion is not null)
                    qaPairs.Add((currentQuestion, segments));

                currentQuestion = CapitalizeFirstLetter(text);
                segments = new List<AnswerSegment>();
            }
            else if (message.Role == MessageRole.Assistant)
            {
                foreach (var block in message.Content)
                {
                    switch (block)
                    {
                        case TextBlock { Text.Length: > 0 } tb:
                            segments.Add(new AnswerSegment(false, tb.Text));
                            break;
                        case ToolUseBlock { Name: "think" } thinkBlock:
                            if (ExtractThought(thinkBlock) is { } thought)
                                segments.Add(new AnswerSegment(true, thought));
                            break;
                    }
                }
            }
        }
        if (currentQuestion is not null)
            qaPairs.Add((currentQuestion, segments));

        return qaPairs;
    }

    /// <summary>
    /// Pulls the raw reasoning text out of a "think" tool call, leaving the choice of
    /// how to render it (Markdown blockquote vs. docx paragraph) to TranscriptWriter.
    /// Returns null if the block isn't a well-formed think call -- e.g. the LLM sent
    /// malformed input that failed to parse into a "thought" string.
    /// </summary>
    private static string? ExtractThought(ToolUseBlock block)
    {
        if (!block.Input.TryGetProperty("thought", out var thoughtProp) ||
            thoughtProp.GetString() is not { Length: > 0 } thought)
            return null;
        return thought;
    }

    /// <summary>
    /// Uppercases the first letter of a prompt for the transcript file -- users
    /// often type prompts lowercase, which reads oddly as the "Q" in a Q&amp;A
    /// document. Leaves the text untouched if it has no lowercase first letter
    /// (already capitalized, or starts with punctuation/a digit).
    /// </summary>
    private static string CapitalizeFirstLetter(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i])) continue;
            if (char.IsLower(text[i]))
                return text[..i] + char.ToUpper(text[i]) + text[(i + 1)..];
            return text;
        }
        return text;
    }

    /// <summary>
    /// Extracts just the first line of the system prompt, so the transcript records
    /// which persona/instructions produced the replies without dumping the entire --
    /// often very long -- prompt.
    /// </summary>
    private static string GetFirstParagraph(string text)
    {
        var trimmed = text.TrimStart();
        var newlineIndex = trimmed.IndexOf('\n');
        var firstLine = newlineIndex >= 0 ? trimmed[..newlineIndex] : trimmed;
        return firstLine.Trim();
    }
}
