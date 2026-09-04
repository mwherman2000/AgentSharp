using AgentSharp.Llm;

namespace AgentSharp.Tests.Llm;

/// <summary>
/// Verifies OpenAiCompatibleClient.StreamAsync's timeout is idle-based (reset on every
/// received chunk) rather than a fixed cap on total call duration -- same fix and same
/// regression shape as AnthropicClientStreamingTests, kept separate since the two
/// clients implement the reset independently (different SSE formats/parsing) and could
/// diverge.
/// </summary>
public class OpenAiCompatibleClientStreamingTests
{
    private static LlmRequest MakeRequest() => new()
    {
        SystemPrompt = "You are a test assistant.",
        Messages = [ChatMessage.UserText("Hello")]
    };

    private static string TextDeltaChunk(string text) =>
        "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"" + text + "\"}}]}\n\n";

    private const string DoneChunk = "data: [DONE]\n\n";

    private static OpenAiCompatibleClient MakeClient(
        IReadOnlyList<(string Chunk, TimeSpan DelayBefore)> chunks, TimeSpan idleTimeout)
    {
        var httpClient = new HttpClient(new DelayedStreamHttpMessageHandler(chunks))
        {
            Timeout = idleTimeout
        };
        return new OpenAiCompatibleClient(httpClient, "fake-api-key", "gpt-4o");
    }

    [Fact]
    public async Task StreamAsync_SlowButSteadyChunks_CompletesEvenThoughTotalDurationExceedsIdleTimeout()
    {
        // Margins are generous (idle timeout several times the per-gap delay) so this
        // doesn't flake under scheduler contention when the full suite runs in parallel.
        var idleTimeout = TimeSpan.FromSeconds(2);
        var chunks = new (string, TimeSpan)[]
        {
            (TextDeltaChunk("a"), TimeSpan.Zero),
            (TextDeltaChunk("b"), TimeSpan.FromMilliseconds(300)),
            (TextDeltaChunk("c"), TimeSpan.FromMilliseconds(300)),
            (TextDeltaChunk("d"), TimeSpan.FromMilliseconds(300)),
            (DoneChunk, TimeSpan.FromMilliseconds(300)),
        };
        var client = MakeClient(chunks, idleTimeout);

        var received = new List<string>();
        await foreach (var evt in client.StreamAsync(MakeRequest()))
        {
            if (evt is TextDelta td) received.Add(td.Text);
        }

        Assert.Equal(["a", "b", "c", "d"], received);
    }

    [Fact]
    public async Task StreamAsync_GapLongerThanIdleTimeout_CancelsEvenThoughConnectionIsStillOpen()
    {
        var idleTimeout = TimeSpan.FromMilliseconds(300);
        var chunks = new (string, TimeSpan)[]
        {
            (TextDeltaChunk("a"), TimeSpan.Zero),
            (TextDeltaChunk("b"), TimeSpan.FromSeconds(3)),
        };
        var client = MakeClient(chunks, idleTimeout);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.StreamAsync(MakeRequest())) { }
        });
    }
}
