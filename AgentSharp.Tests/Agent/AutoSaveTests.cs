using System.Text.RegularExpressions;
using AgentSharpLib;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;

namespace AgentSharp.Tests.Agent;

/// <summary>The automatic saves: before and after /compact, and before a handoff.</summary>
public class AutoSaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "agentsharp_autosave_" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<string> _transcripts = [];

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        foreach (var t in Directory.GetFiles(Directory.GetCurrentDirectory(), "*-20??-??-??_??-??-??-*.docx")) File.Delete(t);
    }

    private static StreamEvent[] Reply(string text) => [new TextDelta(text), new StreamDone("end_turn")];

    private async Task<AgentSession> BuildAsync(FakeStreamingLlmClient llm, string persona = "ray", bool withSessions = true)
    {
        var builder = new AgentBuilder().WithLlmClient(llm).WithoutProjectScan().WithoutMemory().WithSuperPrompt(persona);
        if (withSessions) builder.WithSessionManager(new SessionManager(_dir));
        return await builder.BuildAsync();
    }

    private string[] SessionFiles(string pattern) => Directory.Exists(Path.Combine(_dir, "sessions"))
        ? Directory.GetFiles(Path.Combine(_dir, "sessions"), pattern).Select(f => Path.GetFileName(f)).Order().ToArray()
        : [];

    private static string[] Transcripts(string pattern) =>
        Directory.GetFiles(Directory.GetCurrentDirectory(), pattern).Select(f => Path.GetFileName(f)).Order().ToArray();

    [Fact]
    public void FileStamp_Is24HourWithTheZone_AndHasNoCharactersAWindowsFileNameCannotHold()
    {
        var stamp = LocalTime.FileStamp(new DateTimeOffset(2026, 10, 29, 15, 4, 5, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 29, 15, 4, 5))));

        Assert.Matches(@"^2026-10-29_15-04-05-[A-Z0-9]{2,5}$", stamp); // 15 = 3 pm on a 24-hour clock
        Assert.DoesNotContain(':', stamp);
        Assert.DoesNotContain(' ', stamp);
    }

    [Fact]
    public async Task SaveName_IsPersonaKindAndStamp_AndFollowsTheHandoffs()
    {
        var session = await BuildAsync(new FakeStreamingLlmClient(), "ray");

        Assert.Matches(@"^ray-saved-\d{4}-\d\d-\d\d_\d\d-\d\d-\d\d-[A-Z0-9]+$", session.SaveName("saved"));
        Assert.Matches(@"^ray-transcription-\d{4}-\d\d-\d\d_\d\d-\d\d-\d\d-[A-Z0-9]+$", session.SaveName("transcription")); // /transcribe with no name

        session.Handoff("donald");
        Assert.Matches(@"^donald-saved-\d{4}-\d\d-\d\d_\d\d-\d\d-\d\d-[A-Z0-9]+$", session.SaveName("saved"));
    }

    [Fact]
    public async Task SaveName_ForACustomSystemPrompt_UsesCustom()
    {
        var session = await new AgentBuilder().WithLlmClient(new FakeStreamingLlmClient())
            .WithoutProjectScan().WithoutMemory().WithSystemPrompt("x").BuildAsync();

        Assert.StartsWith("custom-saved-", session.SaveName("saved"));
    }

    [Fact]
    public async Task Handoff_SavesFirst_AsPersonaHandoffTarget_WithATranscript()
    {
        var llm = new FakeStreamingLlmClient().Enqueue(Reply("one"));
        var session = await BuildAsync(llm);
        await session.SendAsync("hi");

        session.Handoff("donald");

        var saved = SessionFiles("ray-handoff-donald-*.json");
        Assert.Single(saved);
        Assert.Matches(@"^ray-handoff-donald-\d{4}-\d\d-\d\d_\d\d-\d\d-\d\d-[A-Z0-9]+\.json$", saved[0]);
        Assert.Single(Transcripts("ray-handoff-donald-*.docx"));

        // Saved before the switch: the file holds the conversation as it was, still as ray, with no handoff record yet.
        var loaded = await new SessionManager(_dir).LoadAsync(Path.GetFileNameWithoutExtension(saved[0]));
        Assert.Equal(2, loaded!.Count);
        Assert.DoesNotContain(loaded.Messages, m => m.Kind == PromptKind.Hnd);
        Assert.Equal("donald", session.SuperPrompt);
    }

    [Fact]
    public async Task HandoffByTheTool_SavesAConsistentHistory_NotOneEndingInAnUnansweredToolCall()
    {
        var llm = new FakeStreamingLlmClient()
            .Enqueue(new ToolUseStart("h1", "handoff"), new ToolInputDelta("""{"persona":"donald","reason":"r"}"""), new ToolUseEnd(), new StreamDone("tool_use"))
            .Enqueue(Reply("Handing over."));
        var session = await BuildAsync(llm, "andy");

        await session.SendAsync("negotiate this");

        var saved = SessionFiles("andy-handoff-donald-*.json");
        Assert.Single(saved);
        var loaded = await new SessionManager(_dir).LoadAsync(Path.GetFileNameWithoutExtension(saved[0]));
        Assert.Equal(4, loaded!.Count); // user, tool_use, tool_result, final answer
        Assert.Equal("Handing over.", loaded.GetLastAssistantText());
    }

    [Theory]
    [InlineData("ray")]      // already in charge: nothing happens
    [InlineData("nobody")]   // unknown: rejected before anything changes
    public async Task Handoff_ThatDoesNothing_SavesNothing(string target)
    {
        var session = await BuildAsync(new FakeStreamingLlmClient());

        try { session.Handoff(target); } catch (ArgumentException) { }

        Assert.Empty(SessionFiles("*.json"));
        Assert.Empty(Transcripts("*-20??-??-??_??-??-??-*.docx"));
    }

    [Fact]
    public async Task WithoutASessionManager_NothingIsSaved_AndHandoffStillWorks()
    {
        var session = await BuildAsync(new FakeStreamingLlmClient(), withSessions: false);

        session.Handoff("donald");

        Assert.Equal("donald", session.SuperPrompt);
        Assert.Null(session.AutoSave("anything", withTranscript: true));
    }

    [Fact]
    public async Task Compact_SavesBeforeWithATranscript_AndAfterWithout()
    {
        var llm = new FakeStreamingLlmClient();
        for (var i = 0; i < 4; i++) llm.Enqueue(Reply($"answer {i}"));
        var session = await BuildAsync(llm);
        for (var i = 0; i < 4; i++) await session.SendAsync($"question {i}");

        var outcome = session.Compact(50, dropOldest: true);

        Assert.True(outcome.Result!.TurnsDropped > 0);
        Assert.Matches(@"^ray-compact-before-\d{4}-\d\d-\d\d_\d\d-\d\d-\d\d-[A-Z0-9]+$", outcome.Before!.SessionId!);
        Assert.Matches(@"^ray-compact-after-\d{4}-\d\d-\d\d_\d\d-\d\d-\d\d-[A-Z0-9]+$", outcome.After!.SessionId!);
        Assert.NotNull(outcome.Before.TranscriptPath);
        Assert.Null(outcome.After.TranscriptPath);
        Assert.Single(Transcripts("ray-compact-before-*.docx"));
        Assert.Empty(Transcripts("ray-compact-after-*.docx"));

        // Before holds every turn; after holds the shortened conversation and the compaction record.
        var sessions = new SessionManager(_dir);
        var before = await sessions.LoadAsync(outcome.Before.SessionId!);
        var after = await sessions.LoadAsync(outcome.After.SessionId!);
        Assert.Equal(8, before!.Count);
        Assert.True(after!.Count < before.Count + 1);
        Assert.Equal(PromptKind.Cmp, after.Messages[^1].Kind);
    }

    [Fact]
    public async Task Compact_WithNothingToDrop_SavesOnlyBefore()
    {
        var session = await BuildAsync(new FakeStreamingLlmClient());

        var outcome = session.Compact(50, dropOldest: true);

        Assert.Equal(0, outcome.Result!.TurnsDropped);
        Assert.NotNull(outcome.Before);
        Assert.Null(outcome.After);
        Assert.Empty(SessionFiles("ray-compact-after-*.json"));
    }

    [Fact]
    public async Task Compact_WithoutASessionManager_StillCompacts()
    {
        var llm = new FakeStreamingLlmClient();
        for (var i = 0; i < 4; i++) llm.Enqueue(Reply($"answer {i}"));
        var session = await BuildAsync(llm, withSessions: false);
        for (var i = 0; i < 4; i++) await session.SendAsync($"question {i}");

        var outcome = session.Compact(50, dropOldest: true);

        Assert.True(outcome.Result!.TurnsDropped > 0);
        Assert.Null(outcome.Before);
        Assert.Null(outcome.After);
    }
}
