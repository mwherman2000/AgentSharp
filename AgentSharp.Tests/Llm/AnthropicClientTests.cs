using System.Net;
using AgentSharp.Llm;

namespace AgentSharp.Tests.Llm;

public class AnthropicClientTests
{
    private static LlmRequest MakeRequest() => new()
    {
        SystemPrompt = "You are a test assistant.",
        Messages = [ChatMessage.UserText("Hello")]
    };

    private static AnthropicClient MakeClient(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(body, status));
        return new AnthropicClient(httpClient, "fake-api-key", "claude-sonnet-4-20250514");
    }

    [Fact]
    public async Task SendAsync_ParsesTextResponse()
    {
        const string body = """
            {
                "id": "msg_123",
                "type": "message",
                "role": "assistant",
                "content": [{ "type": "text", "text": "Hello there" }],
                "stop_reason": "end_turn",
                "usage": { "input_tokens": 25, "output_tokens": 10 }
            }
            """;
        var client = MakeClient(body);

        var response = await client.SendAsync(MakeRequest());

        Assert.Equal("Hello there", response.Message.GetText());
        Assert.Equal(MessageRole.Assistant, response.Message.Role);
        Assert.Equal("end_turn", response.StopReason);
        Assert.Equal(25, response.InputTokens);
        Assert.Equal(10, response.OutputTokens);
    }

    [Fact]
    public async Task SendAsync_ParsesToolUseResponse()
    {
        const string body = """
            {
                "id": "msg_456",
                "type": "message",
                "role": "assistant",
                "content": [{ "type": "tool_use", "id": "toolu_01ABC", "name": "read_file", "input": { "path": "foo.cs" } }],
                "stop_reason": "tool_use",
                "usage": { "input_tokens": 30, "output_tokens": 15 }
            }
            """;
        var client = MakeClient(body);

        var response = await client.SendAsync(MakeRequest());

        var toolUse = Assert.Single(response.Message.GetToolUses());
        Assert.Equal("toolu_01ABC", toolUse.Id);
        Assert.Equal("read_file", toolUse.Name);
        Assert.Equal("foo.cs", toolUse.Input.GetProperty("path").GetString());
        Assert.Equal("tool_use", response.StopReason);
    }

    [Fact]
    public async Task SendAsync_ParsesCacheTokens()
    {
        const string body = """
            {
                "id": "msg_789",
                "type": "message",
                "role": "assistant",
                "content": [{ "type": "text", "text": "cached!" }],
                "stop_reason": "end_turn",
                "usage": {
                    "input_tokens": 5,
                    "output_tokens": 2,
                    "cache_creation_input_tokens": 100,
                    "cache_read_input_tokens": 900
                }
            }
            """;
        var client = MakeClient(body);

        var response = await client.SendAsync(MakeRequest());

        Assert.Equal(100, response.CacheCreationInputTokens);
        Assert.Equal(900, response.CacheReadInputTokens);
    }

    [Fact]
    public async Task SendAsync_MissingCacheTokens_DefaultToZero()
    {
        const string body = """
            {
                "id": "msg_000",
                "type": "message",
                "role": "assistant",
                "content": [{ "type": "text", "text": "no cache info" }],
                "stop_reason": "end_turn",
                "usage": { "input_tokens": 5, "output_tokens": 2 }
            }
            """;
        var client = MakeClient(body);

        var response = await client.SendAsync(MakeRequest());

        Assert.Equal(0, response.CacheCreationInputTokens);
        Assert.Equal(0, response.CacheReadInputTokens);
    }

    [Fact]
    public async Task SendAsync_OnFailure_ThrowsWithApiErrorBody()
    {
        const string errorBody = """{"type":"error","error":{"type":"invalid_request_error","message":"bad request"}}""";
        var client = MakeClient(errorBody, HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(MakeRequest()));

        Assert.Contains("bad request", ex.Message);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }
}
