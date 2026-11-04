# Backlog: ideas for new personas

| | |
|---|---|
| Status | **Backlogged. No prompts have been written.** These are ideas to choose from, not a spec. |
| Date | 2026-10-31 |
| Raised by | Michael W. Herman |
| Affects (when taken up) | A new file in `AgentSharpLib/Superprompts/` (with `handoff: true` for any that should be handoff targets), [HANDOFFDESIGN.md](../HANDOFFDESIGN.md) |

---

## 1. Where things stand

There are thirteen personas. Seven are handoff targets: `ray`, `donald`, `andy`, `lucy`, `connie`, `code`, `alchemist`. Six are not: `angie`, `fed`, `jeff`, `raquel`, `jeff-ray`, `rosie`. The alchemist (section 5) is built.

| Persona | Role |
|---|---|
| `andy` | Go-to-market expert advisor |
| `angie` | AI farming assistant |
| `connie` | Understands and runs Consort Markdown super prompts |
| `donald` | The 47th President of the United States, in persona |
| `fed` | Chair of the Federal Reserve System, in persona |
| `jeff` | Intellectual agent inspired by Jeffrey Snover: systems philosophy and technical imagination |
| `lucy` | Persistent digital person with identity, memory and relationships |
| `raquel` | Digital personage inspired by Raquel Welch |
| `ray` | Intellectual agent devoted to the official Principles collection |
| `jeff-ray` | Joint intelligence of Jeff and Ray |
| `rosie` | Autonomous literary horror-story agent |
| `code` | Coding assistant: reads, writes and edits code, runs shell and git |
| `alchemist` | Designs a new persona with you and creates it with the `create_digital` tool |

What is missing is mostly not more characters. It is specialists that work beside these, and roles that make use of handoff and return.

## 2. The ideas

### 2.1 Roles that exploit handoff and return

| Idea | What it does | Why it fits |
|---|---|---|
| **Triage or concierge** | Picks the right persona for a request and hands off. | Its whole job is the `handoff` tool, the most natural user of the mechanism. |
| **Critic or reviewer** | Takes a draft from another persona and attacks it, then returns with a verdict. | The conversation goes there and comes back, which is what the stack is for. |
| **Evidence auditor** (a critic for Andy) | Checks an Andy report for claims that are not actually sourced, using Andy's own tags (directly evidenced, reasonable interpretation, bounded counter-reading). | Matches Andy's evidence discipline and checks long reports like the one in the session that hit the dropped connection. |
| **Devil's advocate or Socratic questioner** | Asks questions instead of answering, to stress-test a plan. | A different posture from every current persona. |

### 2.2 Specialists to sit beside Andy, Ray and Cody

| Idea | What it does |
|---|---|
| **Researcher and fact-checker** | Verifies sources and dates. A gap in long, source-heavy reports. |
| **Writer and editor** | Tightens documents, fixes tone, produces `.docx`. Useful for the book work. |
| **Analyst** | Spreadsheets, tables and charts. |
| **Planner** | Breaks work into steps and keeps backlog notes like the ones in `docs/backlog`. |
| **Cody split into narrower personas** | A code reviewer, a test writer, an architect, a security reviewer. Separate personas with narrow jobs usually beat one persona that does everything. |

### 2.3 Domain experts from the author's own projects

| Idea | What it does |
|---|---|
| **Identity and DID specialist** | The SVRN7, DRN and DID work. |
| **Digital-person curator** | Lineage, reproduction and recombination from the Digitomic Evolution design. |
| **Teacher or tutor** | Ray already has teaching in its brief; a dedicated persona would give a lesson structure. |

### 2.4 Housekeeping agents

| Idea | What it does |
|---|---|
| **Memory curator** | Prunes `MEMORY.md`. A dropped connection once made the model write a bogus entry (fixed since, but the entry stays); this persona would find and remove entries like it. |
| **Session archivist** | Names, tags and summarizes saved sessions. |
| **Judge for live checks** | Grades handoff behavior in `AgentSharp.HandoffLive` instead of relying on string checks. |

### 2.5 Characters

Donald, Fed Chair and Raquel are a different kind of persona: voice and viewpoint, not skill. More public figures or archetypes fit here, with the same care about real people that the existing prompts already take. Not recommended as a priority.

### 2.6 A persona that creates personas

The **alchemist**: a persona whose job is to design a new persona for you, with a `create_digital` tool that makes it available to hand off to. It is a different kind of change from the rest, because it needs a new mechanism and not only a new prompt. See section 5 and [ALCHEMISTDESIGN.md](../ALCHEMISTDESIGN.md).

## 3. If choosing three

1. The **triage persona**.
2. The **critic**, starting with the evidence auditor for Andy.
3. The **researcher and fact-checker**.

They interact with the handoff stack the most, and each takes only a new prompt and one line in each of two lists.

## 4. What adding one takes

1. Write the persona file: `AgentSharpLib/Superprompts/<nickname>.md` for a built-in one, or ask the alchemist to make one (it lands in `~/.agentsharp/superprompts/`). The header has the display name, aliases, a one-line role (what the other personas see when deciding whether to hand off), `handoff: true` or `false`, and an optional `order`; the prompt follows. The existing ones start with a line like "You are Cody Pando, ..." and carry the copyright line.
2. If it should be a handoff target, set `handoff: true`. The tool schema, the persona list in each prompt and the validation follow from that.
3. For a built-in one, add it to the expected lists in `PersonaCatalogTests`.
4. Add a test that it is listed and that the handoff tool accepts it (see `Code_IsAHandoffTarget_...` in `AgentBuilderTests`), and re-run `AgentSharp.HandoffLive` to see whether the new persona changes how often the others hand off.

Review the prompt against the checklist in [prompt-review-backlog.md](../prompt-review-backlog.md) before shipping it.

## 5. A persona that creates personas (the alchemist and `create_digital`)

**The idea.** A persona that interviews the user, writes a new persona prompt, and makes the new persona available to hand off to. A good fit for the super-prompt approach: `docs/superpromptguide.md` could be its template, and `connie`'s strength with prompt languages points to where its own prompt could start.

**Why it did not work.** (Built since: personas are now files, see [ALCHEMISTDESIGN.md](../ALCHEMISTDESIGN.md).) Personas used to be compiled in, as constants in `SystemPromptBuilder`, so a new persona needed a code change and a rebuild. The existing `sub_agent` tool creates throwaway workers for one task; those are not named personas you can hand off to and return from.

### 5.0 Names and roles (decided)

* **The persona is the alchemist.** The name is deliberately utilitarian, not a human-style nickname like Andy or Cody: it is a utility persona, and the name says what it does. It does the design work: interviews the user about what the persona is for, its voice, its limits and when others should hand off to it; drafts the prompt using `docs/superpromptguide.md`; writes the one-line role other personas see; revises with the user; then calls the tool and offers a handoff to the new persona.
* **The tool is `create_digital`.** It does the mechanics and has no judgment: validates the nickname (safe character set, no shadowing, no collision, cap on created personas), shows the full prompt for approval, writes the persona file, and registers the persona so it can be handed to in the same session. A created persona is a handoff target by default (`handoff: false` opts out), so the user and the model can hand to it at once. It gets no tool or approval privileges the others lack.
* **Why the split.** The persona proposes and the tool enforces, so the approval gate and validation apply even if the alchemist's prompt is influenced by web content or a pasted document. Any persona can call `create_digital`.
* **Open.** Whether `create_digital` later takes edit, promote and retire actions or stays create-only.

### 5.1 Three stages, cheapest first

| Stage | What it is | What it takes |
|---|---|---|
| **0. The alchemist drafts** | The alchemist interviews the user and writes a persona prompt to a file. The user reviews it and adds it to the code by hand. | One new prompt. No architecture change. |
| **1. Persistent, file-based personas** | Personas load from files at startup, from the user folder and the project: nickname, aliases, full name, role, whether it is a handoff target, then the prompt body. A `create_digital` tool writes the file and registers the persona, so it can be handed to in the same session. | The persona list becomes a registry instead of static code; a tool; validation; approval. |
| **2. Throwaway personas** | The alchemist creates a specialist for one problem ("a tax-law reviewer for this contract"), hands off, and the persona is dropped when the user is done. Nothing is saved to disk. | Everything in stage 1, plus the session file must store the definition, so that `/load` and the handoff stack still resolve. |

### 5.2 Things to settle before stage 1

* **Approval.** A created persona's prompt is shown in full and approved before the persona exists, using the same approval prompt as file writes. It is a handoff target by default.
* **Safety.** Personas share the same tools, so a created persona can run shell commands like any other. The approval gate still applies, but a prompt built from web content or a pasted document could carry instructions. `create_digital` only writes what the user approves, and a created persona gets no tool or approval privileges the others lack.
* **Quality.** The first-pass review in [prompt-review-backlog.md](../prompt-review-backlog.md) found real problems in the hand-written prompts, so a generated prompt should go through a critic persona first (see 2.1). That is a natural use of handoff and return.
* **Too many targets.** Every handoff target is described in every persona's prompt. Many created personas would cost tokens and make routing worse. Put a cap on them, or add the triage persona (2.1) to choose among them.
* **Names.** A created persona must not shadow a built-in one, and nicknames need a safe character set (the nickname becomes part of file names and of the saved handoff stack).
* **Where they live.** User-level or project-level, and whether the project ones are committed to the repository.
* **Visibility.** Say in `/status` and the persona list which personas are built-in and which were created.

### 5.3 Recommendation

Start with stage 0. It costs one prompt, shows whether the interview and drafting are any good, and tells us what stage 1 must enforce. Stage 1 is the real feature, roughly a day of focused work, mostly turning the persona list into a registry and testing every path that uses it. Stage 2 only after stage 1 has been used for a while.
