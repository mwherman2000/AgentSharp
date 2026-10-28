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
| The command prompt (`21 ray>`) | nickname |
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
* **Command prompt prefix:** `ReplHost.PromptPrefix(number, superPrompt)` builds it fresh for every prompt (`21 ray>`), so it follows `/clear <persona>`, a handoff made by the model and `/return`. The Lucy app resolves the name per prompt for the same reason.
* **One stack format.** `/status`, `/return`, `/load`, the console line and the history record all use `AgentSession.DescribeHandoffStack()`: `Andy Pando > Ray Pando > Donald J. Pando (current)`, or `(empty)`.

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
