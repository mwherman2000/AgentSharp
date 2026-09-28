using System.Text.Json;
using AgentSharpLib.Agent;
using AgentSharpLib.Llm;
using AgentSharpLib.Transcripts;

namespace AgentSharp.Tests.Ui;

public class ConversationTranscriptTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "agentsharp-transcript-" + Guid.NewGuid().ToString("N"));

    public ConversationTranscriptTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ConversationHistory SampleHistory()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("what is 2+2?");
        history.AddAssistantMessage(new List<ContentBlock>
        {
            new TextBlock { Text = "Let me think." },
            new ToolUseBlock { Id = "t1", Name = "think", Input = JsonSerializer.SerializeToElement(new { thought = "It's 4." }) },
            new ToolUseBlock { Id = "t2", Name = "read_file", Input = JsonSerializer.SerializeToElement(new { path = "x" }) },
        });
        history.AddToolResults([new ToolResultBlock { ToolUseId = "t1", Content = "ok" }, new ToolResultBlock { ToolUseId = "t2", Content = "file" }]);
        history.AddAssistantMessage(new List<ContentBlock> { new TextBlock { Text = "It's 4." } });
        history.AddUserMessage("thanks");
        return history;
    }

    [Fact]
    public void BuildQaPairs_MergesAssistantMessagesAndSkipsToolNoise()
    {
        var pairs = ConversationTranscript.BuildQaPairs(SampleHistory());

        Assert.Equal(2, pairs.Count);
        Assert.Equal("What is 2+2?", pairs[0].Question); // capitalized
        Assert.Equal(
            [new AnswerSegment(false, "Let me think."), new AnswerSegment(true, "It's 4."), new AnswerSegment(false, "It's 4.")],
            pairs[0].Segments);
        Assert.Equal("Thanks", pairs[1].Question);
        Assert.Empty(pairs[1].Segments);
    }

    [Fact]
    public void Write_DefaultsToMarkdownAndRecordsFirstLineOfSystemPrompt()
    {
        var path = ConversationTranscript.Write(SampleHistory(), "I am Lucy.\nMore details.", _dir, "chat");

        Assert.Equal(Path.Combine(_dir, "chat.md"), path);
        var text = File.ReadAllText(path);
        Assert.Contains("I am Lucy.", text);
        Assert.DoesNotContain("More details.", text);
        Assert.Contains("What is 2+2?", text);
    }

    [Fact]
    public void Write_DocxExtension_WritesWordDocument()
    {
        var path = ConversationTranscript.Write(SampleHistory(), "prompt", _dir, "chat.docx");

        Assert.Equal(Path.Combine(_dir, "chat.docx"), path);
        Assert.Equal("PK"u8.ToArray(), File.ReadAllBytes(path)[..2]); // zip container
    }

    [Fact]
    public void Write_IgnoresDirectoryPortionOfName()
    {
        var path = ConversationTranscript.Write(SampleHistory(), "prompt", _dir, "../escape");

        Assert.Equal(Path.Combine(_dir, "escape.md"), path);
    }

    [Fact]
    public void Write_NameWithNoFileName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ConversationTranscript.Write(SampleHistory(), "prompt", _dir, "somedir/"));
    }
}
