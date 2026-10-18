namespace AgentSharpLib.Transcripts;

/// <summary>
/// What a /transcribe transcript includes beyond the user's prompts and the
/// assistant's text replies.
/// </summary>
/// <param name="IncludeThinking">Record the assistant's "think" tool calls as
/// blockquoted thoughts. On by default.</param>
/// <param name="IncludeToolCalls">Record every other tool call (name and input) and its
/// result, in the order they happened. Off by default.</param>
public sealed record TranscriptOptions(bool IncludeThinking = true, bool IncludeToolCalls = false)
{
    public const string Usage = "/transcribe <name> [--tools] [--no-thinking]";

    /// <summary>
    /// Splits a /transcribe argument into the file name and its flags. Flags may appear
    /// anywhere; everything else is rejoined as the name. Returns null (with
    /// <paramref name="error"/> set) on an unknown flag.
    /// </summary>
    public static TranscriptOptions? Parse(string argument, out string name, out string? error)
    {
        var options = new TranscriptOptions();
        var nameParts = new List<string>();
        error = null;

        foreach (var token in argument.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "--tools":
                    options = options with { IncludeToolCalls = true };
                    break;
                case "--thinking":
                    options = options with { IncludeThinking = true };
                    break;
                case "--no-thinking":
                    options = options with { IncludeThinking = false };
                    break;
                default:
                    if (token.StartsWith("--"))
                    {
                        name = "";
                        error = $"Unknown option '{token}'.";
                        return null;
                    }
                    nameParts.Add(token);
                    break;
            }
        }

        name = string.Join(' ', nameParts);
        return options;
    }
}
