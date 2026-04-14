using AgentSharp.Llm;

namespace AgentSharp.Agent;

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
        => _messages.Add(ChatMessage.UserText(text));

    public void AddAssistantMessage(ChatMessage message)
        => _messages.Add(message);

    public void AddAssistantMessage(IReadOnlyList<ContentBlock> content)
        => _messages.Add(new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = content
        });

    public void AddToolResults(IEnumerable<ToolResultBlock> results)
        => _messages.Add(ChatMessage.ToolResults(results));

    public void Clear()
        => _messages.Clear();

    /// <summary>
    /// Get the last assistant text response.
    /// </summary>
    public string? GetLastAssistantText()
        => _messages.LastOrDefault(m => m.Role == MessageRole.Assistant)?.GetText();
}
