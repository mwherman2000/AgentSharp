using System.Runtime.CompilerServices;
using AgentSharp.Llm;

namespace AgentSharp.Tests.Agent;

/// <summary>
/// Returns a scripted sequence of StreamEvent batches (or throws a scripted exception)
/// from StreamAsync, one batch per call, in order. Used to test
/// AgentLoop.RunTurnStreamingAsync's tool-parsing and retry logic without a real
/// network call. SendAsync is intentionally unsupported -- these tests exist
/// specifically to exercise the streaming path (mirrors FakeLlmClient, which covers
/// SendAsync only).
/// </summary>
internal class FakeStreamingLlmClient : ILlmClient
{
    private readonly Queue<Func<List<StreamEvent>>> _responses = new();

    public string ProviderName => "FakeStreaming";
    public string ModelId => "fake-streaming-model";
    public TimeSpan StreamingTimeout => TimeSpan.FromSeconds(100);
    public TimeSpan NonStreamingTimeout => TimeSpan.FromSeconds(1000);
    public int CallCount { get; private set; }
    public List<CancellationToken> ReceivedTokens { get; } = new();

    public FakeStreamingLlmClient Enqueue(params StreamEvent[] events)
    {
        _responses.Enqueue(() => events.ToList());
        return this;
    }

    public FakeStreamingLlmClient EnqueueThrow(Exception ex)
    {
        _responses.Enqueue(() => throw ex);
        return this;
    }

    public Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("FakeStreamingLlmClient only supports StreamAsync in these tests.");

    public async IAsyncEnumerable<StreamEvent> StreamAsync(LlmRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        CallCount++;
        ReceivedTokens.Add(ct);
        if (_responses.Count == 0)
            throw new InvalidOperationException("FakeStreamingLlmClient: no more scripted responses.");

        var next = _responses.Dequeue();
        foreach (var evt in next())
        {
            await Task.Yield();
            yield return evt;
        }
    }
}
