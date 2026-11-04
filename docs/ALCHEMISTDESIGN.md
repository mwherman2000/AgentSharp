# Alchemist and create_digital — Design

> Built from [templates/Feature-Design-Template.md](templates/Feature-Design-Template.md). Sections that do not apply are folded into the ones that do. Everything here is a proposal; nothing is built. The backlog entry is section 5 of [backlog/Persona-Ideas.md](backlog/Persona-Ideas.md).

| | |
|---|---|
| Status | Stages 0 and 1 built (persona files, registry, watcher, the alchemist, `create_digital`, both apps); stage 2 (temporary personas) not built |
| Date | 2026-11-04 |
| Code home | `AgentSharpLib` (persona registry, `create_digital` tool); the alchemist prompt in `SystemPromptBuilder` |
| Storage | Persona files in the user folder and the project folder |
| Inputs | [Persona-Ideas.md](backlog/Persona-Ideas.md) section 5, [HANDOFFDESIGN.md](HANDOFFDESIGN.md), [superpromptguide.md](superpromptguide.md), [DigitomicEvolution-Design.md](DigitomicEvolution-Design.md) item A5 |

---

## 1. Purpose, scope and restraint

The **alchemist** is a persona that designs a new persona with the user. The **`create_digital`** tool turns the approved design into a persona that exists in the running session and can be handed to.

The name `alchemist` is deliberately utilitarian, not a human-style nickname like Andy or Cody: it is a utility persona, and the name says what it does.

It does **not**:

* give a created persona any tool or approval privilege the other personas lack;
* make a created persona a handoff target without the user's say-so;
* change the built-in personas, or let a created one shadow them;
* replace the existing `sub_agent` tool, which makes throwaway workers for one task, not named personas.

## 2. Roles

| Part | Does | Does not |
|---|---|---|
| **Alchemist (persona)** | Interviews the user about what the persona is for, its voice, its limits and when others should hand off to it. Drafts the prompt using `superpromptguide.md`. Writes the one-line role other personas see. Revises with the user. Calls `create_digital`. Offers a handoff to the new persona. | Write files or register anything itself. |
| **`create_digital` (tool)** | Validates, asks for approval, writes the persona file, registers the persona for the session. No judgment. | Design or improve the prompt. |

**Why the split.** The persona proposes and the tool enforces. The approval gate and validation apply even if the alchemist's prompt is influenced by web content or a pasted document. Any persona can call `create_digital`, so a user can ask Connie to make one and the same rules hold.

## 3. Why it does not work today

Personas are compiled in. The prompts are constants in `SystemPromptBuilder`, and `Personas` and `HandoffPersonas` are static. A new persona needs a code change and a rebuild. Stage 1 below turns the persona list into a registry. It builds on the `RegisterSuperPrompt(key, aliases, name, Func<string> prompt)` hook proposed as A5 in the Digitomic Evolution design, so the registry should be one mechanism, not two.

## 4. Stages

| Stage | What it is | What it takes |
|---|---|---|
| **0. Alchemist drafts** | The alchemist interviews the user and writes a persona prompt to a file. The user reviews it and adds it to the code by hand. | One new prompt. No architecture change. |
| **1. File-based personas** | Personas load from files at startup, from the user folder and the project. `create_digital` writes the file and registers the persona. | Registry instead of static lists; the tool; validation; approval. |
| **2. Throwaway personas** | The alchemist creates a specialist for one problem, hands off, and the persona is dropped when the user is done. Nothing is saved to disk. | Everything in stage 1, plus the session file stores the definition so that `/load` and the handoff stack still resolve. |

**Marking temporary personas (stage 2, not built).** A temporary persona is one made for a single conversation and never saved as a file. To keep it distinct from saved ones:

* a new source, `PersonaSource.Temporary`, next to `BuiltIn`, `User` and `Project`;
* `create_digital` takes `temporary: true`; no file is written, the persona exists only in the registry and in the session file (so `/load` and the handoff stack still resolve it);
* it is shown with a "(temporary)" tag in `/status`, the persona list and the approval prompt, and in saved transcripts, so nobody mistakes it for a saved one;
* it is dropped when the conversation is cleared or the app exits, and it can be kept by an explicit "save" that writes it as a user or project file;
* it counts toward the cap on created personas, and it is never a handoff target unless the user promotes it.

**Recommendation.** Start with stage 0: it costs one prompt, shows whether the interview and drafting are any good, and tells us what stage 1 must enforce. Stage 2 only after stage 1 has been used for a while.

## 5. The create_digital tool

### 5.1 Input

| Field | Notes |
|---|---|
| `nickname` | Safe character set, because it becomes part of file names and of the saved handoff stack. |
| `aliases` | Optional. Same rules as the nickname. |
| `full_name` | Display name. |
| `role` | One line. This is what the other personas see when deciding whether to hand off. |
| `prompt` | The full persona prompt. |

A new persona is never a handoff target at creation. There is no input to set it; promotion is a separate user decision (see section 9).

### 5.2 Steps

1. **Validate.** Nickname and aliases use the safe character set; none shadows a built-in persona or collides with an existing one; the cap on created personas is not reached; required fields are present.
2. **Approve.** Show the full prompt and definition and ask the user, using the same approval prompt as file writes. A refusal ends the call and nothing is written.
3. **Write.** Save the persona file to the chosen location (section 7).
4. **Register.** Add it to the registry so it can be resolved in the same session. It is not in `HandoffPersonas`.
5. **Report.** Return the nickname, file path and how to hand off to it.

### 5.3 Refusals

| Refusal | When |
|---|---|
| Invalid name | Characters outside the safe set. |
| Shadows built-in | Nickname or alias equals a built-in nickname or alias. |
| Collision | Matches a created persona already registered. |
| Cap reached | Too many created personas. |
| Not approved | The user declined. |

## 6. Governance and safety

* **Same privileges.** Personas share the same tools, so a created persona can run shell commands like any other. The approval gate still applies. `create_digital` gives it nothing more.
* **Injected content.** A prompt built from web content or a pasted document could carry instructions. The user sees the full prompt before anything is written.
* **The file tools are covered too.** `write_file` and `edit_file` are `Write`-level, which is normally auto-approved, so on their own they could write a persona file (or set `handoff: true` in one) without the `create_digital` approval. `ApprovalGate` therefore asks about any `write_file` or `edit_file` whose path is inside a `.agentsharp/superprompts` folder (`PersonaRegistry.IsPersonaFilePath`; user and project folders alike, case-insensitive, `..` resolved), every time, with no "always allow", and denies it when no one can answer. Not covered: a path reached through a symbolic link or a Windows 8.3 short name, and a persona file changed by a program the user runs (`run_shell` is Destructive and is asked about anyway).
* **Why there is no "always allow" for persona writes.** Both `create_digital` and a file-tool write into a `superprompts` folder are asked about every time, and an `s` answer counts as allow-once (`ApprovalRequest.OfferAlways` is false, so neither app offers it).
  * *It is remembered per tool name.* `ApprovalGate` stores "always allow" by tool name for the rest of the session, so one `s` would approve every later persona write, including ones nobody has seen.
  * *A persona's prompt is high-leverage.* Unlike a shell command, whose effect the prompt shows, a persona's prompt takes over the conversation once it is used, and with `handoff: true` the model can hand off to it. Pre-approving unseen persona text is the hole the check exists to close, and text the model was fed (a web page, a pasted document) is exactly how a bad prompt would arrive.
  * *Asking is cheap.* Creating or editing a persona is rare and deliberate, so one keypress each time costs little.
  * *What there is instead: a per-file grant that covers the prompt only.* When a file tool asks about a persona file, `s` means "allow edits to this file's prompt for this session (header changes still ask)". The options, and why this one:

    | Option | What `s` would do | Verdict |
    |---|---|---|
    | A. No grant | Nothing: ask every time | Safest; tedious when you iterate on one persona. This was the state before the grant. |
    | B. Plain per-file grant | Allow every later write to this file | Rejected: an edit could then add `handoff: true` or rewrite the whole prompt, unseen. You approved a file, not its content. |
    | **C. Per-file grant, prompt only (built)** | Allow later edits to this file that leave the header unchanged; ask again for any edit that changes it | Chosen: the header decides what the model may do with the persona (hand off to it, how others see it), the body is what you are iterating on. |
    | D. Per-tool grant (the usual "always allow") | Allow every later persona write | Rejected: covers files nobody has seen (see above). |
    | E. Grant on exact content | Allow only a write of precisely the text approved | Not built: it would help a retry but not iteration, since each edit is new text. |

    How option C works. `ApprovalGate` keeps the granted files, by full resolved path (case-insensitive, `..` resolved), for the session. On a later `write_file` or `edit_file` to that file, `PersonaFileEdit.ChangesHeader` reads the file now, works out the text after the call (the `content`, or the one occurrence of `old_string` replaced), and compares the header (the text between the two `---` lines) with the current one, exactly. Same header: no prompt. Different header, or anything it cannot tell, asks again, and the prompt says why (the warning "This edit changes the persona's header..."). It cannot tell when: the file is missing or has no valid header, the new text has none, `old_string` is empty, absent or matches more than once, or the tool is not a file writer. `create_digital` never gets a grant, since each call makes a new file. Limits: a grant covers a file, not a content, so a later edit can still rewrite the whole prompt, which the user chose by granting it; text the model was fed could do that to the one granted persona; and the file is read at approval time, so a change made between the check and the write is not seen. A grant ends with the session.
* **Quality.** The first-pass review in [prompt-review-backlog.md](prompt-review-backlog.md) found real problems in hand-written prompts, so a generated prompt should go through a critic persona first (Persona-Ideas 2.1), a natural use of handoff and return.
* **Too many targets.** Every handoff target is described in every persona's prompt. Many created personas would cost tokens and make routing worse. Hence the cap, and the triage persona (Persona-Ideas 2.1) as a way to choose among them.

## 7. Persistence

* **Built (phase 1a, file loading and watching).** Each persona is one Markdown file. The built-in ones are in `AgentSharpLib/Superprompts/` and embedded in the assembly. `PersonaCatalog` assembles the set, `PersonaRegistry.Current` holds it, and `PersonaWatcher` reloads it when a file is added, changed, renamed or deleted (300 ms debounce). Both apps start the watcher at startup (`PersonaRegistry.WatchAndReport`).
* **File format.** The file name (without `.md`) is the nickname: lower case letters, digits, `-` and `_`, at most 32 characters. A header between two `---` lines, then the prompt:

  ```
  ---
  name: Andy Pando
  aliases: a, andrew
  role: go-to-market (GTM) expert advisor
  handoff: true
  order: 10
  ---
  You are Andy Pando, ...
  ```

  `name` is required. `role` defaults to the name, `handoff` to false, `aliases` to none. `order` sorts the list and the lowest is the default persona; a new persona without it goes last, and an override without it keeps the built-in's position. An invalid file is skipped with a warning and the rest still load.
* **Where.** Built-in (embedded), user (`~/.agentsharp/superprompts`), project (`.agentsharp/superprompts` in the working directory). A user file may add a persona or replace a built-in one. A project file may only add new ones, because a repository you clone could otherwise change what a persona you trust says. Open: whether project-level ones are committed to the repository.
* **Load.** At startup, from all three, in that order. A key or alias must not clash with another persona's.
* **Limits.** The `handoff` tool's schema (its list of persona names) is built once per session, so a new handoff target appears to the model after `/clear` or a restart, though the tool's own validation and the persona list in each prompt use the current set.
* **Files are not locked.** A locked file would stop your editor saving (many editors replace the file by renaming), has no effect on Linux and macOS, and would block any tool that writes persona files. Instead, a persona whose file is deleted or becomes invalid is *retired*: the registry keeps its last good version, it still resolves by name and alias, so a conversation using it carries on, but it is not listed and cannot be handed to. A file with its name coming back makes it live again. Retired personas are kept until the app exits, and a retired name can still be typed in `--Superprompt` or `/clear`. If an invalid edit is to a file that replaced a built-in, the built-in takes over again, with a warning.
* **Handoff stack.** The saved stack stores nicknames, so a stack that names a created persona resolves only if its file is still present; otherwise `/load` reports it as missing.

## 8. Runtime integration

* The alchemist is a built-in persona, listed like the others, with its nickname `alchemist`.
* `/status` and the persona list say which personas are built-in and which were created.
* Handoff to and return from a created persona work the same as for built-in ones, once it has been promoted to a handoff target.

## 9. Promotion to a handoff target

Adding a nickname to `HandoffPersonas` makes the tool schema, the persona list in each prompt and the validation follow. For created personas that list must become dynamic. Promotion is a user action, not something the tool or the alchemist does on its own. Open: whether it is a second tool action, a slash command, or an edit of the persona file.

## 10. Testing

| Claim | Test |
|---|---|
| A valid call writes the file and registers the persona | Happy path; the persona resolves by nickname and alias. |
| Each refusal in 5.3 is enforced | One test per refusal; nothing is written. |
| A created persona is not a handoff target | Not in the handoff tool schema until promoted. |
| A promoted persona is a handoff target | Modeled on `Code_IsAHandoffTarget_...` in `AgentBuilderTests`. |
| Decline writes nothing | Approval refused, no file, no registration. |
| Created personas load at startup | Round trip through the file. |
| A saved stack naming a missing persona is reported on `/load` | No crash. |

Re-run `AgentSharp.HandoffLive` to see whether adding created personas changes how often the others hand off.

## 11. Implementation phases

| Phase | Deliverable | Status |
|---|---|---|
| 0 | Alchemist persona (`Superprompts/alchemist.md`) | Built. Not yet reviewed against the prompt-review checklist |
| 1a | Persona registry, files, watcher, retirement | Built |
| 1b | `create_digital` tool with validation and approval (Destructive, asked every time) | Built |
| 1c | File load at startup, `/personas`, `/status` list and `[user]`/`[project]` tags, `/handoff`, `/return` | Built in both apps. Promotion is an edit of `handoff:` in the file |
| 2 | Throwaway personas | Not built (see section 4) |

## 12. Open questions for the owner

* Does `create_digital` stay create-only, or later take edit, promote and retire actions? (Today: create-only.)
* Are project-level persona files committed to the repository? (Not decided; a project file can add personas but never replace one.)
* The cap on created personas is 20 (`CreateDigitalTool.MaxCreatedPersonas`). Is that right?
* Promotion to a handoff target is a manual edit of `handoff: true` in the file. Should there be a command or tool action for it?
* Should the handoff tool's name list refresh when persona files change, instead of once per session?
* The per-file grant covers the prompt but not the header (section 6, option C). Should a changed `handoff:` line ever be grantable, or always asked about? Today: always.
* Should a grant also be offered for `create_digital`'s file after it is created (so the alchemist can refine a persona it just made without asking each time)? Today: only the file tools' prompt offers it, so the first refinement asks and `s` then grants.
