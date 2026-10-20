using System.Text.Json;
using AgentSharpLib.Agent;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;
using AgentSharpLib.Transcripts;

namespace AgentSharp.Tests.Ui;

public class MessageTimestampTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "agentsharp-timestamps-" + Guid.NewGuid().ToString("N"));

    public MessageTimestampTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void History_StampsNewMessages_ButKeepsRestoredOnesAsTheyWere()
    {
        var before = DateTimeOffset.Now;
        var history = new ConversationHistory();
        history.AddUserMessage("hi");
        history.AddAssistantMessage([new TextBlock { Text = "hello" }]);
        history.AddRestored(ChatMessage.UserText("old"));

        Assert.All(history.Messages.Take(2), m => Assert.InRange(m.Timestamp!.Value, before, DateTimeOffset.Now));
        Assert.Null(history.Messages[2].Timestamp);
    }

    [Fact]
    public void QuestionCount_CountsTypedQuestions_NotToolResultsOrReplies()
    {
        var history = new ConversationHistory();
        Assert.Equal(0, history.QuestionCount);

        history.AddUserMessage("one");
        history.AddAssistantMessage([new TextBlock { Text = "a" }]);
        history.AddToolResults([new ToolResultBlock { ToolUseId = "t", Content = "ok" }]);
        history.AddUserMessage("two");

        Assert.Equal(2, history.QuestionCount); // the next prompt is "3 <agent>>"
    }

    [Fact]
    public async Task SavedSession_RoundTripsTimestamps()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("question");
        history.AddAssistantMessage([new TextBlock { Text = "answer" }]);
        var sessions = new SessionManager(_dir);

        await sessions.SaveAsync(history, "s1");
        var loaded = await sessions.LoadAsync("s1");

        Assert.NotNull(loaded);
        Assert.Equal(history.Messages.Select(m => m.Timestamp), loaded!.Messages.Select(m => m.Timestamp));
    }

    [Fact]
    public async Task SessionSavedBeforeTimestamps_LoadsWithoutThem()
    {
        var sessionsDir = Path.Combine(_dir, "sessions");
        Directory.CreateDirectory(sessionsDir);
        await File.WriteAllTextAsync(Path.Combine(sessionsDir, "old.json"),
            """{"id":"old","created_at":"2026-10-01T10:00:00Z","message_count":1,"messages":[{"role":"user","content":[{"type":"text","text":"old question"}]}]}""");

        var loaded = await new SessionManager(_dir).LoadAsync("old");

        Assert.Null(loaded!.Messages[0].Timestamp);
    }

    [Fact]
    public void Transcript_ShowsWhenEachQuestionWasAsked_AndOmitsItWhenUnknown()
    {
        var asked = new DateTimeOffset(2026, 10, 17, 19, 12, 9, TimeSpan.Zero);
        var history = new ConversationHistory();
        history.AddRestored(ChatMessage.UserText("first") with { Timestamp = asked });
        history.AddRestored(ChatMessage.AssistantText("a1"));
        history.AddRestored(ChatMessage.UserText("second"));
        history.AddRestored(ChatMessage.AssistantText("a2"));

        var md = File.ReadAllText(ConversationTranscript.Write(history, "prompt", _dir, "t"));

        var expected = $"_{asked.ToLocalTime():yyyy-MM-dd HH:mm:ss}_";
        Assert.Contains($"## Q1\n\n{expected}\n\nFirst", md.Replace("\r\n", "\n"));
        Assert.Equal(1, md.Split(expected).Length - 1);
        Assert.Contains("## Q2\n\nSecond", md.Replace("\r\n", "\n")); // no stamp line

        var docxPath = ConversationTranscript.Write(history, "prompt", _dir, "t.docx");
        using var archive = System.IO.Compression.ZipFile.OpenRead(docxPath);
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        Assert.Contains(expected.Trim('_'), reader.ReadToEnd());
    }

    [Fact]
    public async Task PromptKinds_AreCounted_Saved_AndLabelledInTheTranscript()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("typed");
        history.AddAssistantMessage([new TextBlock { Text = "a1" }]);
        history.AddUserMessage("The previous request failed with an error.", kind: PromptKind.Sys);
        history.AddAssistantMessage([new TextBlock { Text = "a2" }]);
        history.AddUserMessage("Checkpoint (/save) 2026-10-19 10:00:00", PromptKind.Chk);
        history.AddAssistantMessage([new TextBlock { Text = "a2b" }]);
        history.AddUserMessage("typed again");
        history.AddAssistantMessage([new TextBlock { Text = "a3" }]);

        Assert.Equal(4, history.QuestionCount); // sys prompts number like any other

        var sessions = new SessionManager(_dir);
        await sessions.SaveAsync(history, "s2");
        var json = await File.ReadAllTextAsync(Path.Combine(_dir, "sessions", "s2.json"));
        var kinds = JsonDocument.Parse(json).RootElement.GetProperty("messages").EnumerateArray()
            .Where(m => m.TryGetProperty("kind", out _)).Select(m => m.GetProperty("kind").GetString());
        Assert.Equal(["std", "sys", "chk", "std"], kinds);

        var loaded = await sessions.LoadAsync("s2");
        Assert.Equal([PromptKind.Std, PromptKind.Sys, PromptKind.Chk, PromptKind.Std], loaded!.Messages.Where(m => m.Role == MessageRole.User).Select(m => m.Kind));

        var md = File.ReadAllText(ConversationTranscript.Write(loaded, "prompt", _dir, "t2")).Replace("\r\n", "\n");
        Assert.Contains("## Q1\n", md);
        Assert.Contains("## S2\n", md);
        Assert.Contains("## C3\n", md);
        Assert.Contains("## Q4\n", md);
    }

    [Fact]
    public async Task HandEditedSession_WithSysAndChkKinds_LoadsAndShowsSAndCInTheTranscript()
    {
        var sessionsDir = Path.Combine(_dir, "sessions");
        Directory.CreateDirectory(sessionsDir);
        await File.WriteAllTextAsync(Path.Combine(sessionsDir, "edited.json"), """
            {"id":"edited","created_at":"2026-10-01T10:00:00Z","message_count":6,"messages":[
              {"role":"user","kind":"std","content":[{"type":"text","text":"typed"}]},
              {"role":"assistant","content":[{"type":"text","text":"a1"}]},
              {"role":"user","kind":"sys","content":[{"type":"text","text":"The previous request failed with an error."}]},
              {"role":"assistant","content":[{"type":"text","text":"a2"}]},
              {"role":"user","kind":"chk","content":[{"type":"text","text":"Resumed (/load) 2026-10-19 10:00:00"}]},
              {"role":"assistant","content":[{"type":"text","text":"a3"}]}]}
            """);

        var loaded = await new SessionManager(_dir).LoadAsync("edited");

        Assert.Equal([PromptKind.Std, PromptKind.Sys, PromptKind.Chk],
            loaded!.Messages.Where(m => m.Role == MessageRole.User).Select(m => m.Kind));
        var md = File.ReadAllText(ConversationTranscript.Write(loaded, "prompt", _dir, "t3")).Replace("\r\n", "\n");
        Assert.Contains("## Q1\n", md);
        Assert.Contains("## S2\n", md);
        Assert.Contains("## C3\n", md);
    }
}
