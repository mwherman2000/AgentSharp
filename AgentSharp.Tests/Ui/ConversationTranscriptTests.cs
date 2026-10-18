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
    public void BuildQaPairs_NoThinking_OmitsThoughts()
    {
        var pairs = ConversationTranscript.BuildQaPairs(SampleHistory(), new TranscriptOptions(IncludeThinking: false));

        Assert.Equal(
            [new AnswerSegment(false, "Let me think."), new AnswerSegment(false, "It's 4.")],
            pairs[0].Segments);
    }

    [Fact]
    public void BuildQaPairs_IncludeToolCalls_RecordsCallsAndResultsInOrderWithoutThinkNoise()
    {
        var pairs = ConversationTranscript.BuildQaPairs(SampleHistory(), new TranscriptOptions(IncludeToolCalls: true));

        var segments = pairs[0].Segments;
        Assert.Equal(5, segments.Count);
        Assert.Equal(new AnswerSegment(true, "It's 4."), segments[1]);
        Assert.True(segments[2].IsTool);
        Assert.Contains("**Tool call:** `read_file`", segments[2].Text);
        Assert.Contains("{\"path\":\"x\"}", segments[2].Text);
        Assert.True(segments[3].IsTool);
        Assert.Contains("**Tool result:** `read_file`", segments[3].Text);
        Assert.Contains("file", segments[3].Text);
        Assert.DoesNotContain(segments, s => s.Text.Contains("`think`"));
    }

    [Fact]
    public void BuildQaPairs_ToolResult_MarksErrorsAndTruncatesLongOutput()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("go");
        history.AddAssistantMessage(new List<ContentBlock>
        {
            new ToolUseBlock { Id = "t1", Name = "run", Input = JsonSerializer.SerializeToElement(new { }) },
        });
        history.AddToolResults([new ToolResultBlock { ToolUseId = "t1", Content = new string('x', 5000) + "```", IsError = true }]);

        var result = ConversationTranscript.BuildQaPairs(history, new TranscriptOptions(IncludeToolCalls: true))[0].Segments[1];

        Assert.Contains("(error)", result.Text);
        Assert.Contains("truncated, 5,003 characters total", result.Text);
        Assert.True(result.Text.Length < 2300);
    }

    [Theory]
    [InlineData("chat", "chat", true, false)]
    [InlineData("chat --tools", "chat", true, true)]
    [InlineData("--no-thinking chat --TOOLS", "chat", false, true)]
    public void TranscriptOptions_Parse_SplitsNameFromFlags(string argument, string name, bool thinking, bool tools)
    {
        var options = TranscriptOptions.Parse(argument, out var parsedName, out var error);

        Assert.Null(error);
        Assert.Equal(name, parsedName);
        Assert.Equal(new TranscriptOptions(thinking, tools), options);
    }

    [Fact]
    public void TranscriptOptions_Parse_RejectsUnknownFlag()
    {
        Assert.Null(TranscriptOptions.Parse("chat --bogus", out _, out var error));
        Assert.Contains("--bogus", error);
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
    public void Write_IncludeToolCalls_AppearsInMarkdownAndDocxFiles()
    {
        var options = new TranscriptOptions(IncludeToolCalls: true);

        var md = File.ReadAllText(ConversationTranscript.Write(SampleHistory(), "prompt", _dir, "tools", options: options));
        Assert.Contains("**Tool call:** `read_file`", md);
        Assert.Contains("**Tool result:** `read_file`", md);

        var docxPath = ConversationTranscript.Write(SampleHistory(), "prompt", _dir, "tools.docx", options: options);
        using var archive = System.IO.Compression.ZipFile.OpenRead(docxPath);
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("Tool call:", xml);
        Assert.Contains("read_file", xml);
        Assert.Contains("Tool result:", xml);
        Assert.Contains("<w:sz w:val=\"20\"/>", xml); // tool text is 10pt

        var plainDocx = ConversationTranscript.Write(SampleHistory(), "prompt", _dir, "plain.docx");
        using var plainArchive = System.IO.Compression.ZipFile.OpenRead(plainDocx);
        using var plainReader = new StreamReader(plainArchive.GetEntry("word/document.xml")!.Open());
        Assert.DoesNotContain("<w:sz w:val=\"20\"/>", plainReader.ReadToEnd());

        var plain = File.ReadAllText(ConversationTranscript.Write(SampleHistory(), "prompt", _dir, "plain"));
        Assert.DoesNotContain("Tool call:", plain);
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
