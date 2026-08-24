# AgentSharp

**AgentSharp** is a terminal-based AI coding agent for .NET, built around the same core architectural patterns as Claude Code: an agentic think → decide → execute → observe loop, a self-discovering tool registry, tiered safety/approval gates, project-context awareness, streaming LLM responses, and persistent memory/session state.

Point it at a project directory, give it a task in plain English, and it reads, edits, and runs commands in your codebase — asking for confirmation before anything destructive.

## Features

- **Multi-provider LLM support** — Anthropic (native API with prompt caching), OpenAI, xAI (Grok), Google Gemini, local Ollama models, and any OpenAI-compatible endpoint via `--base-url`.
- **Self-discovering tool registry** — file read/write/edit, directory listing, grep, shell execution, web fetch, sub-agent delegation, and memory tools all register themselves and declare a risk level.
- **Tiered safety model** — read-only tools run automatically, write tools run and log, and destructive operations (`rm -rf`, `sudo`, force pushes, etc., detected by a shell command classifier) always prompt for approval.
- **SSRF-hardened web fetch** — the web fetch tool blocks requests to private/internal network addresses.
- **Multi-agent orchestration** — the primary agent can delegate sub-tasks to sub-agents via a dedicated orchestrator and tool.
- **Persistent memory & sessions** — a per-project `MEMORY.md` carries facts and preferences across runs; conversations can be saved and reloaded by session ID.
- **Streaming by default** — token-by-token streaming responses, with a `/sync` toggle to fall back to non-streaming request/response for debugging or comparison.
- **Interactive REPL or one-shot mode** — drive it conversationally or fire a single prompt and exit, ideal for scripting.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- An API key for at least one supported provider (or a local [Ollama](https://ollama.com) install, which needs no key)

## Build

```bash
git clone https://github.com/mwherman2000/AgentSharp.git
cd AgentSharp
dotnet build
```

## Run

```bash
dotnet run --project AgentSharp
```

Or publish and install it as a `agentsharp` executable on your `PATH`:

```bash
dotnet publish AgentSharp -c Release -o ./publish
```

## Configuration

AgentSharp resolves configuration in this order: CLI flags → environment variables → `~/.agentsharp/config.json` → built-in defaults.

### Environment variables

| Variable | Purpose |
|---|---|
| `ANTHROPIC_API_KEY` | API key for Anthropic (Claude) |
| `OPENAI_API_KEY` | API key for OpenAI |
| `XAI_API_KEY` | API key for xAI (Grok) |
| `GEMINI_API_KEY` | API key for Google (Gemini) |
| `AGENT_PROVIDER` | Default provider (`anthropic`, `openai`, `grok`, `gemini`, `ollama`) |
| `AGENT_MODEL` | Default model |
| `AGENT_API_KEY` | Generic API key (any provider) |
| `AGENT_BASE_URL` | Custom API base URL for OpenAI-compatible providers |
| `AGENT_ENABLE_OTEL` | Emit OpenTelemetry traces (turn/LLM-call/tool-call spans) via the console exporter (default: off) |

Ollama needs no API key — just run `ollama serve` locally (default `http://localhost:11434/v1`).

### CLI flags

```
agentsharp                          Start interactive REPL
agentsharp "fix the bug in main.cs"  One-shot mode
agentsharp --prompt "explain this"   One-shot mode (explicit)

-p, --provider <name>    LLM provider: anthropic, openai, grok, gemini, ollama
-m, --model <name>       Model identifier (e.g., claude-sonnet-5, gpt-4o)
-k, --api-key <key>      API key (or set via environment variable)
    --base-url <url>     Custom API base URL for compatible providers
-h, --help               Show help
-v, --version            Show version
```

### Example

```bash
export ANTHROPIC_API_KEY=sk-ant-...
dotnet run --project AgentSharp
```

## REPL commands

| Command | Description |
|---|---|
| `/help` | Show commands |
| `/exit` | Exit the agent |
| `/clear` | Clear conversation |
| `/save` | Save session |
| `/load <id>` | Load session |
| `/sessions` | List sessions |
| `/status` | Agent status |
| `/memory` | View memory |
| `/request` | Toggle request trace |
| `/history` | Toggle history trace |
| `/tools` | Toggle tools trace |
| `/sync` | Toggle non-streaming vs streaming responses |

## Project structure

```
AgentSharp/
  Agent/          Agent loop, system prompt building, multi-agent orchestration
  Context/        Project context scanning
  Llm/            LLM clients (Anthropic, OpenAI-compatible)
  Memory/         Persistent memory and session management
  Safety/         Approval gate and shell command risk classification
  Tools/          Tool registry and built-in tool implementations
  Ui/             REPL host, command parsing, console rendering
AgentSharp.Tests/  xUnit test suite, mirrors the source layout
docs/              Design notes (e.g. streaming vs. sync)
```

## Testing

```bash
dotnet test
```

## Safety model

Every tool declares a `ToolRiskLevel`:

- **ReadOnly** — runs automatically (e.g. reading a file, listing a directory)
- **Write** — runs automatically and is logged (e.g. editing a file)
- **Destructive** — always prompts for approval before running, with the shell command classifier explaining *why* a command is considered risky (e.g. `rm -rf`, `sudo`, `git push --force`)

## License

Released under the [MIT License](LICENSE.md).
