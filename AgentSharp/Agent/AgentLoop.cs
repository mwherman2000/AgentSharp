using AgentSharp.Llm;
using AgentSharp.Safety;
using AgentSharp.Tools;
using Spectre.Console;
using System;
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

    public AgentLoop(
        ILlmClient llm,
        ToolRegistry tools,
        ApprovalGate approval,
        string systemPrompt,
        ConversationHistory? history = null,
        int maxTokens = DefaultMaxTokens)
    {
        _llm = llm;
        _tools = tools;
        _approval = approval;
        _systemPrompt = systemPrompt;
        _history = history ?? new ConversationHistory();
        _maxTokens = maxTokens;
    }

    /// <summary>
    /// Run one turn of the agent loop: process a user message through
    /// as many LLM calls and tool executions as needed until the model
    /// produces a final text response (stop_reason: "end_turn").
    /// </summary>
    static private int nTurns = 0;
    static private int nToolsExecutions = 0;
    public async Task<string> RunTurnStreamingAsync(string userMessage, CancellationToken ct = default)
    {
        nTurns++;
        //nToolsExecutions = 0;
        _history.AddUserMessage(userMessage);
        Console.WriteLine($"\n{nTurns}>>>userMessage: {userMessage}");

        var fullResponseText = new StringBuilder();
        int iterations = 0;
        int consecutiveStreamErrors = 0;
        const int maxIterations = 25; // Safety limit to prevent infinite loops
        const int maxStreamRetries = 3; // Max consecutive streaming failures before giving up

        while (iterations++ < maxIterations)
        {
            ct.ThrowIfCancellationRequested();

            // --- THINK: Call the LLM with full history ---
            var request = BuildRequest();
            if (Program.RequestTrace) Console.WriteLine($"\n{nTurns}>>>request: {System.Text.Json.JsonSerializer.Serialize(request)}");
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

            try
            {
                await foreach (var evt in _llm.StreamAsync(request, ct))
                {
                    switch (evt)
                    {
                        case TextDelta td:
                            currentText.Append(td.Text);
                            WriteTextToConsole(td.Text);
                            Console.WriteLine($"\n <<TextDelta: {td.Text}");
                            break;

                        case ToolUseStart tus:
                            nToolsExecutions++;
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
                            Console.WriteLine($"\n{nToolsExecutions} <<ToolUseStart: {tus.Id} {tus.Name}");
                            break;

                        case ToolInputDelta tid:
                            currentToolInput.Append(tid.PartialJson);
                            Console.WriteLine($"\n <<ToolInputDelta: {tid.PartialJson}");
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
                                    Console.WriteLine($"\n{nToolsExecutions} <<ToolUseEnd.inputJson: {inputJson.ToString()}");
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
                                Console.WriteLine($"\n{nToolsExecutions}<<<TextUseEnd: {currentToolId} {currentToolName} {inputJson.ToString()}");
                            }
                            currentToolId = null;
                            currentToolName = null;
                            currentToolInput.Clear();
                            break;

                        case StreamDone sd:
                            stopReason = sd.StopReason;
                            Console.WriteLine($"\n{nTurns}<<<StreamDone: {stopReason} nToolExecutions {nToolsExecutions}");
                            break;

                        case UsageInfo ui:
                            lastInputTokens = ui.InputTokens;
                            lastOutputTokens = ui.OutputTokens;
                            lastCacheCreationTokens = ui.CacheCreationInputTokens;
                            lastCacheReadTokens = ui.CacheReadInputTokens;
                            Console.WriteLine($"\n <<UsageInfo: input={ui.InputTokens} output={ui.OutputTokens} cacheCreate={ui.CacheCreationInputTokens} cacheRead={ui.CacheReadInputTokens}");
                            break;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (HttpRequestException ex) when (ex.StatusCode is
                System.Net.HttpStatusCode.Unauthorized or
                System.Net.HttpStatusCode.Forbidden or
                System.Net.HttpStatusCode.BadRequest)
            {
                // Non-retryable auth/client errors — don't loop, just report
                AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
                AnsiConsole.MarkupLine("[dim]Check your API key and provider configuration.[/]");
                break;
            }
            catch (Exception ex)
            {
                // Transient API or streaming error — retry with exponential backoff
                consecutiveStreamErrors++;
                streamError = true;
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
            Console.WriteLine($"\n<<<TotalUsage: input={_totalInputTokens} output={_totalOutputTokens} cacheCreate={_totalCacheCreationTokens} cacheRead={_totalCacheReadTokens}");

            // Flush any remaining text
            if (currentText.Length > 0)
            {
                contentBlocks.Add(new TextBlock { Text = currentText.ToString() });
                AppendResponseSegment(fullResponseText, currentText.ToString());
                Console.WriteLine($"\n<<<fullResponseText: {fullResponseText}");
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
                Console.WriteLine($"\n{nTurns}<<<stopReason: {stopReason} Count 0 nTurns {nTurns} nToolExecutions {nToolsExecutions}");

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

        if (iterations >= maxIterations)
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
        nTurns++;
        nToolsExecutions = 0;
        _history.AddUserMessage(userMessage);
        Console.WriteLine($"\n{nTurns}>>>userMessage (non-streaming): {userMessage}");

        var fullResponseText = new StringBuilder();
        int iterations = 0;
        int consecutiveErrors = 0;
        const int maxIterations = 25; // Safety limit to prevent infinite loops
        const int maxRetries = 3; // Max consecutive call failures before giving up

        while (iterations++ < maxIterations)
        {
            ct.ThrowIfCancellationRequested();

            // --- THINK: Call the LLM with full history ---
            var request = BuildRequest();
            if (Program.RequestTrace) Console.WriteLine($"\n{nTurns}>>>request: {System.Text.Json.JsonSerializer.Serialize(request)}");
            if (Program.ToolsTrace)   Console.WriteLine($"\n >>request.Tools: {System.Text.Json.JsonSerializer.Serialize(request.Tools)}");
            if (Program.HistoryTrace) Console.WriteLine($"\n >>request.Messages: {System.Text.Json.JsonSerializer.Serialize(request.Messages)}");

            LlmResponse response;
            try
            {
                response = await _llm.SendAsync(request, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (HttpRequestException ex) when (ex.StatusCode is
                System.Net.HttpStatusCode.Unauthorized or
                System.Net.HttpStatusCode.Forbidden or
                System.Net.HttpStatusCode.BadRequest)
            {
                // Non-retryable auth/client errors — don't loop, just report
                AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
                AnsiConsole.MarkupLine("[dim]Check your API key and provider configuration.[/]");
                break;
            }
            catch (Exception ex)
            {
                // Transient API error — retry with exponential backoff. Unlike the
                // streaming path, a failed SendAsync never has partial content to
                // preserve, so every retryable failure gets the same error+nudge
                // treatment as streaming's "no content at all" case.
                consecutiveErrors++;
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

            _totalInputTokens += response.InputTokens;
            _totalOutputTokens += response.OutputTokens;
            _totalCacheCreationTokens += response.CacheCreationInputTokens;
            _totalCacheReadTokens += response.CacheReadInputTokens;
            Console.WriteLine($"\n<<<TotalUsage: input={_totalInputTokens} output={_totalOutputTokens} cacheCreate={_totalCacheCreationTokens} cacheRead={_totalCacheReadTokens}");

            // SendAsync returns the whole message already assembled (unlike streaming,
            // there's no live rendering as it arrives), so render it here in one shot.
            var text = response.Message.GetText();
            if (text.Length > 0)
            {
                WriteTextToConsole(text);
                AppendResponseSegment(fullResponseText, text);
                Console.WriteLine($"\n<<<fullResponseText: {fullResponseText}");
            }

            // --- DECIDE: Append assistant message to history ---
            _history.AddAssistantMessage(response.Message);

            var toolUses = response.Message.GetToolUses().ToList();
            if (toolUses.Count == 0)
            {
                AnsiConsole.WriteLine();
                Console.WriteLine($"\n{nTurns}<<<stopReason: {response.StopReason} Count 0 nToolExecutions {nToolsExecutions} (non-streaming)");

                if (response.StopReason == "max_tokens")
                {
                    AddMaxTokensNudge();
                    continue;
                }

                break;
            }

            nToolsExecutions += toolUses.Count;

            // --- EXECUTE: Run each tool call ---
            var toolResults = await ExecuteToolCallsAsync(toolUses, ct);

            // --- OBSERVE: Feed results back to the LLM ---
            _history.AddToolResults(toolResults);

            // Loop continues -- the LLM will see the tool results and decide next steps
        }

        if (iterations >= maxIterations)
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
            // If the LLM sent malformed JSON, return the parse error
            // so the model can self-correct on the next iteration
            if (toolUse.ParseError is not null)
            {
                var parseErrorResult = ToolResult.Error(toolUse.ParseError);
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
            Console.WriteLine($"\n<<<toolUse: {toolUse.Id} {toolUse.Name} {toolUse.Input}\ninputSummary: {inputSummary}");

            OnToolStart?.Invoke(toolUse.Name, inputSummary);

            // Safety gate: check approval
            if (tool is not null)
            {
                var approved = await _approval.CheckApprovalAsync(tool, inputSummary, toolUse.Input);
                if (!approved)
                {
                    var deniedResult = ToolResult.Error("User denied this tool execution.");
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

            toolResults.Add(new ToolResultBlock
            {
                ToolUseId = toolUse.Id,
                Content = result.Output,
                IsError = result.IsError
            });
            Console.WriteLine($"\n<<<toolResults: {toolUse.Id} {toolUse.Name} {result.IsError}\nresult.Output:{result.Output}");

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
