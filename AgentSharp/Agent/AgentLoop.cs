using AgentSharp.Llm;
using AgentSharp.Safety;
using AgentSharp.Telemetry;
using AgentSharp.Tools;
using Spectre.Console;
using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AgentSharp.Agent;

/// <summary>
/// The core agent loop -- the #1 pattern from Claude Code.
///
/// Think -> Decide -> Execute -> Observe -> Repeat
///
/// The loop continues until the LLM stops requesting tool calls
/// (stop_reason: "end_turn") or the user cancels.
///
/// This is intentionally kept simple: ~100 lines of core logic.
/// The loop is the heart of the agent, everything else is plumbing.
/// </summary>
public class AgentLoop
{
    private readonly ILlmClient _llm;
    private readonly ToolRegistry _tools;
    private readonly ApprovalGate _approval;
    private readonly ConversationHistory _history;
    private readonly string _systemPrompt;
    private readonly int _maxTokens;
    private int _totalInputTokens;
    private int _totalOutputTokens;
    private int _totalCacheCreationTokens;
    private int _totalCacheReadTokens;

    /// <summary>Turn counter for OTel turn.number tags and the RequestTrace/ToolsTrace/
    /// HistoryTrace console prefixes. Instance state, not static -- each concurrent
    /// sub-agent owns its own AgentLoop, so a shared static counter would let them
    /// stomp on each other's turn numbers in telemetry.</summary>
    private int _turnNumber;

    /// <summary>Running count of tool executions within the current turn only, reset
    /// to 0 at the start of each RunTurn*Async call. Tagged on every tool.* telemetry
    /// span alongside <see cref="_totalToolExecutions"/> so a trace shows both "how
    /// far into this turn" and "how far into the whole conversation."</summary>
    private int _turnToolExecutions;

    /// <summary>Grand total tool executions across every turn this AgentLoop has run
    /// (never reset). Instance state for the same reason as <see cref="_turnNumber"/> --
    /// each sub-agent's own AgentLoop tracks its own total independently.</summary>
    private int _totalToolExecutions;

    /// <summary>Grand total tool executions across every turn this AgentLoop has run.</summary>
    public int TotalToolExecutions => _totalToolExecutions;

    public ConversationHistory History => _history;

    /// <summary>The system prompt this AgentLoop was built with.</summary>
    public string SystemPrompt => _systemPrompt;

    /// <summary>Cumulative input tokens billed across every LLM call this AgentLoop has made.</summary>
    public int TotalInputTokens => _totalInputTokens;

    /// <summary>Cumulative output tokens billed across every LLM call this AgentLoop has made.</summary>
    public int TotalOutputTokens => _totalOutputTokens;

    /// <summary>Cumulative tokens written to the prompt cache (0 if the provider doesn't support it).</summary>
    public int TotalCacheCreationTokens => _totalCacheCreationTokens;

    /// <summary>Cumulative tokens served from the prompt cache instead of being reprocessed.</summary>
    public int TotalCacheReadTokens => _totalCacheReadTokens;

    /// <summary>
    /// Event raised when tool execution starts, for UI rendering.
    /// </summary>
    public event Action<string, string>? OnToolStart; // (toolName, inputSummary)

    /// <summary>
    /// Event raised when tool execution completes.
    /// </summary>
    public event Action<string, ToolResult>? OnToolEnd; // (toolName, result)

    /// <summary>Default max_tokens for requests built by this loop. Overridable per
    /// instance via the <c>maxTokens</c> constructor parameter (wired to
    /// <c>--max-tokens</c>/<c>AGENT_MAX_TOKENS</c> on the CLI). Claude models support up
    /// to 128K output tokens; large content-generation turns (e.g. drafting a whole book
    /// chapter in one tool call) need real headroom here, not a small default.</summary>
    public const int DefaultMaxTokens = 128_000;

    /// <summary>Default cap on LLM&lt;-&gt;tool round-trips within a single turn, overridable
    /// per instance via the <c>maxIterations</c> constructor parameter (wired to
    /// <c>--max-iterations</c>/<c>AGENT_MAX_ITERATIONS</c> on the CLI). Guards against a
    /// runaway loop, but a single iteration typically only fetches/verifies a handful of
    /// sources -- a deep multi-phase deliverable (e.g. a 20-40-source sourced report,
    /// each fetched and verified individually, then synthesized and rendered to a file)
    /// can legitimately need several times the old 25-iteration default before it ever
    /// reaches a final answer.</summary>
    public const int DefaultMaxIterations = 100;

    /// <summary>Timeout budget for a retry attempt (the 2nd/3rd try after a failure
    /// within the same turn iteration's retry loop), overriding the LLM client's own
    /// much longer per-request timeout for that attempt only. The first attempt always
    /// gets the client's full configured timeout (10 min default, or --timeout/
    /// AGENT_TIMEOUT_MINUTES) -- this only shortens *retries*, so a persistently dead
    /// connection is discovered in well under maxStreamRetries/maxRetries full
    /// timeouts rather than that many multiplied together.</summary>
    private static readonly TimeSpan RetryAttemptTimeout = TimeSpan.FromMinutes(2);

    private readonly int _maxIterations;

    public AgentLoop(
        ILlmClient llm,
        ToolRegistry tools,
        ApprovalGate approval,
        string systemPrompt,
        ConversationHistory? history = null,
        int maxTokens = DefaultMaxTokens,
        int maxIterations = DefaultMaxIterations)
    {
        _llm = llm;
        _tools = tools;
        _approval = approval;
        _systemPrompt = systemPrompt;
        _history = history ?? new ConversationHistory();
        _maxTokens = maxTokens;
        _maxIterations = maxIterations;
    }

    /// <summary>
    /// Run one turn of the agent loop: process a user message through
    /// as many LLM calls and tool executions as needed until the model
    /// produces a final text response (stop_reason: "end_turn").
    /// </summary>
    public async Task<string> RunTurnStreamingAsync(string userMessage, CancellationToken ct = default)
    {
        _turnNumber++;
        _turnToolExecutions = 0;
        using var turnActivity = AgentTelemetry.Source.StartActivity("agent.turn");
        turnActivity?.SetTag("turn.number", _turnNumber);
        turnActivity?.SetTag("turn.mode", "streaming");
        _history.AddUserMessage(userMessage);

        var fullResponseText = new StringBuilder();
        int iterations = 0;
        int consecutiveStreamErrors = 0;
        const int maxStreamRetries = 3; // Max consecutive streaming failures before giving up

        while (iterations++ < _maxIterations)
        {
            ct.ThrowIfCancellationRequested();

            // --- THINK: Call the LLM with full history ---
            var request = BuildRequest();
            using var llmActivity = AgentTelemetry.Source.StartActivity("llm.request");
            llmActivity?.SetTag("llm.messages.count", request.Messages.Count);
            llmActivity?.SetTag("llm.tools.count", request.Tools?.Count ?? 0);
            if (Program.RequestTrace) Console.WriteLine($"\n{_turnNumber}>>>request: {System.Text.Json.JsonSerializer.Serialize(request)}");
            if (Program.ToolsTrace)   Console.WriteLine($"\n >>request.Tools: {System.Text.Json.JsonSerializer.Serialize(request.Tools)}");
            if (Program.HistoryTrace) Console.WriteLine($"\n >>request.Messages: {System.Text.Json.JsonSerializer.Serialize(request.Messages)}");

            // Accumulate the streamed response
            var contentBlocks = new List<ContentBlock>();
            var currentText = new StringBuilder();
            string? currentToolId = null;
            string? currentToolName = null;
            var currentToolInput = new StringBuilder();
            string stopReason = "end_turn";
            bool streamError = false;
            // Providers report usage as a running snapshot (not a per-event delta),
            // so we overwrite rather than accumulate as UsageInfo events arrive.
            int lastInputTokens = 0;
            int lastOutputTokens = 0;
            int lastCacheCreationTokens = 0;
            int lastCacheReadTokens = 0;

            // The client's own per-request timeout (10 min default, or --timeout/
            // AGENT_TIMEOUT_MINUTES) is sized to give a first attempt every chance to
            // complete. A retry after a failure (including that same timeout firing on
            // a stalled connection) doesn't need that long to reveal whether this
            // attempt is going to work either -- capping it at RetryAttemptTimeout
            // means a persistently dead connection fails in well under
            // maxStreamRetries full timeouts instead of multiplying them out.
            using var attemptCts = consecutiveStreamErrors > 0
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : null;
            attemptCts?.CancelAfter(RetryAttemptTimeout);
            var attemptCt = attemptCts?.Token ?? ct;

            try
            {
                await foreach (var evt in _llm.StreamAsync(request, attemptCt))
                {
                    switch (evt)
                    {
                        case TextDelta td:
                            currentText.Append(td.Text);
                            WriteTextToConsole(td.Text);
                            llmActivity?.AddEvent(new ActivityEvent("text_delta",
                                tags: new ActivityTagsCollection { { "text.length", td.Text.Length }, { "text.content", td.Text } }));
                            break;

                        case ToolUseStart tus:
                            // Flush any accumulated text
                            if (currentText.Length > 0)
                            {
                                contentBlocks.Add(new TextBlock { Text = currentText.ToString() });
                                AppendResponseSegment(fullResponseText, currentText.ToString());
                                currentText.Clear();
                            }
                            currentToolId = tus.Id;
                            currentToolName = tus.Name;
                            currentToolInput.Clear();
                            llmActivity?.AddEvent(new ActivityEvent("tool_use_start",
                                tags: new ActivityTagsCollection { { "tool.id", tus.Id }, { "tool.name", tus.Name } }));
                            break;

                        case ToolInputDelta tid:
                            currentToolInput.Append(tid.PartialJson);
                            break;

                        case ToolUseEnd:
                            if (currentToolId is not null && currentToolName is not null)
                            {
                                JsonElement inputJson;
                                try
                                {
                                    inputJson = currentToolInput.Length > 0
                                        ? JsonDocument.Parse(currentToolInput.ToString()).RootElement.Clone()
                                        : JsonDocument.Parse("{}").RootElement;
                                }
                                catch (JsonException ex)
                                {
                                    // LLM sent malformed tool input — record a failing tool use
                                    // so the error feeds back into the loop for self-correction
                                    inputJson = JsonDocument.Parse("{}").RootElement;
                                    contentBlocks.Add(new ToolUseBlock
                                    {
                                        Id = currentToolId,
                                        Name = currentToolName,
                                        Input = inputJson,
                                        ParseError = $"Invalid JSON in tool input: {ex.Message}"
                                    });
                                    currentToolId = null;
                                    currentToolName = null;
                                    currentToolInput.Clear();
                                    break;
                                }

                                contentBlocks.Add(new ToolUseBlock
                                {
                                    Id = currentToolId,
                                    Name = currentToolName,
                                    Input = inputJson
                                });
                                // ExecuteToolCallsAsync hasn't run yet for this iteration's tool
                                // calls -- _turnToolExecutions/_totalToolExecutions still reflect
                                // only prior iterations of this turn. Project what count this
                                // block will get once executed: prior count plus how many
                                // ToolUseBlocks this streamed response has produced so far
                                // (including this one, just added above).
                                var toolUseOrdinal = contentBlocks.Count(cb => cb is ToolUseBlock);
                                llmActivity?.AddEvent(new ActivityEvent("tool_use_end",
                                    tags: new ActivityTagsCollection
                                    {
                                        { "tool.id", currentToolId },
                                        { "tool.name", currentToolName },
                                        { "tool.input", inputJson.GetRawText() },
                                        { "tool.execution.turn_count", _turnToolExecutions + toolUseOrdinal },
                                        { "tool.execution.total_count", _totalToolExecutions + toolUseOrdinal }
                                    }));
                            }
                            currentToolId = null;
                            currentToolName = null;
                            currentToolInput.Clear();
                            break;

                        case StreamDone sd:
                            stopReason = sd.StopReason;
                            llmActivity?.SetTag("llm.stop_reason", stopReason);
                            break;

                        case UsageInfo ui:
                            lastInputTokens = ui.InputTokens;
                            lastOutputTokens = ui.OutputTokens;
                            lastCacheCreationTokens = ui.CacheCreationInputTokens;
                            lastCacheReadTokens = ui.CacheReadInputTokens;
                            llmActivity?.SetTag("llm.usage.input_tokens", ui.InputTokens);
                            llmActivity?.SetTag("llm.usage.output_tokens", ui.OutputTokens);
                            llmActivity?.SetTag("llm.usage.cache_creation_tokens", ui.CacheCreationInputTokens);
                            llmActivity?.SetTag("llm.usage.cache_read_tokens", ui.CacheReadInputTokens);
                            break;
                    }
                }
            }
            // Both AnthropicClient.SendAsync/StreamAsync enforce their own per-request
            // timeout via a CancellationTokenSource linked to (but distinct from) this
            // ct -- a stalled/hung connection throws the exact same
            // OperationCanceledException type as a real user Ctrl+C. Only rethrow (and
            // so abort the whole turn) when ct itself -- the turn's own token -- was
            // what got cancelled; a request-level timeout with ct still live is a
            // transient failure and must fall through to the retry-with-backoff catch
            // below, same as any other failed request.
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (HttpRequestException ex) when (ex.StatusCode is
                System.Net.HttpStatusCode.Unauthorized or
                System.Net.HttpStatusCode.Forbidden or
                System.Net.HttpStatusCode.BadRequest)
            {
                // Non-retryable auth/client errors — don't loop, just report
                llmActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
                PrintAuthOrConfigHint(ex.Message);
                break;
            }
            catch (Exception ex)
            {
                // Transient API or streaming error — retry with exponential backoff
                consecutiveStreamErrors++;
                streamError = true;
                llmActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                AnsiConsole.MarkupLine($"\n[red]Error:[/] [dim]{Markup.Escape(ex.GetType().FullName ?? ex.GetType().Name)}: {Markup.Escape(ex.Message)}[/]");
                if (ex.InnerException is { } innerEx)
                    AnsiConsole.MarkupLine($"[dim]  Inner: {Markup.Escape(innerEx.GetType().FullName ?? innerEx.GetType().Name)}: {Markup.Escape(innerEx.Message)}[/]");
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(ex.StackTrace ?? "")}[/]");

                if (consecutiveStreamErrors >= maxStreamRetries)
                {
                    AnsiConsole.MarkupLine($"[yellow]Failed after {maxStreamRetries} consecutive errors. Stopping.[/]");
                    break;
                }

                // Exponential backoff: 500ms, 1s, 2s, ...
                var delay = ComputeBackoffDelay(consecutiveStreamErrors);
                AnsiConsole.MarkupLine($"[dim]Retrying in {delay.TotalSeconds:F1}s...[/]");
                await Task.Delay(delay, ct);

                // If we have no content at all, add the error as text so the
                // LLM sees what happened and can adjust
                if (contentBlocks.Count == 0 && currentText.Length == 0)
                {
                    _history.AddAssistantMessage(new List<ContentBlock>
                    {
                        new TextBlock { Text = $"[Error during response: {ex.Message}]" }
                    });
                    _history.AddUserMessage(
                        "The previous request failed with an error. Please try a different approach.");
                    continue;
                }
            }

            // Reset error counter on successful stream completion
            if (!streamError)
                consecutiveStreamErrors = 0;

            _totalInputTokens += lastInputTokens;
            _totalOutputTokens += lastOutputTokens;
            _totalCacheCreationTokens += lastCacheCreationTokens;
            _totalCacheReadTokens += lastCacheReadTokens;
            turnActivity?.SetTag("turn.total_input_tokens", _totalInputTokens);
            turnActivity?.SetTag("turn.total_output_tokens", _totalOutputTokens);
            turnActivity?.SetTag("turn.total_cache_creation_tokens", _totalCacheCreationTokens);
            turnActivity?.SetTag("turn.total_cache_read_tokens", _totalCacheReadTokens);

            // Flush any remaining text
            if (currentText.Length > 0)
            {
                contentBlocks.Add(new TextBlock { Text = currentText.ToString() });
                AppendResponseSegment(fullResponseText, currentText.ToString());
            }

            // --- DECIDE: Append assistant message to history ---
            _history.AddAssistantMessage(contentBlocks);

            // If no tool calls, we're done. Every tool_use block requires a matching
            // tool_result in the next message per the Anthropic API contract, so we must
            // execute pending tool calls even if stopReason claims "end_turn" (providers
            // can report stop_reason inconsistently when a tool_use block is present).
            var toolUses = contentBlocks.OfType<ToolUseBlock>().ToList();
            if (toolUses.Count == 0)
            {
                AnsiConsole.WriteLine();
                turnActivity?.SetTag("turn.stop_reason", stopReason);
                turnActivity?.SetTag("turn.tool_executions", _turnToolExecutions);

                if (stopReason == "max_tokens")
                {
                    // The response (or a tool call the model was mid-way through
                    // starting, e.g. a ToolUseStart with no matching ToolUseEnd) got
                    // cut off by the output token limit before any tool_use block
                    // could be completed, so there's nothing here to execute or
                    // report a tool_result for. Nudge the model to pick up where it
                    // left off instead of silently ending the turn.
                    AddMaxTokensNudge();
                    continue;
                }

                break;
            }

            // --- EXECUTE: Run each tool call ---
            var toolResults = await ExecuteToolCallsAsync(toolUses, ct);

            // --- OBSERVE: Feed results back to the LLM ---
            _history.AddToolResults(toolResults);

            // Loop continues -- the LLM will see the tool results and decide next steps
        }

        if (iterations >= _maxIterations)
        {
            AnsiConsole.MarkupLine("[yellow]Warning: Agent loop reached maximum iterations. Stopping.[/]");
        }

        return fullResponseText.ToString();
    }

    /// <summary>
    /// Non-streaming counterpart to RunTurnAsync: calls ILlmClient.SendAsync instead
    /// of StreamAsync, so each LLM call arrives as one complete LlmResponse instead of
    /// incremental StreamEvents. Shares request building, the tool-execution loop, the
    /// max_tokens-continuation nudge, and retry/backoff with the streaming path -- only
    /// "how do we get a response out of the LLM, and how do we render it" differs.
    /// </summary>
    public async Task<string> RunTurnNonStreamingAsync(string userMessage, CancellationToken ct = default)
    {
        _turnNumber++;
        _turnToolExecutions = 0;
        using var turnActivity = AgentTelemetry.Source.StartActivity("agent.turn");
        turnActivity?.SetTag("turn.number", _turnNumber);
        turnActivity?.SetTag("turn.mode", "non-streaming");
        _history.AddUserMessage(userMessage);

        var fullResponseText = new StringBuilder();
        int iterations = 0;
        int consecutiveErrors = 0;
        const int maxRetries = 3; // Max consecutive call failures before giving up

        while (iterations++ < _maxIterations)
        {
            ct.ThrowIfCancellationRequested();

            // --- THINK: Call the LLM with full history ---
            var request = BuildRequest();
            using var llmActivity = AgentTelemetry.Source.StartActivity("llm.request");
            llmActivity?.SetTag("llm.messages.count", request.Messages.Count);
            llmActivity?.SetTag("llm.tools.count", request.Tools?.Count ?? 0);
            if (Program.RequestTrace) Console.WriteLine($"\n{_turnNumber}>>>request: {System.Text.Json.JsonSerializer.Serialize(request)}");
            if (Program.ToolsTrace)   Console.WriteLine($"\n >>request.Tools: {System.Text.Json.JsonSerializer.Serialize(request.Tools)}");
            if (Program.HistoryTrace) Console.WriteLine($"\n >>request.Messages: {System.Text.Json.JsonSerializer.Serialize(request.Messages)}");

            // See the matching comment in RunTurnStreamingAsync: only the first
            // attempt gets the client's full configured timeout; retries after a
            // failure (including that same timeout firing on a stalled connection)
            // are capped much shorter so a persistently dead connection fails fast
            // instead of multiplying the full timeout by maxRetries.
            using var attemptCts = consecutiveErrors > 0
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : null;
            attemptCts?.CancelAfter(RetryAttemptTimeout);
            var attemptCt = attemptCts?.Token ?? ct;

            LlmResponse response;
            try
            {
                response = await _llm.SendAsync(request, attemptCt);
            }
            // Both AnthropicClient.SendAsync/StreamAsync enforce their own per-request
            // timeout via a CancellationTokenSource linked to (but distinct from) this
            // ct -- a stalled/hung connection throws the exact same
            // OperationCanceledException type as a real user Ctrl+C. Only rethrow (and
            // so abort the whole turn) when ct itself -- the turn's own token -- was
            // what got cancelled; a request-level timeout with ct still live is a
            // transient failure and must fall through to the retry-with-backoff catch
            // below, same as any other failed request.
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (HttpRequestException ex) when (ex.StatusCode is
                System.Net.HttpStatusCode.Unauthorized or
                System.Net.HttpStatusCode.Forbidden or
                System.Net.HttpStatusCode.BadRequest)
            {
                // Non-retryable auth/client errors — don't loop, just report
                llmActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
                PrintAuthOrConfigHint(ex.Message);
                break;
            }
            catch (Exception ex)
            {
                // Transient API error — retry with exponential backoff. Unlike the
                // streaming path, a failed SendAsync never has partial content to
                // preserve, so every retryable failure gets the same error+nudge
                // treatment as streaming's "no content at all" case.
                consecutiveErrors++;
                llmActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                AnsiConsole.MarkupLine($"\n[red]Error:[/] [dim]{Markup.Escape(ex.GetType().FullName ?? ex.GetType().Name)}: {Markup.Escape(ex.Message)}[/]");
                if (ex.InnerException is { } innerEx)
                    AnsiConsole.MarkupLine($"[dim]  Inner: {Markup.Escape(innerEx.GetType().FullName ?? innerEx.GetType().Name)}: {Markup.Escape(innerEx.Message)}[/]");
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(ex.StackTrace ?? "")}[/]");

                if (consecutiveErrors >= maxRetries)
                {
                    AnsiConsole.MarkupLine($"[yellow]Failed after {maxRetries} consecutive errors. Stopping.[/]");
                    break;
                }

                var delay = ComputeBackoffDelay(consecutiveErrors);
                AnsiConsole.MarkupLine($"[dim]Retrying in {delay.TotalSeconds:F1}s...[/]");
                await Task.Delay(delay, ct);

                _history.AddAssistantMessage(new List<ContentBlock>
                {
                    new TextBlock { Text = $"[Error during response: {ex.Message}]" }
                });
                _history.AddUserMessage(
                    "The previous request failed with an error. Please try a different approach.");
                continue;
            }

            consecutiveErrors = 0;

            llmActivity?.SetTag("llm.stop_reason", response.StopReason);
            llmActivity?.SetTag("llm.usage.input_tokens", response.InputTokens);
            llmActivity?.SetTag("llm.usage.output_tokens", response.OutputTokens);
            llmActivity?.SetTag("llm.usage.cache_creation_tokens", response.CacheCreationInputTokens);
            llmActivity?.SetTag("llm.usage.cache_read_tokens", response.CacheReadInputTokens);

            _totalInputTokens += response.InputTokens;
            _totalOutputTokens += response.OutputTokens;
            _totalCacheCreationTokens += response.CacheCreationInputTokens;
            _totalCacheReadTokens += response.CacheReadInputTokens;
            turnActivity?.SetTag("turn.total_input_tokens", _totalInputTokens);
            turnActivity?.SetTag("turn.total_output_tokens", _totalOutputTokens);
            turnActivity?.SetTag("turn.total_cache_creation_tokens", _totalCacheCreationTokens);
            turnActivity?.SetTag("turn.total_cache_read_tokens", _totalCacheReadTokens);

            // SendAsync returns the whole message already assembled (unlike streaming,
            // there's no live rendering as it arrives), so render it here in one shot.
            var text = response.Message.GetText();
            if (text.Length > 0)
            {
                WriteTextToConsole(text);
                AppendResponseSegment(fullResponseText, text);
            }

            // --- DECIDE: Append assistant message to history ---
            _history.AddAssistantMessage(response.Message);

            var toolUses = response.Message.GetToolUses().ToList();
            if (toolUses.Count == 0)
            {
                AnsiConsole.WriteLine();
                turnActivity?.SetTag("turn.stop_reason", response.StopReason);
                turnActivity?.SetTag("turn.tool_executions", _turnToolExecutions);

                if (response.StopReason == "max_tokens")
                {
                    AddMaxTokensNudge();
                    continue;
                }

                break;
            }

            // --- EXECUTE: Run each tool call ---
            var toolResults = await ExecuteToolCallsAsync(toolUses, ct);

            // --- OBSERVE: Feed results back to the LLM ---
            _history.AddToolResults(toolResults);

            // Loop continues -- the LLM will see the tool results and decide next steps
        }

        if (iterations >= _maxIterations)
        {
            AnsiConsole.MarkupLine("[yellow]Warning: Agent loop reached maximum iterations. Stopping.[/]");
        }

        return fullResponseText.ToString();
    }

    /// <summary>
    /// Builds the request sent to the LLM: system prompt, full history, and tool
    /// definitions. Shared by both the streaming and non-streaming turn loops.
    /// </summary>
    private LlmRequest BuildRequest() => new()
    {
        SystemPrompt = _systemPrompt,
        Messages = _history.Messages.ToList(),
        Tools = _tools.GetDefinitions(),
        MaxTokens = _maxTokens
    };

    /// <summary>
    /// Exponential backoff schedule for retrying a failed LLM call: 500ms, 1s, 2s, ...
    /// </summary>
    private static TimeSpan ComputeBackoffDelay(int attempt) =>
        TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));

    /// <summary>
    /// Anthropic and OpenAI both report a too-long conversation as a plain 400 --
    /// the same status this catch already treats as "non-retryable client error" --
    /// so without this check a context-length overflow prints the generic
    /// "check your API key" hint, actively pointing the user at the wrong fix.
    /// There's no dedicated status code for it, only wording in the error body, so
    /// this matches on the phrasing providers actually use rather than a status.
    /// </summary>
    private static bool IsContextLengthError(string exceptionMessage) =>
        exceptionMessage.Contains("too long", StringComparison.OrdinalIgnoreCase) ||
        exceptionMessage.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase) ||
        exceptionMessage.Contains("context length", StringComparison.OrdinalIgnoreCase) ||
        exceptionMessage.Contains("maximum context", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Anthropic reports an empty/insufficient account balance as a plain 400 too --
    /// same non-dedicated-status-code situation as context length, and the API key
    /// itself may be perfectly valid -- without this check it gets the same
    /// misleading "check your API key" hint as a real auth failure, when the actual
    /// fix is adding credits/checking the plan, not touching the key or config at
    /// all. Also matches OpenAI's equivalent wording for the same underlying case.
    /// </summary>
    private static bool IsInsufficientCreditError(string exceptionMessage) =>
        exceptionMessage.Contains("credit balance", StringComparison.OrdinalIgnoreCase) ||
        exceptionMessage.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
        exceptionMessage.Contains("exceeded your current quota", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Follow-up hint for a non-retryable 4xx from the LLM: a context-length overflow
    /// or an out-of-credit account each get specific recovery guidance instead of the
    /// generic "check your API key" hint, which is actively wrong for both -- neither
    /// is an API key or config problem. (There's no compaction for the context-length
    /// case -- ConversationHistory is a flat append-only list, so /clear or a fresh
    /// session are the only ways out.)
    /// </summary>
    private static void PrintAuthOrConfigHint(string exceptionMessage)
    {
        if (IsContextLengthError(exceptionMessage))
        {
            AnsiConsole.MarkupLine("[dim]This conversation has grown too large for the model's context window -- " +
                "not an API key or config problem. Use /clear to start fresh (this discards history), or continue " +
                "the remaining work in a new session.[/]");
        }
        else if (IsInsufficientCreditError(exceptionMessage))
        {
            AnsiConsole.MarkupLine("[dim]Your account has run out of credit/quota -- not an API key or config " +
                "problem. Add credits or check your plan at your provider's billing page (e.g. " +
                "console.anthropic.com -> Plans & Billing for Anthropic), then retry.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Check your API key and provider configuration.[/]");
        }
    }

    /// <summary>
    /// The response (or a tool call the model was mid-way through starting) got cut
    /// off by the output token limit before any tool_use block could be completed, so
    /// there's nothing to execute or report a tool_result for. Nudge the model to pick
    /// up where it left off instead of silently ending the turn.
    /// </summary>
    private void AddMaxTokensNudge()
    {
        AnsiConsole.MarkupLine("[yellow]Response was cut off by the output token limit. Asking the model to continue...[/]");
        _history.AddUserMessage("Your previous response was cut off because it reached the output token limit. Please continue where you left off.");
    }

    /// <summary>
    /// Each flushed text segment (split from the next by an intervening tool call,
    /// or from a prior LLM iteration) is a fresh piece of model output that assumes
    /// nothing about what preceded it -- it won't start with a leading space or
    /// newline even when continuing a sentence-in-progress across a tool call. Plain
    /// concatenation therefore glues unrelated segments together (e.g. "this one.Alright"),
    /// so insert a newline at the boundary unless one side already provides whitespace.
    /// </summary>
    private static void AppendResponseSegment(StringBuilder fullResponseText, string segment)
    {
        if (segment.Length == 0) return;
        if (fullResponseText.Length > 0 &&
            !char.IsWhiteSpace(fullResponseText[fullResponseText.Length - 1]) &&
            !char.IsWhiteSpace(segment[0]))
        {
            fullResponseText.Append('\n');
        }
        fullResponseText.Append(segment);
    }

    /// <summary>
    /// AnsiConsole.Write(string) forwards to the composite-format overload, which
    /// treats the text as a format string and throws FormatException the moment it
    /// contains a brace (e.g. code deltas) -- Text() writes the content literally
    /// instead. Embedding a raw '\n' inside a single Text segment also doesn't
    /// reliably move the cursor to column 0 under VT processing, so this splits on
    /// newlines and emits each break via AnsiConsole.WriteLine explicitly.
    /// </summary>
    private static void WriteTextToConsole(string text)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) AnsiConsole.WriteLine();
            if (lines[i].Length > 0) AnsiConsole.Write(new Text(lines[i]));
        }
    }

    /// <summary>
    /// Runs every pending tool call (with approval-gate checks) and collects the
    /// results to feed back to the LLM. Shared by both the streaming and
    /// non-streaming turn loops -- tool execution doesn't depend on how the
    /// triggering LLM response was received.
    /// </summary>
    private async Task<List<ToolResultBlock>> ExecuteToolCallsAsync(
        IReadOnlyList<ToolUseBlock> toolUses, CancellationToken ct)
    {
        AnsiConsole.WriteLine(); // Separator before tool execution output
        var toolResults = new List<ToolResultBlock>();

        foreach (var toolUse in toolUses)
        {
            // Counted here -- the single place shared by both the streaming and
            // non-streaming turn loops -- rather than at each call site, which
            // previously counted inconsistently: streaming incremented as soon as
            // the model started emitting a tool call (ToolUseStart), before it was
            // even validated, while non-streaming added the whole batch up front.
            // Counting every toolUse processed here (including a parse error or a
            // denied approval below) matches that prior "tool_use blocks issued"
            // semantic, now counted exactly once and in one place for both paths.
            _turnToolExecutions++;
            _totalToolExecutions++;

            using var toolActivity = AgentTelemetry.Source.StartActivity($"tool.{toolUse.Name}");
            toolActivity?.SetTag("tool.id", toolUse.Id);
            toolActivity?.SetTag("tool.name", toolUse.Name);
            toolActivity?.SetTag("tool.execution.turn_count", _turnToolExecutions);
            toolActivity?.SetTag("tool.execution.total_count", _totalToolExecutions);

            // If the LLM sent malformed JSON, return the parse error
            // so the model can self-correct on the next iteration
            if (toolUse.ParseError is not null)
            {
                var parseErrorResult = ToolResult.Error(toolUse.ParseError);
                toolActivity?.SetStatus(ActivityStatusCode.Error, toolUse.ParseError);
                toolResults.Add(new ToolResultBlock
                {
                    ToolUseId = toolUse.Id,
                    Content = parseErrorResult.Output,
                    IsError = true
                });
                OnToolEnd?.Invoke(toolUse.Name, parseErrorResult);
                continue;
            }

            var tool = _tools.Get(toolUse.Name);
            var inputSummary = SummarizeInput(toolUse.Name, toolUse.Input);
            toolActivity?.SetTag("tool.input", toolUse.Input.GetRawText());

            OnToolStart?.Invoke(toolUse.Name, inputSummary);

            // Safety gate: check approval
            if (tool is not null)
            {
                var approved = await _approval.CheckApprovalAsync(tool, inputSummary, toolUse.Input, ct);
                if (!approved)
                {
                    var deniedResult = ToolResult.Error("User denied this tool execution.");
                    toolActivity?.SetStatus(ActivityStatusCode.Error, "User denied this tool execution.");
                    toolResults.Add(new ToolResultBlock
                    {
                        ToolUseId = toolUse.Id,
                        Content = deniedResult.Output,
                        IsError = true
                    });
                    OnToolEnd?.Invoke(toolUse.Name, deniedResult);
                    continue;
                }
            }

            // Execute the tool
            var result = await _tools.ExecuteAsync(toolUse.Name, toolUse.Input, ct);

            toolActivity?.SetTag("tool.is_error", result.IsError);
            toolActivity?.SetTag("tool.output.length", result.Output.Length);
            if (result.IsError)
                toolActivity?.SetStatus(ActivityStatusCode.Error, result.Output);

            toolResults.Add(new ToolResultBlock
            {
                ToolUseId = toolUse.Id,
                Content = result.Output,
                IsError = result.IsError
            });

            OnToolEnd?.Invoke(toolUse.Name, result);
        }

        return toolResults;
    }

    /// <summary>
    /// Create a human-readable summary of tool input for approval prompts.
    /// </summary>
    private static string SummarizeInput(string toolName, JsonElement input)
    {
        try
        {
            return toolName switch
            {
                "read_file" => $"Read file: {input.GetProperty("path").GetString()}",
                "write_file" => $"Write to: {input.GetProperty("path").GetString()}",
                "edit_file" => $"Edit file: {input.GetProperty("path").GetString()}",
                "list_files" => $"List files in: {(input.TryGetProperty("path", out var p) ? p.GetString() : ".")}",
                "grep" => $"Search for: {input.GetProperty("pattern").GetString()}",
                "run_shell" => $"Run: {input.GetProperty("command").GetString()}",
                "think" => "Thinking...",
                _ => input.GetRawText()
            };
        }
        catch
        {
            return input.GetRawText();
        }
    }
}
