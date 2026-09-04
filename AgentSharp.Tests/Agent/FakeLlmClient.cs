using AgentSharp.Llm;

namespace AgentSharp.Tests.Agent;

/// <summary>
/// Returns a scripted sequence of LlmResponses (or throws scripted exceptions) from
/// SendAsync, one per call, in order. Used to test AgentLoop.RunTurnNonStreamingAsync's
/// tool-processing loop without a real network call. StreamAsync is intentionally
/// unsupported -- these tests exist specifically to exercise the SendAsync path.
/// </summary>
internal class FakeLlmClient : ILlmClient
{
    private readonly Queue<Func<LlmResponse>> _responses = new();

    public string ProviderName => "Fake";
    public string ModelId => "fake-model";
    public TimeSpan StreamingTimeout => TimeSpan.FromSeconds(100);
    public TimeSpan NonStreamingTimeout => TimeSpan.FromSeconds(1000);
    public int CallCount { get; private set; }
    public List<LlmRequest> Requests { get; } = new();
    public List<CancellationToken> ReceivedTokens { get; } = new();

    public FakeLlmClient Enqueue(LlmResponse response)
    {
        _responses.Enqueue(() => response);
        return this;
    }

    public FakeLlmClient EnqueueThrow(Exception ex)
    {
        _responses.Enqueue(() => throw ex);
        return this;
    }

    public Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
    {
        CallCount++;
        Requests.Add(request);
        ReceivedTokens.Add(ct);
        if (_responses.Count == 0)
            throw new InvalidOperationException("FakeLlmClient: no more scripted responses.");
        return Task.FromResult(_responses.Dequeue()());
    }

    public IAsyncEnumerable<StreamEvent> StreamAsync(LlmRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("FakeLlmClient only supports SendAsync in these tests.");
}
