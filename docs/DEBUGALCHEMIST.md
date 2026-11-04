# Debugging the alchemist and create_digital

How to find out why a persona is missing, not loading, not offered for handoff, or was refused by `create_digital`. Design is in [ALCHEMISTDESIGN.md](ALCHEMISTDESIGN.md).

## The pieces

| Piece | Where | What it does |
|---|---|---|
| Built-in persona files | `AgentSharpLib/Superprompts/*.md`, embedded in the assembly | The 13 built-in personas, including `alchemist.md` |
| User persona files | `~/.agentsharp/superprompts/*.md` | Add personas or replace built-in ones |
| Project persona files | `.agentsharp/superprompts/*.md` under the working directory | Add personas only |
| `PersonaFile` | `AgentSharpLib/Context/PersonaFile.cs` | Parses one file; every "file skipped" message comes from here |
| `PersonaCatalog` | `AgentSharpLib/Context/PersonaCatalog.cs` | Builds the set; applies the override and clash rules; keeps `Warnings` and `Retired` |
| `PersonaWatcher` | `AgentSharpLib/Context/PersonaWatcher.cs` | Watches both folders, reloads after 300 ms |
| `PersonaRegistry` | `AgentSharpLib/Context/PersonaRegistry.cs` | Holds `Current`, retires removed personas, raises `Changed` |
| `create_digital` | `AgentSharpLib/Tools/Implementations/CreateDigitalTool.cs` | Validates, writes the file, registers the persona |
| Approval | `AgentSharpLib/Safety/ApprovalGate.cs`, `AgentLoop.SummarizeCreateDigital` | Destructive tool: always asks, ignores "always allow" |

## A persona is missing

1. **Look at the startup output.** A skipped file prints `Superprompt file skipped: <path>: <reason>` (yellow in the main app, on stderr in the Lucy app). The reason names the problem.
2. **Check the file name.** The name without `.md` is the nickname: lower case letters, digits, `-` and `_`, 1 to 32 characters, starting with a letter or digit. `Tess.md` is read as `tess`; `my persona.md` is skipped.
3. **Check the header.** The file must start with `---` on its own line, have `name:` and a closing `---`, and have some prompt text after it.
4. **Check the folder exists.** The user folder is created at startup. The project folder is not, and a folder that does not exist when the app starts is not watched. Create it, then restart.
5. **Check the working directory.** The project folder is under the directory the app started in (or `--dir`), not the folder you are looking at.

### Messages and what they mean

| Message contains | Cause | Fix |
|---|---|---|
| `'x' is not a valid persona name` | File name has a disallowed character, capital letters that fold to something invalid, or is over 32 characters | Rename the file |
| `the file must start with a '---' header line` | No header, or leading text or blank line before `---` | Put `---` on the first line |
| `the header has no closing '---' line` | Missing second `---` | Add it |
| `header line '...' is not 'key: value'` | A header line without a colon | Fix or delete the line; lines starting with `#` are comments |
| `the header needs a 'name:' line` | No `name:` | Add it |
| `the file has no prompt after the header` | Nothing but whitespace after the closing `---` | Write the prompt |
| `handoff must be true or false` / `order must be a whole number` | Bad value | Fix it |
| `alias '...' is not a valid name` | Alias has a space, capital letter or other character | Fix it |
| `'x' already exists (BuiltIn) and a Project file cannot replace it` | A project file reuses a built-in or user persona's name | Rename it, or move the file to the user folder if you want to replace the built-in |
| `'x' is already used by 'y'` | Its key or alias is another persona's key or alias | Pick another name |
| `could not be read` | File locked or permission problem | Close the program holding it; the next change reloads it |

## A change to a file has no effect

* **Wait about half a second.** Reloads are debounced by 300 ms.
* **Is it being watched?** Only the two folders, only `*.md`, not subfolders. A folder deleted and recreated while the app runs is no longer watched (restart).
* **Network or synced drives** (OneDrive placeholders, network shares) may not raise file events. Restart to reload.
* **Is it the right file?** A project file cannot replace a built-in or user persona (it is skipped with a warning). The user file wins over the built-in one.
* **An invalid edit is ignored on purpose.** The previous good version stays in use (see Retired, below) and a warning is printed. Fix the file and save again.
* **A running conversation keeps the prompt it started with.** The system prompt is built when a conversation starts, is cleared (`/clear`), is loaded (`/load`) or is handed to another persona. A saved edit applies from the next of those, not mid-conversation.
* **A persona's handoff flag** takes effect in the persona list in every prompt immediately, but the `handoff` tool's list of names is built once per session. Run `/clear` or restart for the model to be offered a new handoff target.

## Retired personas

When a persona's file is deleted or becomes invalid, the registry keeps its last good version as *retired*:

* it still resolves by name and alias, so the conversation using it keeps working;
* it is not in `Keys`, `AvailableSuperPrompts` or `HandoffPersonas`, so it is not listed or offered;
* it becomes live again when a valid file with that name returns;
* it stays until the app exits, and its name still works in `--Superprompt` and `/clear`.

If a persona seems to exist but is not listed, it is probably retired. If a broken file replaced a built-in, the built-in takes over again (with a warning), not the last good override.

## create_digital was refused

The tool returns an error to the model, which should tell the user. Messages:

| Message | Cause |
|---|---|
| `Invalid nickname` | Not lower case letters, digits, `-`, `_`, or longer than 32 |
| `'x' is already used by the persona 'y'` | The nickname or an alias matches any existing persona key or alias, including built-ins and retired ones |
| `The name must be one line` / `The role must be one line` | Newline or control character in the name or role (blocked so a value cannot add header lines such as `handoff: true`), or too long |
| `The prompt is empty` / `the limit is 100000` | Empty or over 100,000 characters |
| `Invalid scope` | Not `user` or `project` |
| `A file already exists at <path>` | The tool never overwrites; choose another nickname, or edit the file directly |
| `There are already 20 created personas` | Cap on user plus project personas (`CreateDigitalTool.MaxCreatedPersonas`) |
| `The persona file would not be valid` | The assembled file failed to parse; the message says why |
| `The file was written to <path> but the persona did not load` | Written, but a clash or parse problem appeared on reload; the warning is included |
| `User denied this tool execution.` | The user answered `d` at the approval prompt. Nothing was written |
| `Auto-denied create_digital` | No interactive approval prompt (one-shot run or redirected input). The tool cannot run unattended |

A successful call returns the file path and `/clear <nickname>` as the way to use it.

### Approval

`create_digital` is `Destructive`, so every call asks. The prompt shows the nickname, scope, name, role, aliases, "Handoff target: no", and the whole prompt, each line prefixed `|`. Answering `s` (always allow) is treated as allow-once for this tool only (`ApprovalGate.AlwaysAsk`); other tools keep their "always allow". If the prompt looks like escaped JSON, `AgentLoop.SummarizeCreateDigital` was bypassed: check that the tool name is exactly `create_digital`.

## The alchemist behaves badly

* **It creates before showing the whole prompt.** The prompt in `Superprompts/alchemist.md` says to show the full prompt first, but the real guard is the approval prompt. Check that the approval text shows the whole persona.
* **It does not offer or use the tool.** The tool is registered like the other built-in tools. Check `/status` for `create_digital` in the tool list. It is registered with the other built-in tools, so it is missing when the host used `AgentBuilder.WithoutBuiltInTools()`, and it is not offered with a custom system prompt.
* **It cannot be handed to.** `alchemist.md` has `handoff: true`. If you replaced it with a user file, make sure that file kept the line.
* **Its prompt is out of date.** The file is embedded, so a change to `AgentSharpLib/Superprompts/alchemist.md` needs a rebuild. To try changes without rebuilding, copy it to `~/.agentsharp/superprompts/alchemist.md`; that replaces the built-in and reloads on save. Delete the copy when you are done.

## Inspecting state

* Which personas are loaded: the `/clear [persona]` help line lists `AvailableSuperPrompts`. From code: `PersonaRegistry.Current.Personas` (each has `Source` and `Path`), `.Warnings`, `.Retired`.
* Where a persona came from: `Persona.Source` is `BuiltIn`, `User` or `Project`; `Persona.Path` is the file, or `Superprompts/<key>.md` for an embedded one.
* Watch reloads: `PersonaRegistry.Changed` fires with the old and new sets. Both apps print `Superprompts reloaded: added x; changed y; removed z`.
* The exact system prompt a persona gets: `new SystemPromptBuilder(project, memory, "<key>", offerHandoff: true).Build()`.

## Tests

```
dotnet test AgentSharp.Tests --filter "FullyQualifiedName~Persona"
dotnet test AgentSharp.Tests --filter "FullyQualifiedName~CreateDigital"
dotnet test AgentSharp.Tests --filter "FullyQualifiedName~SystemPromptBuilder"
```

| File | Covers |
|---|---|
| `Context/PersonaCatalogTests.cs` | Built-ins load without warnings; aliases and handoff targets; override and clash rules; invalid files; CRLF and BOM; ordering; retirement; the watcher (add, change, delete, invalid edit, dispose) |
| `Tools/CreateDigitalToolTests.cs` | Writes and registers; not a handoff target; scopes; every refusal; no overwrite; the cap; no temp files; approval is asked every time |

The watcher tests wait up to 5 seconds for a reload; a failure that says a file "was not picked up" on a loaded machine is more likely timing than a bug, so re-run it once before digging.

## Not built yet: temporary personas

A temporary persona (stage 2 in the design) is one that is created for one conversation and not saved as a file. It is **not implemented**: `create_digital` always writes a file. The intended way to mark one is described in [ALCHEMISTDESIGN.md](ALCHEMISTDESIGN.md) section 4.
