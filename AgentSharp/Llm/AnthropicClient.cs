using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace AgentSharp.Llm;

/// <summary>
/// Anthropic Messages API client with SSE streaming support.
/// Implements the raw HTTP+SSE approach for maximum learning value.
/// </summary>
public class AnthropicClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _model;
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    public string ProviderName => "Anthropic";
    public string ModelId => _model;

    public AnthropicClient(string apiKey, string model = "claude-sonnet-4-20250514")
    {
        _model = model;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
    {
        var body = BuildRequestBody(request, stream: false);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        // Remove the Accept header for non-streaming
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl) { Content = content };
        httpRequest.Headers.Accept.Clear();
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        foreach (var header in _http.DefaultRequestHeaders)
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

        var response = await _http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(json);
    }

    public async IAsyncEnumerable<StreamEvent> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildRequestBody(request, stream: true);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl) { Content = content };
        foreach (var header in _http.DefaultRequestHeaders)
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? eventType = null;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            if (line.StartsWith("event: "))
            {
                eventType = line[7..];
                continue;
            }

            if (line.StartsWith("data: ") && eventType is not null)
            {
                var data = line[6..];
                var events = ParseSseEvent(eventType, data);
                foreach (var evt in events)
                    yield return evt;
                eventType = null;
            }
        }
    }

    private string BuildRequestBody(LlmRequest request, bool stream)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartObject();
        writer.WriteString("model", _model);
        writer.WriteNumber("max_tokens", request.MaxTokens);
        writer.WriteBoolean("stream", stream);

        // System prompt
        writer.WriteString("system", request.SystemPrompt);

        // Messages
        writer.WritePropertyName("messages");
        WriteMessages(writer, request.Messages);

        // Tools
        if (request.Tools is { Count: > 0 })
        {
            writer.WritePropertyName("tools");
            WriteTools(writer, request.Tools);
        }

        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteMessages(Utf8JsonWriter writer, IReadOnlyList<ChatMessage> messages)
    {
        writer.WriteStartArray();
        foreach (var msg in messages)
        {
            writer.WriteStartObject();
            writer.WriteString("role", msg.Role);

            writer.WritePropertyName("content");
            writer.WriteStartArray();
            foreach (var block in msg.Content)
            {
                writer.WriteStartObject();
                switch (block)
                {
                    case TextBlock tb:
                        writer.WriteString("type", "text");
                        writer.WriteString("text", tb.Text);
                        break;

                    case ToolUseBlock tub:
                        writer.WriteString("type", "tool_use");
                        writer.WriteString("id", tub.Id);
                        writer.WriteString("name", tub.Name);
                        writer.WritePropertyName("input");
                        tub.Input.WriteTo(writer);
                        break;

                    case ToolResultBlock trb:
                        writer.WriteString("type", "tool_result");
                        writer.WriteString("tool_use_id", trb.ToolUseId);
                        writer.WriteString("content", trb.Content);
                        if (trb.IsError)
                            writer.WriteBoolean("is_error", true);
                        break;
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteTools(Utf8JsonWriter writer, IReadOnlyList<ToolDefinition> tools)
    {
        writer.WriteStartArray();
        foreach (var tool in tools)
        {
            writer.WriteStartObject();
            writer.WriteString("name", tool.Name);
            writer.WriteString("description", tool.Description);
            writer.WritePropertyName("input_schema");
            tool.InputSchema.WriteTo(writer);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static IEnumerable<StreamEvent> ParseSseEvent(string eventType, string data)
    {
        var json = JsonDocument.Parse(data).RootElement;

        switch (eventType)
        {
            case "content_block_start":
                var cb = json.GetProperty("content_block");
                var cbType = cb.GetProperty("type").GetString();
                if (cbType == "tool_use")
                {
                    yield return new ToolUseStart(
                        cb.GetProperty("id").GetString()!,
                        cb.GetProperty("name").GetString()!);
                }
                break;

            case "content_block_delta":
                var delta = json.GetProperty("delta");
                var deltaType = delta.GetProperty("type").GetString();
                if (deltaType == "text_delta")
                {
                    yield return new TextDelta(delta.GetProperty("text").GetString()!);
                }
                else if (deltaType == "input_json_delta")
                {
                    yield return new ToolInputDelta(delta.GetProperty("partial_json").GetString()!);
                }
                break;

            case "content_block_stop":
                yield return new ToolUseEnd();
                break;

            case "message_delta":
                var msgDelta = json.GetProperty("delta");
                if (msgDelta.TryGetProperty("stop_reason", out var sr))
                {
                    yield return new StreamDone(sr.GetString()!);
                }
                if (json.TryGetProperty("usage", out var usage))
                {
                    yield return new UsageInfo(
                        usage.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0,
                        usage.GetProperty("output_tokens").GetInt32());
                }
                break;

            case "message_start":
                if (json.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("usage", out var startUsage))
                {
                    yield return new UsageInfo(
                        startUsage.GetProperty("input_tokens").GetInt32(),
                        startUsage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0);
                }
                break;
        }
    }

    private static LlmResponse ParseResponse(string json)
    {
        var doc = JsonDocument.Parse(json).RootElement;
        var contentBlocks = new List<ContentBlock>();
        foreach (var block in doc.GetProperty("content").EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            switch (type)
            {
                case "text":
                    contentBlocks.Add(new TextBlock { Text = block.GetProperty("text").GetString()! });
                    break;
                case "tool_use":
                    contentBlocks.Add(new ToolUseBlock
                    {
                        Id = block.GetProperty("id").GetString()!,
                        Name = block.GetProperty("name").GetString()!,
                        Input = block.GetProperty("input").Clone()
                    });
                    break;
            }
        }

        var stopReason = doc.GetProperty("stop_reason").GetString()!;
        var usage = doc.GetProperty("usage");

        return new LlmResponse
        {
            Message = new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = contentBlocks
            },
            StopReason = stopReason,
            InputTokens = usage.GetProperty("input_tokens").GetInt32(),
            OutputTokens = usage.GetProperty("output_tokens").GetInt32()
        };
    }
}
