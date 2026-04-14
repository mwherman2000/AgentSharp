using AgentSharp.Agent;
using AgentSharp.Llm;

namespace AgentSharp.Tests.Agent;

public class ConversationHistoryTests
{
    [Fact]
    public void AddUserMessage_IncreasesCount()
    {
        var history = new ConversationHistory();
        Assert.Equal(0, history.Count);

        history.AddUserMessage("hello");
        Assert.Equal(1, history.Count);
    }

    [Fact]
    public void AddUserMessage_CreatesCorrectRole()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("hello");

        Assert.Equal(MessageRole.User, history.Messages[0].Role);
        Assert.Equal("hello", history.Messages[0].GetText());
    }

    [Fact]
    public void AddAssistantMessage_CreatesCorrectRole()
    {
        var history = new ConversationHistory();
        history.AddAssistantMessage(ChatMessage.AssistantText("response"));

        Assert.Equal(MessageRole.Assistant, history.Messages[0].Role);
        Assert.Equal("response", history.Messages[0].GetText());
    }

    [Fact]
    public void AddToolResults_CreatesUserRole()
    {
        var history = new ConversationHistory();
        var results = new[]
        {
            new ToolResultBlock { ToolUseId = "test-id", Content = "result", IsError = false }
        };

        history.AddToolResults(results);

        Assert.Equal(MessageRole.User, history.Messages[0].Role);
        Assert.Single(history.Messages[0].Content.OfType<ToolResultBlock>());
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("1");
        history.AddUserMessage("2");
        Assert.Equal(2, history.Count);

        history.Clear();
        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void GetLastAssistantText_ReturnsLatest()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("q1");
        history.AddAssistantMessage(ChatMessage.AssistantText("a1"));
        history.AddUserMessage("q2");
        history.AddAssistantMessage(ChatMessage.AssistantText("a2"));

        Assert.Equal("a2", history.GetLastAssistantText());
    }

    [Fact]
    public void GetLastAssistantText_ReturnsNull_WhenEmpty()
    {
        var history = new ConversationHistory();
        Assert.Null(history.GetLastAssistantText());
    }

    [Fact]
    public void MessagesAreAppendOnly()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("first");
        history.AddAssistantMessage(ChatMessage.AssistantText("second"));
        history.AddUserMessage("third");

        Assert.Equal(3, history.Count);
        Assert.Equal("first", history.Messages[0].GetText());
        Assert.Equal("second", history.Messages[1].GetText());
        Assert.Equal("third", history.Messages[2].GetText());
    }
}
