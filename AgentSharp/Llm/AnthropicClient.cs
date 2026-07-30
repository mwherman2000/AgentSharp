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
        : this(new HttpClient(), apiKey, model)
    {
    }

    /// <summary>
    /// Test seam: allows injecting an HttpClient wrapping a fake HttpMessageHandler
    /// so tests can verify request/response handling without calling the real API.
    /// </summary>
    internal AnthropicClient(HttpClient httpClient, string apiKey, string model = "claude-sonnet-4-20250514")
    {
        _model = model;
        _http = httpClient;
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
        await EnsureSuccessAsync(response, ct);

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(json);
    }

    /// <summary>
    /// Throws with the API's actual error body on failure. EnsureSuccessStatusCode()
    /// alone discards the response content, so callers only ever see
    /// "400 (Bad Request)" with no indication of what was actually wrong.
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"Anthropic API error {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
            null,
            response.StatusCode);
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
        await EnsureSuccessAsync(response, ct);

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

        // System prompt -- written as a single cached text block. The system prompt
        // (project instructions, file tree, tool summaries) is large and stable across
        // a session, so this is the highest-value cache breakpoint.
        writer.WritePropertyName("system");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString("text", request.SystemPrompt);
        WriteCacheControl(writer);
        writer.WriteEndObject();
        writer.WriteEndArray();

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
        for (var mi = 0; mi < messages.Count; mi++)
        {
            var msg = messages[mi];
            writer.WriteStartObject();
            writer.WriteString("role", msg.Role);

            writer.WritePropertyName("content");
            writer.WriteStartArray();
            for (var bi = 0; bi < msg.Content.Count; bi++)
            {
                var block = msg.Content[bi];
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

                // Cache breakpoint at the tail of the conversation so far. History is
                // append-only (see ConversationHistory), so everything before this
                // point is byte-identical to the previous request and Anthropic can
                // serve it from cache; only the newly appended turn needs reprocessing.
                if (mi == messages.Count - 1 && bi == msg.Content.Count - 1)
                    WriteCacheControl(writer);

                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteCacheControl(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("cache_control");
        writer.WriteStartObject();
        writer.WriteString("type", "ephemeral");
        writer.WriteEndObject();
    }

    private static void WriteTools(Utf8JsonWriter writer, IReadOnlyList<ToolDefinition> tools)
    {
        writer.WriteStartArray();
        for (var i = 0; i < tools.Count; i++)
        {
            var tool = tools[i];
            writer.WriteStartObject();
            writer.WriteString("name", tool.Name);
            writer.WriteString("description", tool.Description);
            writer.WritePropertyName("input_schema");
            tool.InputSchema.WriteTo(writer);

            // Tool definitions are identical on every request, so cache them too --
            // this is the most stable (and cheapest to keep cached) part of the prompt.
            if (i == tools.Count - 1)
                WriteCacheControl(writer);

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
                    yield return ReadUsage(usage);
                }
                break;

            case "message_start":
                if (json.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("usage", out var startUsage))
                {
                    yield return ReadUsage(startUsage);
                }
                break;
        }
    }

    /// <summary>
    /// Reads Anthropic's usage object, including the cache_creation_input_tokens
    /// and cache_read_input_tokens fields that only appear once cache_control
    /// breakpoints are in the request and the model actually hits the cache.
    /// </summary>
    private static UsageInfo ReadUsage(JsonElement usage)
    {
        return new UsageInfo(
            usage.TryGetProperty("input_tokens", out var it) ? GetIntFlexible(it) : 0,
            usage.TryGetProperty("output_tokens", out var ot) ? GetIntFlexible(ot) : 0,
            usage.TryGetProperty("cache_creation_input_tokens", out var cc) ? GetIntFlexible(cc) : 0,
            usage.TryGetProperty("cache_read_input_tokens", out var cr) ? GetIntFlexible(cr) : 0);
    }

    /// <summary>
    /// Reads a JSON numeric element as an int, tolerating providers/proxies that
    /// emit whole numbers with a decimal point (e.g. 12.0). JsonElement.GetInt32()
    /// uses a strict digit-only fast path and throws a FormatException
    /// ("Expected an ASCII digit") on such values, so we fall back to a
    /// double-based parse and truncate to int.
    /// </summary>
    private static int GetIntFlexible(JsonElement element)
    {
        if (element.TryGetInt32(out var i))
            return i;

        if (element.TryGetDouble(out var d))
            return (int)d;

        var raw = element.GetRawText().Trim('"');
        return (int)double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
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
            InputTokens = GetIntFlexible(usage.GetProperty("input_tokens")),
            OutputTokens = GetIntFlexible(usage.GetProperty("output_tokens")),
            CacheCreationInputTokens = usage.TryGetProperty("cache_creation_input_tokens", out var cc) ? GetIntFlexible(cc) : 0,
            CacheReadInputTokens = usage.TryGetProperty("cache_read_input_tokens", out var cr) ? GetIntFlexible(cr) : 0
        };
    }
}
