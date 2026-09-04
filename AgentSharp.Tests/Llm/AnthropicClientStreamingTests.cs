using AgentSharp.Llm;

namespace AgentSharp.Tests.Llm;

/// <summary>
/// Verifies AnthropicClient.StreamAsync's timeout is idle-based (reset on every
/// received chunk) rather than a fixed cap on total call duration -- regression
/// coverage for the fix that replaced a single CancelAfter(_streamingTimeout) set once
/// up front with a per-chunk-reset idle timer plus a separate, never-reset absolute
/// backstop.
/// </summary>
public class AnthropicClientStreamingTests
{
    private static LlmRequest MakeRequest() => new()
    {
        SystemPrompt = "You are a test assistant.",
        Messages = [ChatMessage.UserText("Hello")]
    };

    private static string TextDeltaEvent(string text) =>
        "event: content_block_delta\n" +
        "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"" + text + "\"}}\n\n";

    private static AnthropicClient MakeClient(
        IReadOnlyList<(string Chunk, TimeSpan DelayBefore)> chunks, TimeSpan idleTimeout)
    {
        var httpClient = new HttpClient(new DelayedStreamHttpMessageHandler(chunks))
        {
            Timeout = idleTimeout
        };
        return new AnthropicClient(httpClient, "fake-api-key", "claude-sonnet-4-20250514");
    }

    [Fact]
    public async Task StreamAsync_SlowButSteadyChunks_CompletesEvenThoughTotalDurationExceedsIdleTimeout()
    {
        // Each gap between chunks is well under the idle timeout, but the sum of all
        // gaps is not -- under the old fixed-cap behavior (CancelAfter called once up
        // front) this would have been killed partway through. Margins are generous
        // (idle timeout several times the per-gap delay) so this doesn't flake under
        // scheduler contention when the full suite runs in parallel.
        var idleTimeout = TimeSpan.FromSeconds(2);
        var chunks = new (string, TimeSpan)[]
        {
            (TextDeltaEvent("a"), TimeSpan.Zero),
            (TextDeltaEvent("b"), TimeSpan.FromMilliseconds(300)),
            (TextDeltaEvent("c"), TimeSpan.FromMilliseconds(300)),
            (TextDeltaEvent("d"), TimeSpan.FromMilliseconds(300)),
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
        // Confirms the fix didn't accidentally disable timeout detection altogether --
        // a single gap longer than the idle timeout must still cancel the stream.
        var idleTimeout = TimeSpan.FromMilliseconds(300);
        var chunks = new (string, TimeSpan)[]
        {
            (TextDeltaEvent("a"), TimeSpan.Zero),
            (TextDeltaEvent("b"), TimeSpan.FromSeconds(3)),
        };
        var client = MakeClient(chunks, idleTimeout);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.StreamAsync(MakeRequest())) { }
        });
    }
}
