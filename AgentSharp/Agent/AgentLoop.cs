using System.Text;
using System.Text.Json;
using AgentSharp.Llm;
using AgentSharp.Safety;
using AgentSharp.Tools;
using Spectre.Console;

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

    public ConversationHistory History => _history;

    /// <summary>
    /// Event raised when tool execution starts, for UI rendering.
    /// </summary>
    public event Action<string, string>? OnToolStart; // (toolName, inputSummary)

    /// <summary>
    /// Event raised when tool execution completes.
    /// </summary>
    public event Action<string, ToolResult>? OnToolEnd; // (toolName, result)

    public AgentLoop(
        ILlmClient llm,
        ToolRegistry tools,
        ApprovalGate approval,
        string systemPrompt,
        ConversationHistory? history = null)
    {
        _llm = llm;
        _tools = tools;
        _approval = approval;
        _systemPrompt = systemPrompt;
        _history = history ?? new ConversationHistory();
    }

    /// <summary>
    /// Run one turn of the agent loop: process a user message through
    /// as many LLM calls and tool executions as needed until the model
    /// produces a final text response (stop_reason: "end_turn").
    /// </summary>
    public async Task<string> RunTurnAsync(string userMessage, CancellationToken ct = default)
    {
        _history.AddUserMessage(userMessage);

        var fullResponseText = new StringBuilder();
        int iterations = 0;
        int consecutiveStreamErrors = 0;
        const int maxIterations = 25; // Safety limit to prevent infinite loops
        const int maxStreamRetries = 3; // Max consecutive streaming failures before giving up

        while (iterations++ < maxIterations)
        {
            ct.ThrowIfCancellationRequested();

            // --- THINK: Call the LLM with full history ---
            var request = new LlmRequest
            {
                SystemPrompt = _systemPrompt,
                Messages = _history.Messages.ToList(),
                Tools = _tools.GetDefinitions(),
                MaxTokens = 4096
            };

            // Accumulate the streamed response
            var contentBlocks = new List<ContentBlock>();
            var currentText = new StringBuilder();
            string? currentToolId = null;
            string? currentToolName = null;
            var currentToolInput = new StringBuilder();
            string stopReason = "end_turn";
            bool streamError = false;

            try
            {
                await foreach (var evt in _llm.StreamAsync(request, ct))
                {
                    switch (evt)
                    {
                        case TextDelta td:
                            currentText.Append(td.Text);
                            AnsiConsole.Write(td.Text); // Live streaming to terminal
                            break;

                        case ToolUseStart tus:
                            // Flush any accumulated text
                            if (currentText.Length > 0)
                            {
                                contentBlocks.Add(new TextBlock { Text = currentText.ToString() });
                                fullResponseText.Append(currentText);
                                currentText.Clear();
                            }
                            currentToolId = tus.Id;
                            currentToolName = tus.Name;
                            currentToolInput.Clear();
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
                            }
                            currentToolId = null;
                            currentToolName = null;
                            currentToolInput.Clear();
                            break;

                        case StreamDone sd:
                            stopReason = sd.StopReason;
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
                AnsiConsole.MarkupLine($"\n[red]Error:[/] [dim]{Markup.Escape(ex.Message)}[/]");

                if (consecutiveStreamErrors >= maxStreamRetries)
                {
                    AnsiConsole.MarkupLine($"[yellow]Failed after {maxStreamRetries} consecutive errors. Stopping.[/]");
                    break;
                }

                // Exponential backoff: 500ms, 1s, 2s, ...
                var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, consecutiveStreamErrors - 1));
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

            // Flush any remaining text
            if (currentText.Length > 0)
            {
                contentBlocks.Add(new TextBlock { Text = currentText.ToString() });
                fullResponseText.Append(currentText);
            }

            // --- DECIDE: Append assistant message to history ---
            _history.AddAssistantMessage(contentBlocks);

            // If no tool calls, we're done (model said "end_turn")
            var toolUses = contentBlocks.OfType<ToolUseBlock>().ToList();
            if (stopReason == "end_turn" || toolUses.Count == 0)
            {
                AnsiConsole.WriteLine();
                break;
            }

            // --- EXECUTE: Run each tool call ---
            AnsiConsole.WriteLine(); // New line after streamed text
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

                OnToolStart?.Invoke(toolUse.Name, inputSummary);

                // Safety gate: check approval
                if (tool is not null)
                {
                    var approved = await _approval.CheckApprovalAsync(tool, inputSummary);
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

                OnToolEnd?.Invoke(toolUse.Name, result);
            }

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
