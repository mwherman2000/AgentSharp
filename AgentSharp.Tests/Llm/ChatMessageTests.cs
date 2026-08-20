using System.Text.Json;
using AgentSharp.Llm;

namespace AgentSharp.Tests.Llm;

public class ChatMessageTests
{
    [Fact]
    public void UserText_CreatesCorrectMessage()
    {
        var msg = ChatMessage.UserText("hello");

        Assert.Equal(MessageRole.User, msg.Role);
        Assert.Single(msg.Content);
        Assert.IsType<TextBlock>(msg.Content[0]);
        Assert.Equal("hello", msg.GetText());
    }

    [Fact]
    public void AssistantText_CreatesCorrectMessage()
    {
        var msg = ChatMessage.AssistantText("response");

        Assert.Equal(MessageRole.Assistant, msg.Role);
        Assert.Equal("response", msg.GetText());
    }

    [Fact]
    public void HasToolUse_ReturnsFalse_ForTextOnly()
    {
        var msg = ChatMessage.UserText("hello");
        Assert.False(msg.HasToolUse);
    }

    [Fact]
    public void HasToolUse_ReturnsTrue_WhenToolUsePresent()
    {
        var msg = new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = new ContentBlock[]
            {
                new TextBlock { Text = "Let me check" },
                new ToolUseBlock
                {
                    Id = "tool-1",
                    Name = "read_file",
                    Input = JsonDocument.Parse("""{"path": "/test.txt"}""").RootElement
                }
            }
        };

        Assert.True(msg.HasToolUse);
    }

    [Fact]
    public void GetToolUses_ReturnsOnlyToolUseBlocks()
    {
        var msg = new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = new ContentBlock[]
            {
                new TextBlock { Text = "text" },
                new ToolUseBlock
                {
                    Id = "t1", Name = "read_file",
                    Input = JsonDocument.Parse("{}").RootElement
                },
                new TextBlock { Text = "more text" },
                new ToolUseBlock
                {
                    Id = "t2", Name = "grep",
                    Input = JsonDocument.Parse("{}").RootElement
                }
            }
        };

        var toolUses = msg.GetToolUses().ToList();
        Assert.Equal(2, toolUses.Count);
        Assert.Equal("read_file", toolUses[0].Name);
        Assert.Equal("grep", toolUses[1].Name);
    }

    [Fact]
    public void ToolResults_CreatesUserMessage()
    {
        var results = new[]
        {
            new ToolResultBlock { ToolUseId = "t1", Content = "result1", IsError = false },
            new ToolResultBlock { ToolUseId = "t2", Content = "error", IsError = true }
        };

        var msg = ChatMessage.ToolResults(results);

        Assert.Equal(MessageRole.User, msg.Role);
        Assert.Equal(2, msg.Content.Count);
    }

    [Fact]
    public void GetText_ConcatenatesMultipleTextBlocks()
    {
        var msg = new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = new ContentBlock[]
            {
                new TextBlock { Text = "Hello " },
                new TextBlock { Text = "World" }
            }
        };

        Assert.Equal("Hello World", msg.GetText());
    }

    [Fact]
    public void GetText_InsertsNewline_BetweenBlocksWithNoWhitespaceBoundary()
    {
        // Simulates text split across a tool call: the segment resuming after the
        // tool result doesn't start with a space, so a naive join would glue it onto
        // the end of the prior segment (e.g. "this one.Alright, look at that").
        var msg = new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = new ContentBlock[]
            {
                new TextBlock { Text = "...move fast on this one." },
                new TextBlock { Text = "Alright, look at that" }
            }
        };

        Assert.Equal("...move fast on this one.\nAlright, look at that", msg.GetText());
    }
}
