# History compaction — backlog

**Status: not implemented.** This document exists to capture the problem and
the constraints a real fix needs to satisfy, so it isn't lost or solved
hastily under pressure the next time a long session runs out of context.

## The problem

`AgentLoop.BuildRequest()` (`AgentSharp/Agent/AgentLoop.cs`) sends the entire
accumulated `ConversationHistory` on every single turn:

```csharp
private LlmRequest BuildRequest() => new()
{
    SystemPrompt = _systemPrompt,
    Messages = _history.Messages.ToList(),
    Tools = _tools.GetDefinitions(),
    MaxTokens = _maxTokens
};
```

There is no summarization, pruning, or context-window management anywhere in
the codebase. Every user message, assistant response, and tool result stays
in history forever, growing the request body every turn for the life of the
session.

This is fine for short sessions against a large-context model, but it breaks
down for:

- Long-running sessions (the Consort-style pipeline pattern — see the
  `hyperonomy-book` project — is explicitly designed to run across many turns
  and sessions).
- Small local models with small context windows (e.g. `qwen3:4b-24k`'s
  24,576-token window via Ollama), where input and output share the same
  budget — see `docs/max-tokens-and-local-model-context.md`.
- Pipelines that repeatedly read a growing state file back into context (the
  Consort pipeline's `seed`/`checkpoint` stages re-read `STATE.md`, which
  reached 15KB+ in one real run).

`--max-tokens` (`docs/max-tokens-and-local-model-context.md`) only bounds the
*output* side of a request. Nothing currently bounds the *input* side, so a
long enough session will eventually exceed the model's context window
regardless of how `--max-tokens` is set.

## Why this isn't a quick fix

The obvious naive fix — drop the oldest N messages once history gets long —
is explicitly **not** what we want here:

- It can silently break `tool_use`/`tool_result` pairing, which the Anthropic
  and OpenAI-compatible APIs both require to be intact (a dangling
  `tool_use` with no matching `tool_result`, or vice versa, is a malformed
  request).
- It can drop context that's still load-bearing — e.g. an early decision or
  constraint from earlier in a book-compilation run that a later stage still
  depends on, even though the message that stated it is now "old."
- For a pipeline whose actual source of truth is a file (`STATE.md`), the
  right compaction strategy might look very different from a general-purpose
  chat-history summarizer — it may be enough to compact anything the agent
  can re-derive by re-reading `STATE.md`, while a naive age-based truncator
  has no way to know that.

## What a real solution needs to account for

- **A compaction algorithm/prompt that's actually good**, not a first draft.
  This likely means summarizing compacted-away turns rather than just
  deleting them, and being deliberate about what must survive verbatim
  (recent turns, unresolved `tool_use`/`tool_result` pairs, anything the rest
  of the session still depends on) versus what's safe to compress or drop.
- **Compatibility with `SessionManager`** (`AgentSharp/Memory/SessionManager.cs`)
  — compaction changes what "the conversation" means, and saved/loaded
  sessions need to keep making sense across it.
- **Parity across `RunTurnStreamingAsync` and `RunTurnNonStreamingAsync`**
  (see `docs/streaming-vs-sync.md`) — whatever compaction does, both loops
  build their request from the same `ConversationHistory`, so a fix in one
  path needs to apply to both, not just one.
- **Provider differences** — `AnthropicClient` and `OpenAiCompatibleClient`
  serialize history differently (e.g. Anthropic's `cache_control` breakpoints
  assume a stable, append-only history); compaction that rewrites history
  needs to not silently defeat prompt caching where it's in use.

## Next step

Design the compaction algorithm/prompt deliberately before implementing —
this is tracked as a backlog item, not scheduled work.
