using AgentSharpLib.Llm;

namespace AgentSharpLib.Agent;

/// <summary>
/// Manages the append-only conversation history.
/// This is the primary state of the agent -- no separate state machine needed.
/// Every LLM call, tool call, and result accumulates here.
/// </summary>
public enum TokenCountKind { Estimated, Calibrated, Actual }

public class ConversationHistory
{
    private readonly List<ChatMessage> _messages = new();

    public IReadOnlyList<ChatMessage> Messages => _messages;
    public int Count => _messages.Count;

    /// <summary>
    /// How many questions the user has asked so far -- user messages with typed text,
    /// not tool results. It is the number of Q&amp;A pairs a transcript would show, so
    /// the next question is <c>QuestionCount + 1</c>.
    /// </summary>
    public int QuestionCount
        => PromptOffset + _messages.Count(IsPrompt);

    /// <summary>
    /// Prompts (typed, loop-generated and checkpoint) that /compact has dropped from the front of
    /// this history. Prompt numbers keep counting from here, so they never go backwards.
    /// </summary>
    public int PromptOffset { get; private set; }

    /// <summary>Turns that /compact has dropped from the front of this history.</summary>
    public int TurnOffset { get; private set; }

    private static bool IsPrompt(ChatMessage m)
        => m.Role == MessageRole.User && m.Content.OfType<TextBlock>().Any(b => b.Text.Length > 0);

    /// <summary>Puts back the counts saved with a session (zero for a session never compacted).</summary>
    public void RestoreOffsets(int prompts, int turns)
    {
        PromptOffset = Math.Max(prompts, 0);
        TurnOffset = Math.Max(turns, 0);
    }

    private long _realPromptTokens;
    private long _estimatedTokensThen;

    /// <summary>
    /// Records the provider's own token count for the prompt just sent (this history as it stood), so
    /// <see cref="TokenScale"/> can turn the character-based estimate into a real figure.
    /// </summary>
    public void RecordPromptUsage(long realPromptTokens)
    {
        var estimate = HistoryCompactor.EstimateTokens(_messages);
        if (realPromptTokens <= 0 || estimate <= 0) return;
        _realPromptTokens = realPromptTokens;
        _estimatedTokensThen = estimate;
        _countAtRecord = _messages.Count;
    }

    private int _countAtRecord = -1;

    /// <summary>
    /// How big the conversation is in tokens: the provider's own count of the last request when nothing
    /// has been added since (Actual), that count scaled to the history as it is now (Calibrated), or the
    /// characters / 4 estimate when no real count has been seen yet (Estimated). The real count includes
    /// the system prompt and tool definitions; the estimate covers the messages only.
    /// </summary>
    public (long Tokens, TokenCountKind Kind) ContextTokens()
    {
        var estimate = HistoryCompactor.EstimateTokens(_messages);
        if (TokenScale is not { } scale) return (estimate, TokenCountKind.Estimated);
        if (_messages.Count == _countAtRecord) return (_realPromptTokens, TokenCountKind.Actual);
        return ((long)(estimate * scale), TokenCountKind.Calibrated);
    }

    /// <summary>Real tokens per estimated token, or null until a real count has been recorded.</summary>
    public double? TokenScale => _estimatedTokensThen > 0 ? (double)_realPromptTokens / _estimatedTokensThen : null;

    /// <summary>
    /// How many turns have been started: typed prompts and /save, /load checkpoints. Loop-generated
    /// (sys) prompts continue the turn they came from. Derived from the history, so a restored
    /// conversation carries its turn count with it.
    /// </summary>
    public int TurnCount
        => TurnOffset + _messages.Count(m => IsPrompt(m) && m.Kind != PromptKind.Sys);

    public void AddUserMessage(string text, PromptKind kind = PromptKind.Std)
        => Add(ChatMessage.UserText(text, kind));

    public void AddCompactionNote(string text)
        => Add(ChatMessage.AssistantCompaction(text));

    public void AddHandoffRecord(string text)
        => Add(ChatMessage.AssistantHandoff(text));

    public void AddAssistantError(string text)
        => Add(ChatMessage.AssistantError(text));

    public void AddAssistantMessage(ChatMessage message)
        => Add(message);

    public void AddAssistantMessage(IReadOnlyList<ContentBlock> content)
        => Add(new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = content
        });

    public void AddToolResults(IEnumerable<ToolResultBlock> results)
        => Add(ChatMessage.ToolResults(results));

    /// <summary>Appends a new message, stamping it with the current time unless it already has one.</summary>
    private void Add(ChatMessage message)
        => _messages.Add(message.Timestamp is null ? message with { Timestamp = DateTimeOffset.Now } : message);

    /// <summary>
    /// Appends a message read back from a saved session exactly as it was, keeping its
    /// original timestamp -- or none, for a session saved before timestamps were recorded.
    /// </summary>
    public void AddRestored(ChatMessage message)
        => _messages.Add(message);

    public void Clear()
    {
        _messages.Clear();
        PromptOffset = TurnOffset = 0;
        _realPromptTokens = _estimatedTokensThen = 0;
        _countAtRecord = -1;
    }

    /// <summary>
    /// Removes every message added after <paramref name="count"/>. Used to roll
    /// back a turn that was cancelled partway through (e.g. Ctrl+C), so the
    /// interrupted user message and any partial assistant/tool-result messages
    /// don't linger in history.
    /// </summary>
    public void TruncateTo(int count)
    {
        if (count < _messages.Count)
            _messages.RemoveRange(count, _messages.Count - count);
    }

    /// <summary>Removes the first <paramref name="count"/> messages (the oldest), keeping prompt and turn numbering going. Used by /compact.</summary>
    public void DropFirst(int count)
    {
        if (count <= 0) return;
        count = Math.Min(count, _messages.Count);
        var dropped = _messages.Take(count).Where(IsPrompt).ToList();
        PromptOffset += dropped.Count;
        TurnOffset += dropped.Count(m => m.Kind != PromptKind.Sys);
        _messages.RemoveRange(0, count);
        _countAtRecord = -1;
    }

    /// <summary>
    /// Get the last assistant text response.
    /// </summary>
    public string? GetLastAssistantText()
        => _messages.LastOrDefault(m => m.Role == MessageRole.Assistant && m.Kind != PromptKind.Hnd)?.GetText();
}
