using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace AgentSharp.Llm;

/// <summary>
/// OpenAI-compatible API client. Works with OpenAI, Grok (xAI),
/// Gemini (via OpenAI-compatible endpoint), and any other compatible provider.
///
/// The OpenAI tool_use format differs from Anthropic:
/// - Tools are defined the same way (JSON schema)
/// - Tool calls come as "tool_calls" array in the assistant message
/// - Tool results use role "tool" with a "tool_call_id" field
/// - Streaming uses different SSE event format
///
/// We translate between OpenAI format and our internal domain types.
/// </summary>
public class OpenAiCompatibleClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _apiUrl;
    private readonly string _providerName;

    public string ProviderName => _providerName;
    public string ModelId => _model;

    public OpenAiCompatibleClient(
        string apiKey,
        string model,
        string baseUrl = "https://api.openai.com/v1",
        string providerName = "OpenAI")
        : this(new HttpClient(), apiKey, model, baseUrl, providerName)
    {
    }

    /// <summary>
    /// Test seam: allows injecting an HttpClient wrapping a fake HttpMessageHandler
    /// so tests can verify request/response handling without calling the real API.
    /// </summary>
    internal OpenAiCompatibleClient(
        HttpClient httpClient,
        string apiKey,
        string model,
        string baseUrl = "https://api.openai.com/v1",
        string providerName = "OpenAI")
    {
        _model = model;
        _apiUrl = $"{baseUrl.TrimEnd('/')}/chat/completions";
        _providerName = providerName;
        _http = httpClient;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>
    /// Create a client for OpenAI.
    /// </summary>
    public static OpenAiCompatibleClient ForOpenAi(string apiKey, string model = "gpt-4o")
        => new(apiKey, model, "https://api.openai.com/v1", "OpenAI");

    /// <summary>
    /// Create a client for xAI / Grok.
    /// </summary>
    public static OpenAiCompatibleClient ForGrok(string apiKey, string model = "grok-3")
        => new(apiKey, model, "https://api.x.ai/v1", "Grok");

    /// <summary>
    /// Create a client for Google Gemini (OpenAI-compatible endpoint).
    /// </summary>
    public static OpenAiCompatibleClient ForGemini(string apiKey, string model = "gemini-2.5-pro")
        => new(apiKey, model, "https://generativelanguage.googleapis.com/v1beta/openai", "Gemini");

    /// <summary>
    /// Create a client for a local Ollama server (OpenAI-compatible endpoint).
    /// Ollama doesn't require an API key; a placeholder value is sent since
    /// the underlying HttpClient always attaches an Authorization header.
    /// </summary>
    public static OpenAiCompatibleClient ForOllama(string model, string baseUrl = "http://localhost:11434/v1")
        => new("ollama", model, baseUrl, "Ollama");

    public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
    {
        var body = BuildRequestBody(request, stream: false);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(_apiUrl, content, ct);
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
            $"{response.RequestMessage?.RequestUri} error {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
            null,
            response.StatusCode);
    }

    public async IAsyncEnumerable<StreamEvent> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildRequestBody(request, stream: true);
        Console.Error.WriteLine($"[TIMING] request body length: {body.Length}");
        var __sw2 = System.Diagnostics.Stopwatch.StartNew();
        var httpContent = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _apiUrl) { Content = httpContent };
        foreach (var header in _http.DefaultRequestHeaders)
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        Console.Error.WriteLine($"[TIMING] headers received: {__sw2.ElapsedMilliseconds}ms");
        await EnsureSuccessAsync(response, ct);

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // Track tool calls being built across deltas
        var activeToolCalls = new Dictionary<int, (string id, string name, StringBuilder inputJson)>();

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null || string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]")
            {
                // Flush any remaining tool calls
                foreach (var tc in activeToolCalls.Values)
                {
                    yield return new ToolUseEnd();
                }
                yield return new StreamDone("end_turn");
                break;
            }

            var json = JsonDocument.Parse(data).RootElement;

            // The include_usage final chunk has an empty "choices" array and would
            // otherwise be skipped by the early-exit below (or never reached, since
            // the finish_reason chunk that precedes it already breaks the loop).
            if (json.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                yield return new UsageInfo(
                    usage.TryGetProperty("prompt_tokens", out var pt) ? GetIntFlexible(pt) : 0,
                    usage.TryGetProperty("completion_tokens", out var cpt) ? GetIntFlexible(cpt) : 0,
                    CacheReadInputTokens: GetCachedTokens(usage));
            }

            var choices = json.GetProperty("choices");
            if (choices.GetArrayLength() == 0) continue;

            var choice = choices[0];
            var delta = choice.GetProperty("delta");

            // Check finish_reason
            if (choice.TryGetProperty("finish_reason", out var fr) &&
                fr.ValueKind == JsonValueKind.String)
            {
                var reason = fr.GetString();
                if (reason == "tool_calls")
                {
                    foreach (var tc in activeToolCalls.Values)
                    {
                        yield return new ToolUseEnd();
                    }
                    yield return new StreamDone("tool_use");
                }
                else
                {
                    yield return new StreamDone(reason == "stop" ? "end_turn" : reason!);
                }
                break;
            }

            // Text content
            if (delta.TryGetProperty("content", out var contentProp) &&
                contentProp.ValueKind == JsonValueKind.String)
            {
                var text = contentProp.GetString();
                if (!string.IsNullOrEmpty(text))
                    yield return new TextDelta(text);
            }

            // Tool calls
            if (delta.TryGetProperty("tool_calls", out var toolCalls))
            {
                foreach (var tc in toolCalls.EnumerateArray())
                {
                    var index = GetIntFlexible(tc.GetProperty("index"));

                    // New tool call start
                    if (tc.TryGetProperty("id", out var idProp) &&
                        idProp.ValueKind == JsonValueKind.String)
                    {
                        var id = idProp.GetString()!;
                        var name = tc.GetProperty("function").GetProperty("name").GetString()!;
                        activeToolCalls[index] = (id, name, new StringBuilder());
                        yield return new ToolUseStart(id, name);
                    }

                    // Tool call argument delta
                    if (tc.TryGetProperty("function", out var fn) &&
                        fn.TryGetProperty("arguments", out var args) &&
                        args.ValueKind == JsonValueKind.String)
                    {
                        var partial = args.GetString()!;
                        if (activeToolCalls.TryGetValue(index, out var active))
                        {
                            active.inputJson.Append(partial);
                            yield return new ToolInputDelta(partial);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reads a JSON numeric element as an int, tolerating providers that emit
    /// whole numbers with a decimal point (e.g. "index": 0.0). JsonElement.GetInt32()
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

        // Last resort: some providers send numbers as JSON strings
        var raw = element.GetRawText().Trim('"');
        return (int)double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
    }

    private string BuildRequestBody(LlmRequest request, bool stream)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartObject();
        writer.WriteString("model", _model);
        writer.WriteNumber("max_tokens", request.MaxTokens);
        writer.WriteBoolean("stream", stream);
        writer.WriteNumber("temperature", request.Temperature);

        // Streamed chunks omit "usage" entirely unless explicitly requested --
        // without this, UsageInfo is never yielded during StreamAsync.
        if (stream)
        {
            writer.WritePropertyName("stream_options");
            writer.WriteStartObject();
            writer.WriteBoolean("include_usage", true);
            writer.WriteEndObject();
        }

        // Messages (system prompt is the first message with role "system" in OpenAI format)
        writer.WritePropertyName("messages");
        writer.WriteStartArray();

        // System message
        writer.WriteStartObject();
        writer.WriteString("role", "system");
        writer.WriteString("content", request.SystemPrompt);
        writer.WriteEndObject();

        // Conversation messages
        foreach (var msg in request.Messages)
        {
            WriteOpenAiMessage(writer, msg);
        }

        writer.WriteEndArray();

        // Tools
        if (request.Tools is { Count: > 0 })
        {
            writer.WritePropertyName("tools");
            writer.WriteStartArray();
            foreach (var tool in request.Tools)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "function");
                writer.WritePropertyName("function");
                writer.WriteStartObject();
                writer.WriteString("name", tool.Name);
                writer.WriteString("description", tool.Description);
                writer.WritePropertyName("parameters");
                tool.InputSchema.WriteTo(writer);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteOpenAiMessage(Utf8JsonWriter writer, ChatMessage msg)
    {
        // Handle tool result messages -- OpenAI uses role "tool"
        if (msg.Content.Any(b => b is ToolResultBlock))
        {
            foreach (var block in msg.Content.OfType<ToolResultBlock>())
            {
                writer.WriteStartObject();
                writer.WriteString("role", "tool");
                writer.WriteString("tool_call_id", block.ToolUseId);
                writer.WriteString("content", block.Content);
                writer.WriteEndObject();
            }
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("role", msg.Role);

        // Handle assistant messages with tool calls
        var toolUses = msg.Content.OfType<ToolUseBlock>().ToList();
        if (toolUses.Count > 0)
        {
            // Text content (if any)
            var text = string.Join("", msg.Content.OfType<TextBlock>().Select(b => b.Text));
            if (!string.IsNullOrEmpty(text))
                writer.WriteString("content", text);
            else
                writer.WriteNull("content");

            // Tool calls
            writer.WritePropertyName("tool_calls");
            writer.WriteStartArray();
            foreach (var tu in toolUses)
            {
                writer.WriteStartObject();
                writer.WriteString("id", tu.Id);
                writer.WriteString("type", "function");
                writer.WritePropertyName("function");
                writer.WriteStartObject();
                writer.WriteString("name", tu.Name);
                writer.WriteString("arguments", tu.Input.GetRawText());
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        else
        {
            // Simple text message
            var text = string.Join("", msg.Content.OfType<TextBlock>().Select(b => b.Text));
            writer.WriteString("content", text);
        }

        writer.WriteEndObject();
    }

    private static LlmResponse ParseResponse(string json)
    {
        var doc = JsonDocument.Parse(json).RootElement;
        var choice = doc.GetProperty("choices")[0];
        var message = choice.GetProperty("message");
        var finishReason = choice.GetProperty("finish_reason").GetString();

        var contentBlocks = new List<ContentBlock>();

        // Text content
        if (message.TryGetProperty("content", out var contentProp) &&
            contentProp.ValueKind == JsonValueKind.String)
        {
            var text = contentProp.GetString();
            if (!string.IsNullOrEmpty(text))
                contentBlocks.Add(new TextBlock { Text = text });
        }

        // Tool calls
        if (message.TryGetProperty("tool_calls", out var toolCalls))
        {
            foreach (var tc in toolCalls.EnumerateArray())
            {
                var fn = tc.GetProperty("function");
                contentBlocks.Add(new ToolUseBlock
                {
                    Id = tc.GetProperty("id").GetString()!,
                    Name = fn.GetProperty("name").GetString()!,
                    Input = JsonDocument.Parse(fn.GetProperty("arguments").GetString()!).RootElement.Clone()
                });
            }
        }

        var stopReason = finishReason == "tool_calls" ? "tool_use" :
                         finishReason == "stop" ? "end_turn" : finishReason!;

        int inputTokens = 0, outputTokens = 0, cacheReadTokens = 0;
        if (doc.TryGetProperty("usage", out var usage))
        {
            inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? GetIntFlexible(pt) : 0;
            outputTokens = usage.TryGetProperty("completion_tokens", out var ct2) ? GetIntFlexible(ct2) : 0;
            cacheReadTokens = GetCachedTokens(usage);
        }

        return new LlmResponse
        {
            Message = new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = contentBlocks
            },
            StopReason = stopReason,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheReadInputTokens = cacheReadTokens
        };
    }

    /// <summary>
    /// OpenAI-compatible providers cache automatically (no cache_control needed) and
    /// report the hit count as usage.prompt_tokens_details.cached_tokens -- there's no
    /// separate "cache creation" concept/cost the way Anthropic has.
    /// </summary>
    private static int GetCachedTokens(JsonElement usage)
        => usage.TryGetProperty("prompt_tokens_details", out var details) &&
           details.TryGetProperty("cached_tokens", out var cached)
            ? GetIntFlexible(cached)
            : 0;
}
