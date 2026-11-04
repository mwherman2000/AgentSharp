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
dotnet run --project AgentSharpApp
```

Or publish and install it as a `agentsharp` executable on your `PATH`:

```bash
dotnet publish AgentSharpApp -c Release -o ./publish
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
    --timeout <minutes>  Request timeout (default 60)
    --max-tokens <n>     Max output tokens per request (default 128000)
    --max-iterations <n> Max LLM/tool round-trips per turn (default 100)
    --dir <path>         Project directory to run in (default: current directory)
    --Superprompt <name> Persona to start as (default: andy; see "Personas" below)
    --prompt <text>      One-shot mode (a bare first argument does the same)
-h, --help               Show help
-v, --version            Show version
```

Settings come from `~/.agentsharp/config.json`, then the `AGENT_*` variables, then these flags. The
Lucy app (`AgentLucyApp`) takes the same flags and has the same commands; it starts as `lucy` and keeps
its saved sessions in `~/.agentsharp/lucy`. `--help` in either app prints the full lists.

### Example

```bash
export ANTHROPIC_API_KEY=sk-ant-...
dotnet run --project AgentSharpApp
```

## REPL commands

| Command | Description |
|---|---|
| `/help` | Show commands |
| `/exit` | Exit the agent |
| `/clear [persona]` | Start a new conversation, optionally as another persona |
| `/personas` | List the personas with their roles; created ones are tagged `[user]` or `[project]` |
| `/handoff <persona> [reason]` | Hand the conversation, with its history, to another persona |
| `/return [reason]` | Hand it back to the persona that handed it over |
| `/compact -N \| +N \| N` | Drop the oldest turns (`-N` drops N%, `+N` keeps N%); saves before and after |
| `/save [id]` | Save session (with a `.docx` transcript) |
| `/load <id>` | Load session |
| `/sessions` | List sessions |
| `/status` | Agent status, including the available superprompts |
| `/model` | Show the current provider and model |
| `/memory [clear]` | View or delete memory |
| `/transcribe [name]` | Write a Q&A transcript |
| `/request` | Toggle request trace |
| `/history` | Toggle history trace |
| `/tools` | Toggle tools trace |
| `/sync` | Toggle non-streaming vs streaming responses |
| `/jaeger [endpoint]` | Switch OTel export to Jaeger (OTLP, default `http://localhost:4317`) |

## Personas (superprompts)

Each persona is one Markdown file: a short header, then the prompt. The 13 built-in ones are in
`AgentSharpLib/Superprompts/` (andy, angie, connie, donald, fed, jeff, lucy, raquel, ray, jeff-ray,
rosie, code, alchemist) and are compiled into the library. At startup the apps also read:

| Folder | Can |
|---|---|
| `~/.agentsharp/superprompts/` | add personas, or replace a built-in one |
| `.agentsharp/superprompts/` in the working directory | add personas only (a cloned repo cannot rewrite a persona you trust) |

```
---
name: Tess Pando
aliases: tea
role: tea expert who recommends teas
handoff: false
order: 200
---
You are Tess Pando, a tea expert. ...
```

The file name (lower case letters, digits, `-`, `_`) is the persona's name. A file watcher reloads
the folders whenever a file is added, changed or deleted; an invalid file is skipped with a warning and
the last good version of a persona stays in use. `handoff: true` lets the other personas hand
conversations to it. See [docs/ALCHEMISTDESIGN.md](docs/ALCHEMISTDESIGN.md) for the format and rules.

The **alchemist** persona designs a new persona with you and creates it with the `create_digital`
tool, which is a Destructive tool: you are shown the whole persona and approve it every time ("always
allow" does not apply to it). Debugging and a demo script are in
[docs/DEBUGALCHEMIST.md](docs/DEBUGALCHEMIST.md).

## Project structure

```
AgentSharpLib/     Reusable agent library
  Agent/          Agent loop, multi-agent orchestration
  Context/        Project context scanning, system prompt building, persona catalog, registry and file watcher
  Superprompts/   One Markdown file per built-in persona (embedded in the assembly)
  CommandLineParser.cs  Flags, environment and config file, shared by both apps
  Llm/            LLM clients (Anthropic, OpenAI-compatible)
  Memory/         Persistent memory and session management
  Output/         IAgentOutput -- host-rendered progress/status events
  Safety/         Approval gate and shell command risk classification
  Telemetry/      OpenTelemetry tracing
  Tools/          Tool registry and built-in tool implementations
  Transcripts/    Markdown / .docx transcript rendering
AgentSharpApp/     Interactive chat CLI (entry point)
  Ui/             REPL host, command parsing, Spectre renderer, approval prompt
  *_prompts/      Prompt source material (GTM, Lucy, O*NET)
AgentLucyApp/      Plain-text chat that starts as Lucy -- same flags and commands as the main app, a small AgentSharpLib sample
DigitomicEvolutionLib/  Library for digitomic evolution (new, empty)
DigitomicEvolutionLib.Tests/  xUnit tests for DigitomicEvolutionLib (new, empty)
AgentSharp.Tests/  xUnit test suite, mirrors the source layout
docs/              Design notes (e.g. streaming vs. sync)
```

## Using the library

Reference `AgentSharpLib` and build an agent with `AgentBuilder`:

```csharp
using AgentSharpLib;

var agent = await new AgentBuilder()
    .WithOptions(new AgentOptions { Provider = "anthropic", ApiKey = apiKey })
    .WithSuperPrompt("lucy")          // optional persona (see "Personas" above)
    .BuildAsync();

var answer = await agent.SendAsync("Summarize this repository.");
```

By default the agent gets all built-in tools, sub-agents, project memory (`MEMORY.md`), and a
system prompt built from the persona plus a scan of the current directory. Output is discarded
and Destructive tools (e.g. `run_shell`) are denied unless you plug in your own:

- `WithOutput(IAgentOutput)` — receive streamed text and status events (the CLI's is `SpectreAgentOutput`)
- `WithApprovalPrompt(IApprovalPrompt)` — decide on Destructive tool calls (the CLI's is `ConsoleApprovalPrompt`)
- `WithTool`, `WithSystemPrompt`, `WithLlmClient`, `WithoutBuiltInTools`, `WithoutSubAgents`, `WithoutMemory`, `WithoutProjectScan`

`AgentSession.Reset(persona?)` starts a new conversation and `Restore(history)` resumes a saved one.
The library never changes the process working directory; tools resolve paths against it.

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
