using AgentSharpLib.Llm;

namespace AgentSharpLib.Agent;

/// <summary>
/// Manages the append-only conversation history.
/// This is the primary state of the agent -- no separate state machine needed.
/// Every LLM call, tool call, and result accumulates here.
/// </summary>
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
        => _messages.Count(m => m.Role == MessageRole.User && m.Content.OfType<TextBlock>().Any(b => b.Text.Length > 0));

    /// <summary>
    /// How many turns have been started: typed prompts and /save, /load checkpoints. Loop-generated
    /// (sys) prompts continue the turn they came from. Derived from the history, so a restored
    /// conversation carries its turn count with it.
    /// </summary>
    public int TurnCount
        => _messages.Count(m => m.Role == MessageRole.User && m.Kind != PromptKind.Sys
            && m.Content.OfType<TextBlock>().Any(b => b.Text.Length > 0));

    public void AddUserMessage(string text, PromptKind kind = PromptKind.Std)
        => Add(ChatMessage.UserText(text, kind));

    public void AddCompactionNote(string text)
        => Add(ChatMessage.AssistantCompaction(text));

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
        => _messages.Clear();

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

    /// <summary>Removes the first <paramref name="count"/> messages (the oldest). Used by /compact.</summary>
    public void DropFirst(int count)
    {
        if (count > 0)
            _messages.RemoveRange(0, Math.Min(count, _messages.Count));
    }

    /// <summary>
    /// Get the last assistant text response.
    /// </summary>
    public string? GetLastAssistantText()
        => _messages.LastOrDefault(m => m.Role == MessageRole.Assistant)?.GetText();
}
