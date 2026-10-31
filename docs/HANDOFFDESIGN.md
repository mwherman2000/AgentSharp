# Handoff design

Handoff passes a running conversation, with its full history, from one persona (superprompt) to another, for example Ray to Donald. The new persona takes over on the user's next prompt.

## Goals

* The model can hand off on its own when another persona would do the job better.
* The user can ask for a handoff in plain language ("hand this to Ray").
* The conversation history is kept intact: nothing is dropped, rewritten or re-counted.
* The new persona knows it has taken over and who wrote the earlier turns.

## Behavior

| Item | Behavior |
|---|---|
| Tool | `handoff(persona, reason)`, risk level `ReadOnly` (no approval prompt) |
| Targets | `ray`, `donald`, `andy`, `lucy`, `connie` (`SystemPromptBuilder.HandoffPersonas`) |
| When it takes effect | After the current turn finishes, for the user's next prompt |
| History | The same `ConversationHistory` instance continues; prompt numbers carry on |
| Console | `Handoff: Andy Pando -> Donald J. Pando (reason)` printed through `IAgentOutput.Info`; the prompt label changes on the next prompt |
| Not offered | With `AgentBuilder.WithoutHandoff()`, or with a custom system prompt (`WithSystemPrompt`) |

The tool rejects, with an error the model can read: a persona outside the target list, an unknown name, the persona already in charge, and a second handoff queued in the same turn.

## Return

A handoff pushes the persona being left onto a stack (`AgentSession.HandoffStack`, most recent last). **Return** pops it and hands the conversation back, on the same history. The term is always "return".

| Way to return | Behavior |
|---|---|
| `/return [reason]` | Returns now (for the next prompt) and prints the stack. With an empty stack it says there is nothing to return to and prints `(empty)`. |
| `handoff` tool, `persona: "return"` | The model hands back after the turn, like any handoff. Offered in the persona catalog as `- return: hand the conversation back to <persona>` only while the stack is not empty; rejected with an error otherwise. |
| `AgentSession.Return(reason)` | Library call. Throws `InvalidOperationException` when the stack is empty. |

* The target can be any persona that was ever on the stack, including ones that are not handoff targets (for example a session that started as `rosie`).
* The system prompt says the conversation was **returned** (not handed over) to the persona, and that earlier turns include its own from before the handoff.
* Handing off to a persona already in charge does nothing and pushes nothing. A custom system prompt is not a persona, so it is never pushed.
* `Reset` (`/clear`) empties the stack. `Restore` empties it unless a `HandoffState` is passed.

### Stack display

`/return` and `/status` print the stack on one line, oldest first, ending with the persona in charge:

```
Handoff stack: Andy Pando > Ray Pando > Donald J. Pando (current)
Handoff stack: (empty)
```

### Nicknames and full names

Each persona has a nickname (`ray`, `donald`) and a full name (`Ray Pando`, `Donald J. Pando`). Where each is used:

| Where | Spelling |
|---|---|
| Commands and tool arguments (`/clear ray`, `handoff(persona: "ray")`) | nickname |
| The command prompt (`21 Ray Pando>`) | full name |
| Other console text (`/status`, `/return`, `/load`, the handoff line, the welcome banner) | full name |
| The transcript, including `H` entries | full name |
| What the model is told (handoff note, handoff stack, `return` line) | nickname |

`/load` prints it too when the loaded conversation has one.

### Save and load

`/save` stores the stack and the persona in charge in the session file (`handoff_stack`, `handoff_persona`), only when the stack is not empty. `/load` restores both with `AgentSession.Restore(history, handoffState)`, so the persona in charge changes to the one that was saved and `/return` works. A session saved without a stack loads as before: the current persona stays and the stack is empty. A saved state naming a persona this build does not have is ignored. The handoff-received note (who handed over and why) is not saved.

### What the new persona knows about the stack

The system prompt gets a `# Handoff stack` section whenever the stack is not empty, naming everyone in order with the persona in charge last:

```
Handoff stack, oldest first: andy > ray > donald (you).
```

It also says earlier assistant turns may have been written by any of the others and that the persona does not need to work out which turn was whose. This is a peripheral awareness of who came before, not an attribution of each turn. The section comes from the stack itself, so it is still there after `/load`, which restores the stack but not the handoff-received note.

## Record in the history, transcript and prompt

Every handoff and return adds an assistant-role record to the history (`PromptKind.Hnd`, code `hnd`, label `H`), for example:

```
Handoff: Andy Pando -> Ray Pando (needs the principles). Handoff stack: andy > ray (current)
Return: Ray Pando -> Andy Pando. Handoff stack: (empty)
```

* **Never sent to a provider.** `AgentLoop.BuildRequest` filters it out, like the `/compact` record. `GetLastAssistantText` skips it.
* **Saved** by `/save` and restored by `/load` with the rest of the history. It is not a prompt, so it does not use up a prompt number.
* **Transcript:** its own entry, `H21`, with the number of the prompt whose turn it ended (the same rule as `E21` and `M21`) and no answer part. The transcript's header still shows the persona in charge when it was written.
* **Console:** the same text, printed through `IAgentOutput.Handoff`, in magenta in the CLI. `/return` prints this line (it is the only output of a successful return); with nothing to return to it prints the notice and `(empty)`.
* **Command prompt prefix:** `ReplHost.PromptPrefix(number, superPrompt)` builds it fresh for every prompt (`21 Ray Pando>`), so it follows `/clear <persona>`, a handoff made by the model and `/return`. The Lucy app resolves the name per prompt for the same reason.
* **`/status` is recorded the same way.** Each `/status` adds a `PromptKind.Sts` record (code `sts`, label `T`) holding the listing exactly as printed, so `/save` keeps it and a transcript shows it as `T21`, in a preformatted block, with the number of the prompt it follows and no answer part. Like the `M` and `H` records it is never sent to the model, is not "the last answer", and counts for nothing in the context size: `PromptKinds.IsLocalRecord` names the three kinds, and the provider's exact count stays exact when one is added. Both apps record it.
* **One stack format.** `/status`, `/return`, `/load`, the console line and the history record all use `AgentSession.DescribeHandoffStack()`: `Andy Pando > Ray Pando > Donald J. Pando (current)`, or `(empty)`.

## Automatic saves

Saves are taken programmatically (`AgentSession.AutoSave`): no checkpoint prompt and no model call, so they are instant and cost no tokens. Each writes the session file with the handoff stack, and optionally a `.docx` transcript of the same name in the project directory. Names are `<persona>-<kind>-<date>_<24-hour time>-<zone>`, for example `ray-handoff-donald-2026-10-29_14-05-33-MDT` (no colons or spaces, which a Windows file name cannot hold). The persona is the nickname of the one in charge when the save is taken.

| When | Name | Transcript |
|---|---|---|
| Start of a handoff (`AgentSession.Handoff`) | `ray-handoff-donald-<time>` | yes |
| Before a `/compact` | `ray-compact-before-<time>` | yes |
| After a `/compact` | `ray-compact-after-<time>` | no |
| `/save` with no name | `ray-saved-<time>` | yes, as `/save` always writes one |
| `/transcribe` with no name (flags alone, such as `/transcribe --tools`, work too) | `ray-transcription-<time>.docx` | the transcript itself, a Word file like the one `/save` writes; a name you give without `.docx` is still Markdown |

* **Handoff:** the save is the first thing `Handoff` does, after the target is validated and a handoff to the persona already in charge is ruled out (those save nothing). It is in `Handoff` and not in the tool's `ExecuteAsync` because the tool runs mid-turn, when the history ends with its own unanswered tool call; a session saved then would not load as a consistent conversation. `Handoff` runs right after the turn, so the file holds the conversation as it was, still under the persona being left. A failed save is reported as a warning and the handoff goes ahead, since the handoff loses nothing.
* **Compact:** `AgentSession.Compact` saves before, compacts, then saves after. If the first save fails, nothing is dropped. If nothing needed dropping, the second save is skipped because nothing changed. The "before" file holds every turn; the "after" file holds the shortened conversation and the compaction record.
* **Only the first save has a transcript.** The "after" save of a compact is the session file alone.
* **`/return` does not save.** Only the handoff that leaves a persona does.
* **No session store, no saves.** `AgentBuilder.WithSessionManager` supplies the store (both apps pass the one they use for `/save` and `/load`). A library user who does not pass one gets no automatic saves, and handoff and compact work as before.
* **Naming is shared.** `AgentSession.SaveName(kind)` builds the name; `LocalTime.FileStamp` the date, time and zone.

## Why the switch is deferred

The system prompt is fixed for the life of an `AgentLoop` (`_systemPrompt` is readonly), so a persona change means building a new loop. The tool runs inside the loop, mid-turn, when the history ends with a `tool_use` block that has no `tool_result` yet. Replacing the loop at that point would orphan the tool call.

So the tool only records a request in a `HandoffQueue`. `AgentSession.SendAsync` takes the request in a `finally` block after the turn returns and calls `Handoff`, which builds a new loop around the existing history. The current prompt is therefore always answered by the persona that received it.

```
user prompt -> loop runs turn
                 model calls handoff(donald, reason)
                   HandoffTool: validate, queue.Request(donald, reason), return "queued"
                 model finishes its answer
             <- turn returns
SendAsync finally: queue.Take() -> session.Handoff(donald, reason)
                     _superPrompt = donald, _handoffContext = "..."
                     Loop = CreateLoop(Loop.History)       // same history, new system prompt
next user prompt -> new loop answers as Donald
```

## Components

| Component | File | Role |
|---|---|---|
| `HandoffTool` | `AgentSharpLib/Tools/Implementations/HandoffTool.cs` | The model-facing tool. Validates the target and queues the request. No parameterless constructor, so `DiscoverTools` skips it and `AgentBuilder` registers it. |
| `HandoffQueue` | same file | Holds the pending `(persona, reason)` and the current persona's canonical name (set by the session, used to reject self-handoff). |
| `AgentSession.Handoff` | `AgentSharpLib/AgentSession.cs` | Validates, records who handed over and why, rebuilds the loop on the existing history, prints the console line. Public, so a host can call it directly. Unlike the tool, it accepts any persona. |
| `AgentSession.SendAsync` | same file | Applies a pending request after the turn. |
| `AgentSession.Reset` / `Restore` | same file | Clear the handoff context and any pending request: a fresh or loaded conversation was not handed over. |
| `SystemPromptBuilder` | `AgentSharpLib/Context/SystemPromptBuilder.cs` | Adds the persona catalog and the handoff-received note. Owns `HandoffPersonas`, `ResolveKey` and the one-line role of each persona. |
| `AgentBuilder.WithoutHandoff` | `AgentSharpLib/AgentBuilder.cs` | Opts out. |
| `SubAgent` | `AgentSharpLib/Agent/MultiAgent/SubAgent.cs` | Filters `handoff` out of a sub-agent's tools, since a sub-agent has no conversation to hand over. |

## System prompt changes

Two optional sections are added, both before `# Memory` because `ConversationTranscript.ExtractRecalledMemory` expects memory to be last.

**Persona catalog** (every persona that has the tool): when to hand off, and the other targets with a one-line role each. The current persona is omitted. The text tells the model to answer the current prompt first; to hand off when the user asks for a persona by name, or when its own answer would be clearly worse (otherwise just answer); and not to hand off to itself or bounce between personas. The tool description carries the same rule, plus: at most one call per turn, `return` only after the conversation was handed to you, and `reason` is one sentence on what the user needs. A test checks that the two texts agree.

**Handoff received** (only after a handoff): who handed the conversation over, the reason, and a statement that earlier assistant turns were written by that persona or an earlier one, not by the new persona. The history is left untouched so the model API's message alternation and the prompt numbering are not disturbed. The context lives in the system prompt instead.

## Tool schema

```json
{
  "persona": { "type": "string", "enum": ["ray", "donald", "andy", "lucy", "connie"] },
  "reason":  { "type": "string" }
}
```

Both are required. The reason is shown to the user in the console line and to the new persona in the handoff-received note.

## Cost and risks

* **Prompt caching.** The system prompt comes first in the request, so a persona change invalidates the Anthropic prompt cache. The first prompt after a handoff pays full input price for the whole history. Handoffs are fine occasionally, not every turn.
* **Persona bleed.** The new persona sees the old persona's turns as assistant turns. The handoff-received note mitigates this but the model can still imitate the earlier voice.
* **Over- and under-triggering.** Models tend to either never hand off or hand off too eagerly. The "only when clearly worse" wording is the lever; tune it from observed behavior.
* **Ping-pong.** There is no cooldown. Self-handoff and a second request in one turn are rejected, but A to B to A across consecutive turns is not. Add a minimum number of turns between handoffs if it shows up.
* **Cancelled or failed turns.** A request queued during a turn that then throws or is cancelled is still applied, because the tool call was already acknowledged to the model.

## Not built

* A `/handoff <persona>` REPL command that calls `AgentSession.Handoff` directly, for a handoff that does not depend on the model choosing to call the tool (see `docs/backlog/Handoff-REPL-Command.md`). `/return` exists; `/handoff` does not.
* A cooldown between handoffs.
* Handoff targets beyond the five initial personas. Add a key to `HandoffPersonas`; the schema, catalog and validation follow from it.
* Per-prompt persona attribution: telling the persona which persona wrote each earlier turn (see `docs/backlog/Handoff-REPL-Command.md`). Today it only knows who is in the stack.
* Persisting the handoff-received note across `/save` and `/load`. The stack is saved; the note (who handed over and why) is not, so a loaded conversation starts without it.

## Try it: a six-step demo

Needs `ANTHROPIC_API_KEY` set. It spends a handful of real model calls. Run the app with `dotnet run --project AgentSharpApp -- --superprompt andy`, then type each line below. The `Handoff:` and `Return:` lines print in magenta.

| # | Type | You should see |
|---|---|---|
| 1 | *(start the app)* | The prompt reads `1 Andy Pando>`. |
| 2 | `Hand this conversation to Ray.` | Andy says it is handing over, then `Auto-saved andy-handoff-ray-<time> (transcript: ...)` and `Handoff: Andy Pando -> Ray Pando (...). Handoff stack: Andy Pando > Ray Pando (current)`. The next prompt reads `2 Ray Pando>`. |
| 3 | `Which persona are you, and who handed you this conversation?` | Ray answers and names Andy. `/status` shows `Handoff stack: Andy Pando > Ray Pando (current)`. |
| 4 | `/return` | `Return: Ray Pando -> Andy Pando. Handoff stack: (empty)`. The prompt is `Andy Pando>` again. |
| 5 | `I'd like to talk with a persistent digital person who has memory and relationships.` | Andy hands off to Lucy on its own: a `Handoff: Andy Pando -> Lucy Pando` line, and the next prompt is `Lucy Pando>`. |
| 6 | `/save`, then `/transcribe`, then `/clear`, then `/load <the lucy-saved-... name>`, then `/status` | The files `lucy-saved-<time>` and `lucy-transcription-<time>.docx` are written. After the load, `/status` shows `Handoff stack: Andy Pando > Lucy Pando (current)`. In the transcript, each switch is an `H` entry. |

Not all in one go? Steps 2 to 4 alone show a handoff, a persona that knows its history, and a return. For the same checks run for you, with no typing, run `dotnet run --project AgentSharp.HandoffLive` (see its README).

## Fixes made while testing it

Two problems turned up in real use and are fixed; neither is specific to handoff.

* **A dropped connection no longer derails a turn.** When a request got no response (a reset or dropped connection, a timeout), the loop told the model "the previous request failed ... try a different approach". The model never saw the request, so it did something else; a dropped `/save` checkpoint produced a long unrelated memory entry. Now the same request is resent unchanged, with no error text or `sys>` prompt in the history. Errors the API answered with a status code behave as before.
* **A lost network is waited out.** On such a failure the loop checks, separately, whether the LLM's host can be reached: an adapter with a gateway, DNS, and a TCP connection, each capped at a few seconds, using only the .NET network classes. It prints one line, for example `Network: Wi-Fi up; DNS ok; api.anthropic.com:443 timed out`. If the host cannot be reached it waits up to 30 seconds, checking every 2, and resends. That wait is not one of the three retries, so three wifi drops in a row no longer use them up in under four seconds. If the network never comes back the turn stops with a clear message; Ctrl+C ends the wait. A local (loopback) endpoint such as Ollama is not checked. `ILlmClient.Endpoint` names the host to check, and `AgentLoop` takes an `IConnectivityProbe` so tests can script the network.
* **The context-size estimate no longer explodes on the first turn.** After a turn it scaled the whole message estimate by the provider's count over the message estimate. The provider's count includes the fixed ~15,000 tokens of system prompt and tools, so after "who are you" (about 3 tokens) the ratio was about 5,000 and the first reply came out as 2.5 million tokens, "251% full", with a false 95% warning. The estimate is now the provider's count plus the estimated growth since, with the ratio measured on the messages alone and kept between 0.5 and 2.0. `/compact` reports the provider's count as "before" and that less the dropped turns as "after".

## Live check against the real API

`AgentSharp.HandoffLive` (a project in the solution, not part of `dotnet test`) runs the scenarios below against the real model and prints what it did. A run on 2026-10-29 with `claude-sonnet-5-5` behaved as expected in all of them: an explicit request handed off to Ray, Ray knew Andy had handed it over, and "hand it back" used `return`; a request for a persistent digital person went from Andy to Lucy, and a go-to-market request went from Ray to Andy; an in-domain question and `17 × 23` did not hand off; and a request for Rosie, who is not a target, was refused with the valid personas listed. The full output, with the personas named, is in the project's [README](../AgentSharp.HandoffLive/README.md#sample-output). It is one sample: the "fit" scenarios are judgment calls, so re-run it after changing the tool description or the persona catalog.

## Tests

`AgentSharp.Tests/Agent/AgentBuilderTests.cs`:

* a model-issued handoff switches persona after the turn and keeps all four history messages;
* the tool alone does not switch mid-turn;
* rosie (not a target), an unknown name and the current persona are rejected;
* the catalog lists only the other targets;
* the handoff-received note names both personas, and `Reset` clears it;
* no tool with `WithoutHandoff()` or a custom system prompt;
* `Return` pops the stack, keeps the history and says "returned" in the system prompt; it throws on an empty stack;
* the tool's `return` target returns after the turn and is rejected with nothing to return to;
* `Reset` and `Restore` clear the stack, `Restore` with a `HandoffState` brings it back.

`AgentSharp.Tests/Memory/SessionManagerTests.cs`: the handoff state round-trips through a session file, and is null for a session saved without one. `AgentSharp.Tests/Ui/CommandParserTests.cs`: `/return` parses.
