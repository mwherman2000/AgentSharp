# `--max-tokens` and local model context windows

`AgentLoop` sends `max_tokens` on every request via `BuildRequest()`
(`AgentSharp/Agent/AgentLoop.cs`). It defaults to `AgentLoop.DefaultMaxTokens`
(**128,000**) and is configurable per run:

| Source | Flag / var |
|---|---|
| CLI | `--max-tokens <n>` |
| Environment | `AGENT_MAX_TOKENS` |
| Config file | `max_tokens` in `~/.agentsharp/config.json` |

Precedence follows the rest of `Configuration` (env → config file → CLI, CLI
wins). The resolved value flows from `Program.cs` through `ReplHost`,
`AgentOrchestrator`, and `SubAgent` into every `AgentLoop` instance — including
sub-agents spawned via the `sub_agent` tool — so it only needs to be set once
per process.

128,000 is a reasonable default for Claude models: `max_tokens` is an *output*
cap, and Claude's context window (up to 1M tokens) is large enough that a 128K
output allowance barely dents the budget left over for the system prompt,
tool definitions, and conversation history.

## Why the default is wrong for small local models

On a local model served through Ollama (`OpenAiCompatibleClient.ForOllama`),
`max_tokens` and the *input* (system prompt + tool schemas + history) draw
from the **same** context window — there's no 1M-token cushion. If
`--max-tokens` is set anywhere close to the model's `num_ctx`, there's little
or no room left for input, and requests will fail or silently truncate
context.

Concretely: `qwen3:4b-24k` (a `qwen3:4b` Modelfile variant with `num_ctx`
capped at 24,576) cannot sensibly take `--max-tokens 128000` — that's over 5x
its entire context window. `--max-tokens` must stay comfortably below
`num_ctx`, leaving headroom for everything else in the request.

## Can a small model's context window just be raised instead?

Sometimes, but it's a real tradeoff, not a free upgrade — worth understanding
before reaching for it.

Qwen3-4B's *native* context length is 32,768 tokens. It can be extended to
131,072 (128K) via YaRN rope scaling, and Qwen's own validation covers
performance up to that length ([Qwen/Qwen3-4B on Hugging
Face](https://huggingface.co/Qwen/Qwen3-4B)). In an Ollama Modelfile:

```
FROM qwen3:4b
PARAMETER num_ctx 131072
PARAMETER rope_scaling yarn
```

Two caveats before doing this:

1. **All open-source runtimes (Ollama/llama.cpp included) implement *static*
   YaRN** — the scaling factor is fixed regardless of the actual prompt
   length. Turning it on can hurt quality on short turns too, not just help
   long ones. General guidance is to only enable it when the long context is
   actually needed for that run ([static YaRN
   tradeoff](https://zolotukhin.ai/blog/2026-05-24-static-yarn-buys-qwen3-128k-by-taxing-every-short-prompt/)).
2. **Memory scales with context.** Raising `num_ctx` from 24K/32K to 128K
   meaningfully increases KV-cache RAM/VRAM usage even on a 4B model.

YaRN-stretched context is also generally lower quality at the far end than a
model natively trained at that length — recall and coherence degrade more on
a small model stretched 4x beyond its native window than on a large model.
For an agentic loop that already leans on a small model for reliable
tool-calling, stacking a stretched context on top adds more risk, not less.

## Recommendation for small local models

Prefer the native context window over YaRN extension unless it's genuinely
not enough:

- Leave `num_ctx` at the model's native length (e.g. 32K for Qwen3-4B) rather
  than YaRN-extending to 128K.
- Set `--max-tokens` well below `num_ctx` — a few thousand tokens, not tens of
  thousands — leaving room for the system prompt, growing conversation
  history, and tool schemas. For `qwen3:4b-24k`, `--max-tokens 6000` is a
  reasonable starting point (see `AgentSharp/Properties/launchSettings.json`).
- Only reach for YaRN-extended context if a task genuinely needs more input
  headroom than the native window provides, understanding it trades away some
  quality on shorter turns.

Sources:
- [Qwen/Qwen3-4B · Hugging Face](https://huggingface.co/Qwen/Qwen3-4B)
- [Static YaRN buys Qwen3 128k context by taxing every short prompt](https://zolotukhin.ai/blog/2026-05-24-static-yarn-buys-qwen3-128k-by-taxing-every-short-prompt/)
- [unsloth/Qwen3-8B-128K-GGUF · Does YaRN gguf works with ollama?](https://huggingface.co/unsloth/Qwen3-8B-128K-GGUF/discussions/2)
