# Debugging AgentSharp

All commands below are PowerShell, run from the VS Code integrated terminal (`` Ctrl+` ``).

> If you're using the [Runme](https://docs.runme.dev) extension to run these blocks directly, code fences are tagged ```` ```pwsh ```` rather than ```` ```powershell ```` — Runme doesn't recognize `powershell` as an executable language identifier and fails with "Cell language is not executable" otherwise.

## Running in the VS Code integrated terminal

1. Open the repo folder in VS Code (`code .` from the repo root, or File > Open Folder).
2. Open a terminal: `` Ctrl+` `` (or View > Terminal).
3. Set an API key for at least one provider in the current session (skip this if you're using local Ollama, which needs no key):

   ```pwsh
   $env:ANTHROPIC_API_KEY = "sk-ant-..."
   ```

4. Launch AgentSharp with `Start-Process` so it runs as its own process rather than tying up your terminal:

   ```pwsh
   Start-Process dotnet -ArgumentList 'run', '--project', 'AgentSharp'
   ```

   This opens AgentSharp's interactive REPL in its own console window. `Start-Process` spawns a child process that inherits the environment variables (like `ANTHROPIC_API_KEY`) already set in your current session, as long as you set them *before* calling `Start-Process`.

   To keep it attached to your current terminal window instead of opening a new one, add `-NoNewWindow`:

   ```pwsh
   Start-Process dotnet -ArgumentList 'run', '--project', 'AgentSharp' -NoNewWindow
   ```

5. One-shot mode (single prompt, then exit), waiting for it to finish and capturing output to a log file — useful when you want to inspect what happened after the fact rather than watch it live:

   ```pwsh
   Start-Process dotnet `
     -ArgumentList 'run', '--project', 'AgentSharp', '--', '--prompt', 'explain this codebase' `
     -NoNewWindow -Wait -RedirectStandardOutput agentsharp.log -RedirectStandardError agentsharp.err.log
   Get-Content agentsharp.log
   ```

   The `--` element in `-ArgumentList` separates `dotnet run`'s own arguments from the ones passed through to AgentSharp, same as it would on the command line.

## Trace toggles (inside the REPL)

Useful when diagnosing what's actually being sent to/from the LLM:

| Command | Shows |
|---|---|
| `/request` | Raw JSON of the full request sent to the LLM each turn |
| `/tools` | The tool definitions included in the request |
| `/history` | The full message history included in the request |
| `/sync` | Switches to non-streaming (`SendAsync`) instead of the default streaming path — useful for comparing behavior when diagnosing a streaming-specific issue (see `docs/streaming-vs-sync.md`) |
| `/status` | Current agent/session status |
| `/memory` | Current contents of `MEMORY.md` |

These toggles gate the `Console.WriteLine` tracing in `AgentLoop.cs`; they print raw, interleaved with the REPL's rendered UI.

## Structured tracing via OpenTelemetry

For a cleaner, span-based alternative (turn → LLM call → tool call hierarchy, with real attribution even when sub-agents run in parallel), set `AGENT_ENABLE_OTEL` before launching:

```pwsh
$env:AGENT_ENABLE_OTEL = "1"
Start-Process dotnet -ArgumentList 'run', '--project', 'AgentSharp' -NoNewWindow
```

To persist it across terminal sessions: `setx AGENT_ENABLE_OTEL 1` (only affects *new* terminals; add `/M` from an elevated prompt for machine-wide).

## Attaching the VS Code debugger (breakpoints)

With the C# Dev Kit (or OmniSharp) extension installed:

1. Open `AgentSharp/Agent/AgentLoop.cs` (or `Program.cs`) and set a breakpoint — e.g. in `RunTurnStreamingAsync`/`RunTurnNonStreamingAsync` to step through the think → decide → execute → observe loop, or in `ExecuteToolCallsAsync` to inspect a specific tool call.
2. Press `F5` (Run > Start Debugging). If no launch configuration exists yet, VS Code will offer to auto-generate one under `.vscode/launch.json` — accept it.
3. One-shot mode is easiest to step through, since it runs a single turn and exits instead of blocking on REPL input. In `.vscode/launch.json`, set the generated config's `args` to something like:
   ```json
   "args": ["--prompt", "say hi"]
   ```
4. Make sure your API key is available to the debug session — either set it with `$env:ANTHROPIC_API_KEY = "..."` in the terminal before pressing F5, or add an `"env"` block to the launch config:
   ```json
   "env": { "ANTHROPIC_API_KEY": "sk-ant-..." }
   ```

   Note: `F5` debugging runs the process in-process under the debugger rather than via `Start-Process` — `Start-Process` is for launching AgentSharp as an independent process from the terminal, not for attaching a debugger to it.

## Other useful terminal commands

```pwsh
dotnet build                                        # compile, surface warnings/errors
dotnet test                                         # run the xUnit suite (AgentSharp.Tests)
Start-Process dotnet -ArgumentList 'run', '--project', 'AgentSharp', '--', '--help' -NoNewWindow -Wait   # show all CLI flags and env vars
```

## Config resolution order

CLI flags → environment variables → `~/.agentsharp/config.json` → built-in defaults. See [README.md](README.md) for the full environment variable and CLI flag tables.
