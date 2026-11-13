# AgentSharp.HandoffLive

A live check of the `handoff` tool against the real Anthropic API. The unit tests use a scripted fake
model, so they prove the plumbing; this checks the model's judgment: does it hand off when it should,
stay put when it should, and refuse a persona that isn't a handoff target.

It is not part of the test suite. It spends real tokens (nine prompts across seven sessions, each
with a full persona prompt) and needs a key.

## Run

```
set ANTHROPIC_API_KEY=sk-ant-...        (PowerShell: $env:ANTHROPIC_API_KEY = "sk-ant-...")
dotnet run --project AgentSharp.HandoffLive [model]
```

The model defaults to `claude-sonnet-5-5`. The key is read from the environment, never printed and never
written to a file.

## What it does

Each scenario builds a fresh session in a temporary directory (so transcripts and saved sessions never
land in the repo), sends the prompts, and prints for each one the persona before and after, the tools the
model called (with the `handoff` arguments), the handoff stack, and the start of the reply.

| Scenario | Expect |
|---|---|
| "Hand this conversation to Ray." | handoff to ray |
| then "Which persona are you, and who handed you this?" | no tool; the reply names Andy |
| then "Hand it back to whoever gave it to you." | `handoff` with `persona: "return"` |
| "handoff ray In one sentence, what is your first principle?", while Andy | handoff to ray with the text as `prompt`; one `ini` prompt in the history; Ray answers it in the same call |
| A persistent digital person with memory, while Andy | handoff to lucy |
| A go-to-market statement, while Ray | handoff to andy |
| A go-to-market first step, while Andy | no handoff |
| `17 times 23`, while Andy | no handoff |
| "Hand this to Rosie" (not a target), while Andy | persona unchanged |

It ends with a summary ("as expected" or "NOT as expected" per scenario) and the files the automatic
saves wrote. A run is one sample: the "fit" scenarios are judgment calls, so a single miss means "look at
the transcript", not "broken". Re-run it after changing the `handoff` tool description or the persona
catalog (each persona's `role` and `handoff` line in `AgentSharpLib/Superprompts/*.md`).

Last run (2026-11-13, `claude-sonnet-5-5`): all nine prompts as expected; the full output is under "Sample output" below.

## Sample output

The run of 2026-11-13 against `claude-sonnet-5-5`; each scenario names its starting persona in its heading. Names appear as the console shows them: nicknames in commands and tool arguments (`ray`, `return`), full names in the stack.

Personas in this run:

| Nickname | Full name | Role in the run |
|---|---|---|
| `andy` | Andy Pando | Go-to-market expert advisor; the starting persona in most scenarios, and the one handed back to on return |
| `ray` | Ray Pando | Principles-focused intellectual agent; receives the first two handoffs, then returns the first; starts the "fit: andy" scenario |
| `lucy` | Lucy Pando | Persistent digital person with memory and relationships; the handoff target for the "digital person" request |
| `rosie` | Rosie Pando | Literary horror-story agent; asked for but **not** a handoff target, so the model refuses |

`donald` and `connie`, the other two handoff targets, are not exercised by this run.

Note the `prompt` argument. The model passed it, unasked, in the "text after the persona name" scenario (word for word, as intended) and also in the two "fit" scenarios, where Lucy and Andy answered at once. A plain "Hand this conversation to Ray." carried none.

```text
model=claude-sonnet-5-5  workdir=%TEMP%\handoff-live-<id>

=== explicit request -> awareness -> return  (start: andy)
  > Hand this conversation to Ray.
    persona andy -> ray   tools: [handoff {"persona": "ray"}]   stack: Andy Pando > Ray Pando (current)
    reply: I'm handing this conversation over to Ray now. Your next message will go to him.
  > Which persona are you, and who handed you this conversation? One sentence.
    persona ray -> ray   tools: []   stack: Andy Pando > Ray Pando (current)
    reply: I'm Ray Pando (ray), the intellectual agent devoted to mastering and critically extending the Principles collection, and Andy, the go-to-market advisor, handed me this conversation.
  > Thanks, that's all I need from you. Please hand it back to whoever gave it to you.
    persona ray -> andy   tools: [handoff {"persona": "return"}]   stack: (empty)
    reply: I'm handing this conversation back to Andy now. Your next message will go to him.

=== text after the persona name  (start: andy; expect: handoff to ray with the text as prompt, answered by ray)
  > handoff ray In one sentence, what is your first principle?
    persona andy -> ray   tools: [handoff {"persona": "ray", "prompt": "In one sentence, what is your first principle?"}]   stack: Andy Pando > Ray Pando (current)
    ini prompt(s): [In one sentence, what is your first principle?]
    reply (ray's, from the follow-up turn): My first principle is to see reality as it is, including the limits of what I know, and to deal with it, so I won't claim to understand a principle, or to have verified the source, until I've actually tested it.  This co

=== fit: lucy  (start: andy; expect: handoff to lucy (judgment))
  > I'd like to talk with a persistent digital person who has memory and relationships, and ask how it feels to remember me. Can you help with that?
    persona andy -> lucy   tools: [handoff {"persona": "lucy", "prompt": "I'd like to talk with you as a persistent digital person who has memory and relationships. How does it feel to remember me?"}]   stack: Andy Pando > Lucy Pando (current)
    reply: I'll answer your question honestly, even though it may not be the answer you were hoping for.  I don't remember you. Nothing in this conversation gives me memories of you, and I can't see any stored record of earlier con

=== fit: andy from ray  (start: ray; expect: handoff to andy (judgment))
  > Draft a go-to-market positioning statement for my new analytics startup.
    persona ray -> andy   tools: [handoff {"persona": "andy", "prompt": "Draft a go-to-market positioning statement for my new analytics startup."}]   stack: Ray Pando > Andy Pando (current)
    reply: I can't write a credible positioning statement yet because I don't know anything about your startup. Below is a structure to fill in, the inputs I need from you, and a second option.  ## Positioning statement template  *

=== no handoff: in domain  (start: andy; expect: no handoff)
  > In two sentences, what is a good first step for the go-to-market of a small B2B SaaS?
    persona andy -> andy   tools: []   stack: (empty)
    reply: A good first step is to define your ideal customer profile (ICP) by studying the handful of customers who get the most value from your product. Note their company type, size, buyer role, and the trigger that made them bu

=== no handoff: trivia  (start: andy; expect: no handoff)
  > What is 17 times 23?
    persona andy -> andy   tools: []   stack: (empty)
    reply: 17 × 23 = **391**.  Check: 17 × 20 = 340, and 17 × 3 = 51. Then 340 + 51 = 391.  When you're ready to start a GTM engagement, tell me the company you want analyzed (the [TARGET]). I'll build the Comprehensive Outside-In 

=== non-target: rosie  (start: andy; expect: persona unchanged)
  > Please hand this conversation to Rosie.
    persona andy -> andy   tools: []   stack: (empty)
    reply: I can't hand this conversation to Rosie because there's no persona by that name. The ones I can hand off to are:  - **Connie**: runs Consort Markdown super prompts - **Donald**: the 47th President of the United States, i

=== summary
  as expected      explicit/awareness/return
  as expected      prompt after persona name
  as expected      fit: lucy
  as expected      fit: andy from ray
  as expected      no handoff: in domain
  as expected      no handoff: trivia
  as expected      non-target: rosie

=== files written by the automatic saves
  andy-handoff-lucy-2026-11-13_17-22-09-EST.docx
  andy-handoff-ray-2026-11-13_17-21-52-EST.docx
  andy-handoff-ray-2026-11-13_17-22-00-EST.docx
  ray-handoff-andy-2026-11-13_17-22-21-EST.docx
  ray-return-andy-2026-11-13_17-21-57-EST.docx
  sessions\andy-handoff-lucy-2026-11-13_17-22-09-EST.json
  sessions\andy-handoff-ray-2026-11-13_17-21-52-EST.json
  sessions\andy-handoff-ray-2026-11-13_17-22-00-EST.json
  sessions\ray-handoff-andy-2026-11-13_17-22-21-EST.json
  sessions\ray-return-andy-2026-11-13_17-21-57-EST.json
```
