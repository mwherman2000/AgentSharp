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

    public void AddUserMessage(string text)
        => Add(ChatMessage.UserText(text));

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

    /// <summary>
    /// Get the last assistant text response.
    /// </summary>
    public string? GetLastAssistantText()
        => _messages.LastOrDefault(m => m.Role == MessageRole.Assistant)?.GetText();
}
