using AgentSharpLib.Agent;
using AgentSharpLib.Llm;
using AgentSharpLib.Memory;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;
using AgentSharpLib.Transcripts;

namespace AgentSharp.Tests.Agent;

/// <summary>
/// /status is recorded in the history (kind Sts, shown as T21 in a transcript) so that /save keeps it and a
/// transcript shows it. Like the /compact and handoff records it is never sent to the model and changes
/// nothing about the size of the conversation.
/// </summary>
public class StatusRecordTests : IDisposable
{
    private const string Listing = "Provider: Anthropic\nModel: claude-sonnet-5-5\nSuperprompt: Andy Pando\nHandoff stack: (empty)\nTools: 12 (a, b)";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "agentsharp_status_" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private static ConversationHistory AskedAndAnswered()
    {
        var h = new ConversationHistory();
        h.AddUserMessage("who are you");
        h.AddAssistantMessage(ChatMessage.AssistantText("I'm Andy."));
        return h;
    }

    [Fact]
    public void TheRecord_IsAnAssistantRecord_ThatIsNotAPrompt()
    {
        var h = AskedAndAnswered();

        h.AddStatusRecord(Listing);

        Assert.Equal(PromptKind.Sts, h.Messages[^1].Kind);
        Assert.Equal(MessageRole.Assistant, h.Messages[^1].Role);
        Assert.Equal(1, h.QuestionCount);
        Assert.Equal(1, h.TurnCount);
        Assert.Equal("I'm Andy.", h.GetLastAssistantText());   // the listing is not "the last answer"
    }

    [Fact]
    public async Task TheRecord_IsNeverSentToTheModel()
    {
        var llm = new FakeStreamingLlmClient().Enqueue(new TextDelta("ok"), new StreamDone("end_turn"));
        var history = AskedAndAnswered();
        history.AddStatusRecord(Listing);
        var loop = new AgentLoop(llm, new ToolRegistry(), new ApprovalGate(), "system prompt", history);

        await loop.RunTurnStreamingAsync("next");

        Assert.DoesNotContain(llm.Requests[0].Messages, m => m.Kind == PromptKind.Sts);
        Assert.DoesNotContain(llm.Requests[0].Messages, m => m.GetText().Contains("Superprompt:"));
    }

    [Fact]
    public void TheRecord_DoesNotChangeTheContextSize_OrTurnTheProvidersCountIntoAnEstimate()
    {
        var h = AskedAndAnswered();
        h.RecordPromptUsage(15_000, overheadTokens: 14_000);
        Assert.Equal((15_000L, TokenCountKind.Actual), h.ContextTokens());

        h.AddStatusRecord(Listing);

        Assert.Equal((15_000L, TokenCountKind.Actual), h.ContextTokens());   // still the provider's exact count
        Assert.Equal(HistoryCompactor.EstimateTokens(AskedAndAnswered().Messages), HistoryCompactor.EstimateTokens(h.Messages));
    }

    [Fact]
    public async Task TheRecord_SurvivesSaveAndLoad()
    {
        var h = AskedAndAnswered();
        h.AddStatusRecord(Listing);

        var sessions = new SessionManager(_dir);
        await sessions.SaveAsync(h, "s");
        var loaded = await sessions.LoadAsync("s");

        Assert.Equal(PromptKind.Sts, loaded!.Messages[^1].Kind);
        Assert.Equal(Listing, loaded.Messages[^1].GetText());
        Assert.Equal(1, loaded.QuestionCount);
    }

    [Fact]
    public void Transcript_ShowsTheListingAsATEntry_WithThePromptsNumber_AndItsLinesIntact()
    {
        var h = AskedAndAnswered();
        h.AddStatusRecord(Listing);
        h.AddUserMessage("and now?");
        h.AddAssistantMessage(ChatMessage.AssistantText("Fine."));
        Directory.CreateDirectory(_dir);

        var path = ConversationTranscript.Write(h, "You are Andy.", _dir, "t.md");
        var text = File.ReadAllText(path).Replace("\r\n", "\n");

        Assert.Contains("## T1\n", text);                                        // shares the number of the prompt it follows
        Assert.Contains("```\n" + Listing + "\n```", text);                      // one line per row, not run together
        Assert.Contains("## Q2\n", text);                                        // and used up no prompt number
        Assert.DoesNotContain("## Q3", text);
        var afterListing = text[text.IndexOf("## T1", StringComparison.Ordinal)..];
        Assert.True(afterListing.IndexOf("## A1", StringComparison.Ordinal) < 0
                    || afterListing.IndexOf("## A1", StringComparison.Ordinal) > afterListing.IndexOf("## Q2", StringComparison.Ordinal)); // no answer part under it
    }

    [Fact]
    public void Transcript_AsDocx_BuildsWithTheListing()
    {
        var h = AskedAndAnswered();
        h.AddStatusRecord(Listing);
        Directory.CreateDirectory(_dir);

        var path = ConversationTranscript.Write(h, "You are Andy.", _dir, "t.docx");

        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void Transcript_AsDocx_SetsTheListingAt10pt_InAMonospaceFont_WithoutALanguageLabel()
    {
        var h = AskedAndAnswered();
        h.AddStatusRecord(Listing);
        Directory.CreateDirectory(_dir);
        var path = ConversationTranscript.Write(h, "You are Andy.", _dir, "t.docx");

        string xml;
        using (var zip = System.IO.Compression.ZipFile.OpenRead(path))
        using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            xml = reader.ReadToEnd();

        var at = xml.IndexOf("Provider: Anthropic", StringComparison.Ordinal);
        Assert.True(at > 0, "the listing is in the document");
        var runStart = xml.LastIndexOf("<w:r>", at, StringComparison.Ordinal);
        var run = xml.Substring(runStart, at - runStart);
        Assert.Contains("<w:sz w:val=\"20\"/>", run);          // 10pt, in half-points
        Assert.Contains("Consolas", run);                      // like any code block
        Assert.DoesNotContain(">text<", xml);                  // no stray "text" label from the code fence

        // The reply text above it keeps the default size.
        var reply = xml.IndexOf("I&#39;m Andy.", StringComparison.Ordinal);
        if (reply < 0) reply = xml.IndexOf("I'm Andy.", StringComparison.Ordinal);
        Assert.True(reply > 0);
        var replyRun = xml.Substring(xml.LastIndexOf("<w:r>", reply, StringComparison.Ordinal), reply - xml.LastIndexOf("<w:r>", reply, StringComparison.Ordinal));
        Assert.DoesNotContain("<w:sz w:val=\"20\"/>", replyRun);
    }
}
