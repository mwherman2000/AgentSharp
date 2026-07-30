using System.Net;
using System.Text;

namespace AgentSharp.Tests.Llm;

/// <summary>
/// Returns a fixed response for every request, without touching the network.
/// Used to test LLM clients' SendAsync against canned API responses.
/// </summary>
internal class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;

    public HttpRequestMessage? LastRequest { get; private set; }

    public StubHttpMessageHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _content = content;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
