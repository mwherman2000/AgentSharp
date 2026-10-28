# Backlog: `/handoff` REPL command

| | |
|---|---|
| Status | **Backlogged.** The `handoff` tool and `AgentSession.Handoff` are built; the command is not. |
| Date | 2026-10-28 |
| Raised by | Michael W. Herman |
| Affects (when taken up) | `AgentSharpApp/Ui/ReplHost.cs` (command handling and the `/help` table), `AgentSharpApp/Cli/` (command parsing if commands are parsed there), [HANDOFFDESIGN.md](../HANDOFFDESIGN.md) |

---

## 1. The idea

A `/handoff <persona>` command that hands the running conversation to another persona
without involving the model. Today the user can only ask in plain language ("hand this to
Ray") and rely on the model to call the `handoff` tool.

## 2. Why

* A command always works. The model may decline, or hand off when the user did not ask.
* It would sit beside `/clear [persona]`: `/clear` starts a fresh conversation under a
  persona, `/handoff` keeps the history.
* It needs no new library code. `AgentSession.Handoff(persona, reason)` already validates,
  rebuilds the loop on the existing history, and prints the console line.

## 3. Sketch

* `/handoff <persona> [reason...]`, taking effect on the next prompt, like the tool.
* An unknown name prints the valid names and leaves the session unchanged
  (`Handoff` throws `ArgumentException` before changing anything).
* Decide whether the command is limited to `SystemPromptBuilder.HandoffPersonas` like the
  tool, or accepts any persona like `AgentSession.Handoff` and `/clear`. Accepting any
  persona is the likelier fit for a user-driven command.
* Add a row to the `/help` table (`ReplHost.cs`, near the `/clear, /cls [persona]` row) and
  list the valid personas the same way.
* Tests: handoff via the command keeps the history count, changes `SuperPrompt`, and an
  unknown name changes nothing.

## 4. Related items, also not built

* **Cooldown.** Self-handoff and two requests in one turn are rejected, but A to B to A on
  consecutive turns is not. Add a minimum number of turns between handoffs if the model
  ping-pongs.
* **Persisting the handoff-received note across `/save` and `/load`.** The stack is saved
  and restored (so `/return` works after a load), but the note saying who handed over and
  why is not, so a loaded conversation starts without it in its system prompt.
* **Per-prompt persona attribution.** The persona in charge is told the names in the handoff
  stack (a peripheral awareness of who came before) but not which persona wrote which earlier
  turn. Telling it prompt by prompt would mean tagging each assistant turn with its author.
  The `H` records in the history already mark where each switch happened, so the author of
  every turn can be derived from them; the open question is how to show it to the model
  without spending tokens on every turn or breaking the prompt cache, for example a compact
  table of "prompts 1-4 Andy, 5-9 Ray" in the system prompt, or a prefix on each assistant
  turn in the request only.
* **More handoff targets.** Add keys to `SystemPromptBuilder.HandoffPersonas`; the schema,
  catalog and validation follow from it.
