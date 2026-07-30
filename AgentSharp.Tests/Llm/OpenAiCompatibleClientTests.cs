using System.Net;
using AgentSharp.Llm;

namespace AgentSharp.Tests.Llm;

public class OpenAiCompatibleClientTests
{
    private static LlmRequest MakeRequest() => new()
    {
        SystemPrompt = "You are a test assistant.",
        Messages = [ChatMessage.UserText("Hello")]
    };

    private static OpenAiCompatibleClient MakeClient(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(body, status));
        return new OpenAiCompatibleClient(httpClient, "fake-api-key", "gpt-4o");
    }

    [Fact]
    public async Task SendAsync_ParsesTextResponse()
    {
        const string body = """
            {
                "id": "chatcmpl-123",
                "choices": [{
                    "index": 0,
                    "message": { "role": "assistant", "content": "Hello there" },
                    "finish_reason": "stop"
                }],
                "usage": { "prompt_tokens": 25, "completion_tokens": 10 }
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
    public async Task SendAsync_ParsesToolCallsResponse()
    {
        const string body = """
            {
                "id": "chatcmpl-456",
                "choices": [{
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "tool_calls": [{
                            "id": "call_01ABC",
                            "type": "function",
                            "function": { "name": "read_file", "arguments": "{\"path\":\"foo.cs\"}" }
                        }]
                    },
                    "finish_reason": "tool_calls"
                }],
                "usage": { "prompt_tokens": 30, "completion_tokens": 15 }
            }
            """;
        var client = MakeClient(body);

        var response = await client.SendAsync(MakeRequest());

        var toolUse = Assert.Single(response.Message.GetToolUses());
        Assert.Equal("call_01ABC", toolUse.Id);
        Assert.Equal("read_file", toolUse.Name);
        Assert.Equal("foo.cs", toolUse.Input.GetProperty("path").GetString());
        Assert.Equal("tool_use", response.StopReason);
    }

    [Fact]
    public async Task SendAsync_ParsesCachedTokens()
    {
        const string body = """
            {
                "id": "chatcmpl-789",
                "choices": [{
                    "index": 0,
                    "message": { "role": "assistant", "content": "cached!" },
                    "finish_reason": "stop"
                }],
                "usage": {
                    "prompt_tokens": 100,
                    "completion_tokens": 5,
                    "prompt_tokens_details": { "cached_tokens": 80 }
                }
            }
            """;
        var client = MakeClient(body);

        var response = await client.SendAsync(MakeRequest());

        Assert.Equal(80, response.CacheReadInputTokens);
        Assert.Equal(0, response.CacheCreationInputTokens);
    }

    [Fact]
    public async Task SendAsync_MissingCachedTokens_DefaultsToZero()
    {
        const string body = """
            {
                "id": "chatcmpl-000",
                "choices": [{
                    "index": 0,
                    "message": { "role": "assistant", "content": "no cache info" },
                    "finish_reason": "stop"
                }],
                "usage": { "prompt_tokens": 5, "completion_tokens": 2 }
            }
            """;
        var client = MakeClient(body);

        var response = await client.SendAsync(MakeRequest());

        Assert.Equal(0, response.CacheReadInputTokens);
    }

    [Fact]
    public async Task SendAsync_OnFailure_ThrowsWithApiErrorBody()
    {
        const string errorBody = """{"error":{"message":"bad request","type":"invalid_request_error"}}""";
        var client = MakeClient(errorBody, HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(MakeRequest()));

        Assert.Contains("bad request", ex.Message);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }
}
