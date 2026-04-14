using AgentSharp.Agent;
using AgentSharp.Memory;

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
        history.AddAssistantMessage(AgentSharp.Llm.ChatMessage.AssistantText("hi there"));
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
    public async Task Save_GeneratesId_WhenNotProvided()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("test");

        var sessionId = await _manager.SaveAsync(history);
        Assert.NotNull(sessionId);
        Assert.Equal(8, sessionId.Length);
    }
}
