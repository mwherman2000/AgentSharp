using AgentSharpLib.Agent;
using AgentSharpLib.Memory;

namespace AgentSharp.Tests.Memory;

public class SessionManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SessionManager _manager;

    public SessionManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsharp_test_" + Guid.NewGuid().ToString("N")[..8]);
        _manager = new SessionManager(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("hello");
        history.AddAssistantMessage(AgentSharpLib.Llm.ChatMessage.AssistantText("hi there"));
        history.AddUserMessage("how are you?");

        var sessionId = await _manager.SaveAsync(history, "test-session");
        Assert.Equal("test-session", sessionId);

        var loaded = await _manager.LoadAsync("test-session");
        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.Count);
    }

    [Fact]
    public async Task Load_ReturnsNull_ForMissingSession()
    {
        var result = await _manager.LoadAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task Exists_IsFalseBeforeSaving_TrueAfter_AndCleansTheIdLikeSaveDoes()
    {
        Assert.False(_manager.Exists("mine"));

        var history = new ConversationHistory();
        history.AddUserMessage("hello");
        await _manager.SaveAsync(history, "mine");

        Assert.True(_manager.Exists("mine"));
        Assert.True(_manager.Exists("../../mine")); // the same file Save would write
        Assert.False(_manager.Exists("other"));
        Assert.False(_manager.Exists(""));
    }

    [Fact]
    public async Task ListSessions_ReportsQuestions_NotMessages_IncludingOnesDroppedByCompact()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("first");
        history.AddAssistantMessage([new AgentSharpLib.Llm.TextBlock { Text = "a" }]);
        history.AddToolResults([new AgentSharpLib.Llm.ToolResultBlock { ToolUseId = "t", Content = "x" }]); // a user message, but not a question
        history.AddUserMessage("nudge", AgentSharpLib.Llm.PromptKind.Sys);                                  // counts, like any prompt
        history.AddUserMessage("second");
        history.RestoreOffsets(prompts: 10, turns: 10);                                                      // 10 more were compacted away
        await _manager.SaveAsync(history, "counted");

        var info = Assert.Single(_manager.ListSessions());

        Assert.Equal(5, info.MessageCount);
        Assert.Equal(13, info.QuestionCount); // 10 dropped + first + nudge + second
        Assert.Equal(history.QuestionCount, info.QuestionCount); // the same number /load and the prompt show
    }

    [Fact]
    public async Task ListSessions_ReturnsAllSaved()
    {
        var h1 = new ConversationHistory();
        h1.AddUserMessage("hello");
        await _manager.SaveAsync(h1, "session-a");

        var h2 = new ConversationHistory();
        h2.AddUserMessage("world");
        await _manager.SaveAsync(h2, "session-b");

        var sessions = _manager.ListSessions();
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public void ListSessions_SortsMostRecentFirst()
    {
        // Names sort opposite to dates, so a name-based order would fail.
        var sessionsDir = Path.Combine(_tempDir, "sessions");
        WriteSession(sessionsDir, "a-newest", new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc));
        WriteSession(sessionsDir, "m-oldest", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteSession(sessionsDir, "z-middle", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var ids = _manager.ListSessions().Select(s => s.Id).ToList();

        Assert.Equal(["a-newest", "z-middle", "m-oldest"], ids);
    }

    private static void WriteSession(string dir, string id, DateTime createdAt) =>
        File.WriteAllText(Path.Combine(dir, $"{id}.json"),
            $$"""{"id":"{{id}}","created_at":"{{createdAt:O}}","message_count":0,"messages":[]}""");

    [Fact]
    public async Task Save_GeneratesId_WhenNotProvided()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("test");

        var sessionId = await _manager.SaveAsync(history);
        Assert.NotNull(sessionId);
        Assert.Equal(8, sessionId.Length);
    }

    [Theory]
    [InlineData("../../evil")]
    [InlineData("/evil")]
    [InlineData(@"..\..\evil")]
    public async Task Save_SanitizesId_CannotEscapeSessionsDirectory(string maliciousId)
    {
        var history = new ConversationHistory();
        history.AddUserMessage("hello");

        var sessionId = await _manager.SaveAsync(history, maliciousId);

        Assert.NotNull(sessionId);
        Assert.Equal("evil", sessionId);
        Assert.True(File.Exists(Path.Combine(_tempDir, "sessions", "evil.json")));
    }

    [Theory]
    [InlineData("../../evil")]
    [InlineData("/evil")]
    public async Task Load_SanitizesId_CannotEscapeSessionsDirectory(string maliciousId)
    {
        var history = new ConversationHistory();
        history.AddUserMessage("hello");
        await _manager.SaveAsync(history, "evil");

        var loaded = await _manager.LoadAsync(maliciousId);

        Assert.NotNull(loaded);
    }
}
