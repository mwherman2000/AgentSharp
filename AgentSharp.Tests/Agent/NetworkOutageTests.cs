using AgentSharpLib.Agent;
using AgentSharpLib.Agent.MultiAgent;
using AgentSharpLib.Llm;
using AgentSharpLib.Network;
using AgentSharpLib.Output;
using AgentSharpLib.Safety;
using AgentSharpLib.Tools;

namespace AgentSharp.Tests.Agent;

/// <summary>
/// When a request gets no response, the loop checks whether the LLM's host can be reached and, if not,
/// waits for the network to come back instead of spending its three quick retries on it.
/// </summary>
public class NetworkOutageTests
{
    private static readonly Uri Api = new("https://api.example.test/v1/messages");

    private static NetworkStatus Down() => new(null, "api.example.test", 443, false, false, null);
    private static NetworkStatus Up() => new("Wi-Fi", "api.example.test", 443, true, true, null);

    /// <summary>Answers each check from a script; the last answer repeats.</summary>
    private sealed class ScriptedProbe(params NetworkStatus[] script) : IConnectivityProbe
    {
        public int Checks { get; private set; }
        public Task<NetworkStatus> CheckAsync(string host, int port, CancellationToken ct) =>
            Task.FromResult(script[Math.Min(Checks++, script.Length - 1)]);
    }

    private sealed class Recorder : IAgentOutput
    {
        public List<string> Infos { get; } = [];
        public List<string> Warnings { get; } = [];
        public void Text(string text) { }
        public void LineBreak() { }
        public void Trace(string text) { }
        public void Info(string message) => Infos.Add(message);
        public void Warning(string message) => Warnings.Add(message);
        public void Error(string message) { }
        public void TransientError(Exception exception) { }
        public void ToolAutoApproved(string toolName) { }
        public void ToolSkipped(string toolTypeName, string reason) { }
        public void SubAgentStarted(string name, string task, bool inBatch) { }
        public void SubAgentFinished(string name, SubAgentStatus status, bool inBatch) { }
        public void SubAgentBatchStarted(int count, int maxConcurrent) { }
        public void SubAgentBatchFinished(int count) { }
    }

    private static HttpRequestException Reset() =>
        new("An error occurred while sending the request.", new IOException("An existing connection was forcibly closed by the remote host."));

    private static AgentLoop NewLoop(ILlmClient llm, IConnectivityProbe probe, Recorder output, TimeSpan? wait = null) =>
        new(llm, new ToolRegistry(), new ApprovalGate(), "system prompt", output: output,
            connectivity: probe, networkWait: wait ?? TimeSpan.FromSeconds(2), networkPoll: TimeSpan.FromMilliseconds(5));

    private static StreamEvent[] Done() => [new TextDelta("Done."), new StreamDone("end_turn")];

    [Fact]
    public async Task Streaming_NetworkDown_WaitsForItThenResends_AndTheWaitIsNotARetry()
    {
        // Three drops in a row would stop the turn if each used up a retry. Each is an outage that recovers instead.
        var llm = new FakeStreamingLlmClient { Endpoint = Api };
        for (var i = 0; i < 3; i++) llm.EnqueueThrow(Reset());
        llm.Enqueue(Done());
        var probe = new ScriptedProbe(Down(), Up(), Down(), Up(), Down(), Up());
        var output = new Recorder();
        var loop = NewLoop(llm, probe, output);

        await loop.RunTurnStreamingAsync("hi");

        Assert.Equal(4, llm.Requests.Count);
        Assert.Equal("Done.", loop.History.GetLastAssistantText());
        Assert.Equal(3, output.Infos.Count(i => i.StartsWith("Network is back")));
        Assert.Contains(output.Infos, i => i.StartsWith("Network: no network connection is up"));
        Assert.Contains(output.Warnings, w => w.Contains("Lost the connection to api.example.test"));
    }

    [Fact]
    public async Task NonStreaming_NetworkDown_WaitsForItThenResends()
    {
        var llm = new FakeLlmClient { Endpoint = Api };
        for (var i = 0; i < 3; i++) llm.EnqueueThrow(Reset());
        llm.Enqueue(new LlmResponse { Message = ChatMessage.AssistantText("Done."), StopReason = "end_turn" });
        var output = new Recorder();
        var loop = NewLoop(llm, new ScriptedProbe(Down(), Up(), Down(), Up(), Down(), Up()), output);

        await loop.RunTurnNonStreamingAsync("hi");

        Assert.Equal(4, llm.Requests.Count);
        Assert.Equal("Done.", loop.History.GetLastAssistantText());
    }

    [Fact]
    public async Task Streaming_HostReachableAtOnce_IsAnOrdinaryRetry_AfterReportingTheStatus()
    {
        var llm = new FakeStreamingLlmClient { Endpoint = Api };
        llm.EnqueueThrow(Reset()).Enqueue(Done());
        var probe = new ScriptedProbe(Up());
        var output = new Recorder();
        var loop = NewLoop(llm, probe, output);

        await loop.RunTurnStreamingAsync("hi");

        Assert.Equal(2, llm.Requests.Count);
        Assert.Equal(1, probe.Checks);                                   // checked once; nothing to wait for
        Assert.Contains(output.Infos, i => i == "Network: Wi-Fi up; DNS ok; api.example.test:443 reachable");
        Assert.Empty(output.Warnings);
    }

    [Fact]
    public async Task Streaming_NetworkNeverReturns_StopsTheTurnWithAClearMessage()
    {
        var llm = new FakeStreamingLlmClient { Endpoint = Api };
        llm.EnqueueThrow(Reset()).Enqueue(Done());   // the second is never reached
        var output = new Recorder();
        var loop = NewLoop(llm, new ScriptedProbe(Down()), output, wait: TimeSpan.FromMilliseconds(60));

        await loop.RunTurnStreamingAsync("hi");

        Assert.Single(llm.Requests);
        Assert.Contains(output.Warnings, w => w.StartsWith("Still unreachable after"));
        Assert.Contains(output.Warnings, w => w.Contains("The network did not come back"));
        Assert.Equal(["hi"], loop.History.Messages.Where(m => m.Role == MessageRole.User).Select(m => m.GetText())); // no nudge either
    }

    [Fact]
    public async Task CtrlC_EndsTheWait()
    {
        var llm = new FakeStreamingLlmClient { Endpoint = Api };
        llm.EnqueueThrow(Reset());
        var loop = NewLoop(llm, new ScriptedProbe(Down()), new Recorder(), wait: TimeSpan.FromMinutes(5));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loop.RunTurnStreamingAsync("hi", cts.Token));
    }

    [Theory]
    [InlineData(null)]                              // a client with no endpoint (a fake, in-process)
    [InlineData("http://localhost:11434/v1/chat")]  // a local Ollama: nothing to wait for
    public async Task NoCheck_ForAClientWithoutARemoteEndpoint(string? endpoint)
    {
        var llm = new FakeStreamingLlmClient { Endpoint = endpoint is null ? null : new Uri(endpoint) };
        llm.EnqueueThrow(Reset()).Enqueue(Done());
        var probe = new ScriptedProbe(Down());
        var loop = NewLoop(llm, probe, new Recorder());

        await loop.RunTurnStreamingAsync("hi");

        Assert.Equal(0, probe.Checks);
        Assert.Equal(2, llm.Requests.Count);   // the ordinary retry
    }

    [Fact]
    public async Task AnErrorTheApiAnsweredWith_IsNotAnOutage()
    {
        var llm = new FakeStreamingLlmClient { Endpoint = Api };
        llm.EnqueueThrow(new HttpRequestException("overloaded", null, (System.Net.HttpStatusCode)529)).Enqueue(Done());
        var probe = new ScriptedProbe(Down());
        var loop = NewLoop(llm, probe, new Recorder());

        await loop.RunTurnStreamingAsync("hi");

        Assert.Equal(0, probe.Checks);
    }

    [Fact]
    public void Describe_SaysWhatIsWrong()
    {
        Assert.Equal("no network connection is up (no adapter has a gateway)", Down().Describe());
        Assert.Equal("Wi-Fi up; DNS lookup of api.example.test failed", new NetworkStatus("Wi-Fi", "api.example.test", 443, false, false, null).Describe());
        Assert.Equal("Wi-Fi up; DNS ok; api.example.test:443 timed out", new NetworkStatus("Wi-Fi", "api.example.test", 443, true, false, "timed out").Describe());
        Assert.Equal("Ethernet up; DNS ok; api.example.test:443 reachable", new NetworkStatus("Ethernet", "api.example.test", 443, true, true, null).Describe());
    }

    [Fact]
    public async Task TheRealProbe_ReportsAClosedPortAsUnreachable_WithoutThrowing()
    {
        // Port 1 on this machine: nothing listens. Whether or not the machine has a gateway, the answer is "not reachable".
        var status = await new ConnectivityProbe().CheckAsync("127.0.0.1", 1, CancellationToken.None);

        Assert.False(status.Reachable);
        Assert.False(string.IsNullOrWhiteSpace(status.Describe()));
    }

    [Fact]
    public void TheRealClients_NameTheirEndpoint()
    {
        Assert.Equal("api.anthropic.com", new AnthropicClient("k").Endpoint!.Host);
        Assert.Equal("api.openai.com", OpenAiCompatibleClient.ForOpenAi("k").Endpoint!.Host);
        Assert.True(OpenAiCompatibleClient.ForOllama("m").Endpoint!.IsLoopback);
    }
}
