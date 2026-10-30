# Backlog: `/handoff` REPL command

| | |
|---|---|
| Status | **Backlogged.** The `handoff` tool, `AgentSession.Handoff` and the `/return` command are built (commit `9468c77`); `/handoff` is not. |
| Date | 2026-10-28 (updated 2026-10-29) |
| Raised by | Michael W. Herman |
| Affects (when taken up) | `AgentSharpApp/Ui/ReplHost.cs` (command handling and the `/help` table), `AgentSharpApp/Cli/` (command parsing if commands are parsed there), [HANDOFFDESIGN.md](../HANDOFFDESIGN.md) |

---

## 1. The idea

A `/handoff <persona>` command that hands the running conversation to another persona
without involving the model. Today the user can only ask in plain language ("hand this to
Ray") and rely on the model to call the `handoff` tool.

## 2. Why

* A command always works. The model may decline, or hand off when the user did not ask.
* It would sit beside `/clear [persona]` and `/return`: `/clear` starts a fresh conversation
  under a persona, `/handoff` keeps the history and pushes the persona being left onto the
  stack, and `/return` pops it. Today `/return` exists without its counterpart.
* It needs no new library code. `AgentSession.Handoff(persona, reason)` already validates,
  rebuilds the loop on the existing history, adds the `H` record to the history (so the
  transcript shows it), and prints the console line with the stack.

## 3. Sketch

* `/handoff <persona> [reason...]`, taking effect on the next prompt, like the tool. Add
  `Handoff` to `CommandType` and a `"handoff"` case in `CommandParser`, as was done for
  `Return`.
* The persona is given by nickname (`ray`, `donald`; aliases such as `trump` also resolve),
  like every command. The console confirmation uses the full name, the same
  `Handoff: Andy Pando -> Ray Pando ... Handoff stack: ...` line the session already prints.
* An unknown name prints the valid names and leaves the session unchanged
  (`Handoff` throws `ArgumentException` before changing anything); the `/clear` case in
  `ReplHost` shows the pattern.
* Handing off to the persona already in charge does nothing (`Handoff` returns early); say so
  instead of staying silent.
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
