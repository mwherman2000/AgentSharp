# Streaming vs. non-streaming: `StreamAsync` and `SendAsync`

Every LLM provider client (`AnthropicClient`, `OpenAiCompatibleClient`) implements
`ILlmClient` with two ways to get a response:

- **`StreamAsync`** — yields incremental `StreamEvent`s (`TextDelta`, `ToolUseStart`,
  `ToolInputDelta`, `ToolUseEnd`, `StreamDone`, `UsageInfo`) as the model generates them.
  This is the default: it lets `AgentLoop` render text to the terminal live, token by
  token, instead of waiting for the whole response.
- **`SendAsync`** — makes one HTTP call and returns a single, fully-assembled
  `LlmResponse` once the model is done generating.

`AgentLoop` has one turn-loop per path:

| Path | Method | Backing call |
|---|---|---|
| Streaming (default) | `RunTurnAsync` | `ILlmClient.StreamAsync` |
| Non-streaming | `RunTurnNonStreamingAsync` | `ILlmClient.SendAsync` |

## Switching modes: the `/sync` command

`Program.SyncMode` is a static bool toggle (same pattern as `RequestTrace` /
`HistoryTrace` / `ToolsTrace`), flipped by the `/sync` REPL command. **Default is
`false`** (streaming). `ReplHost`'s turn dispatch and `Program`'s one-shot mode both
branch on it:

```csharp
if (Program.SyncMode)
    await agent.RunTurnNonStreamingAsync(input, ct);
else
    await agent.RunTurnAsync(input, ct);
```

There is currently no CLI flag or env var for this — it's REPL-only, toggled per
session with `/sync`.

## Which mode to use

Streaming (the default) is almost always the better choice, especially for
interactively-supervised runs against a local model. It gives live
token-by-token feedback, so you can see the model is actually making
progress, catch it going off track early, and — via `RequestTrace` /
`ToolsTrace` / `HistoryTrace` — get per-event trace detail (`<<TextDelta:`,
`<<ToolUseStart:`, etc.) that non-streaming never produces, since `SendAsync`
has no incremental events to trace.

Non-streaming blocks silently until the entire response has arrived. For a
slow local model generating a large response, that can mean several minutes
of a dead terminal with no signal whether it's progressing or stuck — the
only feedback is the eventual result or, if something really is stuck, the
non-streaming timeout (`ILlmClient.NonStreamingTimeout`, 10x the streaming
timeout — see `docs/max-tokens-and-local-model-context.md`).

There's no correctness difference between the two paths (same request
building, same tool-execution logic, same response parsing — see below), so
this is purely a visibility/UX tradeoff, and streaming wins it for anything
you're actively watching. `/sync` mode is there for cases where you don't
need live rendering (e.g. scripting against `--prompt` output) rather than as
a generally-preferred alternative.

## Why two loops instead of one

The two methods differ only in *how a response is obtained and rendered*:

- Streaming has to manually accumulate text/tool-call fragments across events,
  since the model's output arrives piecemeal.
- Non-streaming gets the whole `ChatMessage` (text + tool calls, already parsed)
  in one `LlmResponse`, so there's nothing to accumulate — it renders once and
  moves on.

Everything else — building the request, deciding whether to execute tools, running
them, retry/backoff behavior, and the max-tokens-truncation nudge — doesn't depend
on which transport got the response. That logic is factored into shared private
helpers on `AgentLoop` used by both loops:

| Helper | Purpose |
|---|---|
| `BuildRequest()` | Assembles the `LlmRequest` (system prompt, history, tool defs) |
| `ExecuteToolCallsAsync(toolUses, ct)` | Runs each pending tool call through `ApprovalGate` + `ToolRegistry`, collects `ToolResultBlock`s |
| `AddMaxTokensNudge()` | Adds the "you got cut off, please continue" user message when `stop_reason == "max_tokens"` with no tool call to execute |
| `ComputeBackoffDelay(attempt)` | The 500ms/1s/2s/... exponential backoff schedule for retryable errors |
| `WriteTextToConsole(text)` | Writes text via `AnsiConsole` safely — bypasses the composite-format brace crash and the CRLF cursor-reset bug (see inline doc comment) |

This keeps the two loops from drifting apart: a fix to tool execution, retry
behavior, or rendering only needs to happen once.

## Debug tracing parity

Both loops emit the same category of `Console.WriteLine` trace lines (entry,
`RequestTrace`/`ToolsTrace`/`HistoryTrace` dumps, `<<<TotalUsage:`,
`<<<fullResponseText:`, and the `Count 0`/stop-reason line with `nToolExecutions`).
The streaming loop additionally traces individual `StreamEvent`s
(`<<TextDelta:`, `<<ToolUseStart:`, etc.) — those have no equivalent in the
non-streaming path because the events themselves don't exist there; `SendAsync`
never produces per-token deltas.

## Session/history compatibility across modes

A conversation saved under one mode loads and continues correctly under the other.
This isn't incidental — it falls out of the design:

- `SessionManager` persists `ConversationHistory.Messages` as a generic
  `type: text|tool_use|tool_result` JSON schema. It has no concept of which
  transport produced a message.
- Both loops build identical `ChatMessage`/`ContentBlock` objects before adding
  them to history — streaming assembles them from events, `SendAsync` gets them
  pre-assembled from `ParseResponse`. By the time either path calls
  `AddAssistantMessage`, there's no structural difference left.
- `BuildRequest()` reads from `_history.Messages` unconditionally; it doesn't care
  whether those messages were loaded from disk, produced by streaming, or
  produced by `SendAsync`.
- Nothing mode-specific is persisted: token totals and Anthropic `cache_control`
  breakpoints are computed fresh per request/per `AgentLoop` instance, never
  written to the session file.
- Both loops guarantee history is "clean" (no `tool_use` without a matching
  `tool_result`) before returning control to the REPL, so a saved session is
  never mid-turn.

You can toggle `/sync` mid-session, or save under one mode and `/load` under the
other, with no compatibility concerns.

## Test coverage

- `AnthropicClientTests` / `OpenAiCompatibleClientTests` (`AgentSharp.Tests/Llm/`) —
  exercise `SendAsync` at the HTTP-client level via a stub `HttpMessageHandler`
  (`StubHttpMessageHandler`): response parsing, tool-call parsing, cache-token
  parsing, and the error path.
- `AgentLoopSendAsyncTests` (`AgentSharp.Tests/Agent/`) — exercises
  `RunTurnNonStreamingAsync`'s actual tool-processing loop via a scripted
  `FakeLlmClient` and a minimal `FakeTool`: text-only turns, the tool-call round
  trip (including history contents), the max-tokens nudge-and-continue path,
  non-retryable vs. transient errors, and usage/cache-token accumulation across
  iterations.

**Known gap:** `ApprovalGate.CheckApprovalAsync` isn't behind an interface and
isn't virtual, so it can't be faked in tests. Only `ReadOnly`/`Write`-risk fake
tools are used in `AgentLoopSendAsyncTests` (both auto-approve); the "tool
execution denied" branch (`Destructive`-risk tools, which block on
`Console.ReadKey`) is untested.
