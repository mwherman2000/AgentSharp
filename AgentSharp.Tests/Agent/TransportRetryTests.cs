using System.Net;
using AgentSharpLib.Agent;
using AgentSharpLib.Llm;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;

namespace AgentSharp.Tests.Agent;

/// <summary>
/// A request that never got a response (a dropped connection) is sent again unchanged; it is not
/// reported to the model as a failed approach. Seen when a /save checkpoint died with "An existing
/// connection was forcibly closed by the remote host" and the model, told to "try a different approach",
/// wrote an unrelated memory entry instead of answering.
/// </summary>
public class TransportRetryTests
{
    private static HttpRequestException ConnectionReset() =>
        new("Error while copying content to a stream.", new IOException("An existing connection was forcibly closed by the remote host."));

    private static AgentLoop NewLoop(ILlmClient llm) => new(llm, new ToolRegistry(), new ApprovalGate(), "system prompt");

    private static List<ChatMessage> Prompts(AgentLoop loop) =>
        loop.History.Messages.Where(m => m.Role == MessageRole.User && m.Content.OfType<TextBlock>().Any()).ToList();

    [Fact]
    public async Task Streaming_ConnectionReset_ResendsTheSameRequest_WithoutANudge()
    {
        var llm = new FakeStreamingLlmClient()
            .EnqueueThrow(ConnectionReset())
            .Enqueue(new TextDelta("Done."), new StreamDone("end_turn"));
        var loop = NewLoop(llm);

        await loop.RunTurnStreamingAsync("Checkpoint (/save) now", kind: PromptKind.Chk);

        Assert.Equal(2, llm.Requests.Count);
        Assert.Equal(llm.Requests[0].Messages.Count, llm.Requests[1].Messages.Count);       // the same request, resent
        Assert.Equal(["Checkpoint (/save) now"], Prompts(loop).Select(m => m.GetText()));   // no "try a different approach"
        Assert.DoesNotContain(loop.History.Messages, m => m.GetText().Contains("Error during response"));
        Assert.Equal("Done.", loop.History.GetLastAssistantText());
    }

    [Fact]
    public async Task NonStreaming_ConnectionReset_ResendsTheSameRequest_WithoutANudge()
    {
        var llm = new FakeLlmClient()
            .EnqueueThrow(ConnectionReset())
            .Enqueue(new LlmResponse { Message = ChatMessage.AssistantText("Done."), StopReason = "end_turn" });
        var loop = NewLoop(llm);

        await loop.RunTurnNonStreamingAsync("Checkpoint (/save) now", kind: PromptKind.Chk);

        Assert.Equal(2, llm.Requests.Count);
        Assert.Equal(llm.Requests[0].Messages.Count, llm.Requests[1].Messages.Count);
        Assert.Equal(["Checkpoint (/save) now"], Prompts(loop).Select(m => m.GetText()));
        Assert.DoesNotContain(loop.History.Messages, m => m.GetText().Contains("Error during response"));
    }

    [Fact]
    public async Task Streaming_ResetsOnSeveralRequestsInARow_AreEachResent()
    {
        var llm = new FakeStreamingLlmClient()
            .EnqueueThrow(ConnectionReset())
            .EnqueueThrow(new IOException("Unable to read data from the transport connection."))
            .Enqueue(new TextDelta("Done."), new StreamDone("end_turn"));
        var loop = NewLoop(llm);

        await loop.RunTurnStreamingAsync("hi");

        Assert.Equal(3, llm.Requests.Count);
        Assert.Equal(["hi"], Prompts(loop).Select(m => m.GetText()));
    }

    [Fact]
    public async Task Streaming_AnErrorTheApiAnsweredWith_StillGetsTheNudge()
    {
        // A status code means the API did answer (here an overload), so this is not a transport failure.
        var llm = new FakeStreamingLlmClient()
            .EnqueueThrow(new HttpRequestException("overloaded", null, (HttpStatusCode)529))
            .Enqueue(new TextDelta("Done."), new StreamDone("end_turn"));
        var loop = NewLoop(llm);

        await loop.RunTurnStreamingAsync("hi");

        Assert.Equal([PromptKind.Std, PromptKind.Sys], Prompts(loop).Select(m => m.Kind));
    }
}
