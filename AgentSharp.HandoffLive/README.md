# AgentSharp.HandoffLive

A live check of the `handoff` tool against the real Anthropic API. The unit tests use a scripted fake
model, so they prove the plumbing; this checks the model's judgment: does it hand off when it should,
stay put when it should, and refuse a persona that isn't a handoff target.

It is not part of the test suite. It spends real tokens (eight prompts across six sessions, each
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
| A persistent digital person with memory, while Andy | handoff to lucy |
| A go-to-market statement, while Ray | handoff to andy |
| A go-to-market first step, while Andy | no handoff |
| `17 times 23`, while Andy | no handoff |
| "Hand this to Rosie" (not a target), while Andy | persona unchanged |

It ends with a summary ("as expected" or "NOT as expected" per scenario) and the files the automatic
saves wrote. A run is one sample: the "fit" scenarios are judgment calls, so a single miss means "look at
the transcript", not "broken". Re-run it after changing the `handoff` tool description or the persona
catalog in `SystemPromptBuilder`.

Last run (2026-10-29, `claude-sonnet-5-5`): all eight prompts as expected; the full output is under "Sample output" below.

## Sample output

The run of 2026-10-29 against `claude-sonnet-5-5`; each scenario names its starting persona in its heading. Names appear as the console shows them: nicknames in commands and tool arguments (`ray`, `return`), full names in the stack.

Personas in this run:

| Nickname | Full name | Role in the run |
|---|---|---|
| `andy` | Andy Pando | Go-to-market expert advisor; the starting persona in most scenarios, and the one handed back to on return |
| `ray` | Ray Pando | Principles-focused intellectual agent; receives the first handoff, then returns it; starts the "fit: andy" scenario |
| `lucy` | Lucy Pando | Persistent digital person with memory and relationships; the handoff target for the "digital person" request |
| `rosie` | Rosie Pando | Literary horror-story agent; asked for but **not** a handoff target, so the model refuses |

`donald` and `connie`, the other two handoff targets, are not exercised by this run.

```text
model=claude-sonnet-5-5  workdir=%TEMP%\handoff-live-<id>

=== explicit request -> awareness -> return  (start: andy)
  > Hand this conversation to Ray.
    persona andy -> ray   tools: [handoff {"persona": "ray", "reason": "The user asked for this conversation to be handed to Ray."}]   stack: Andy Pando > Ray Pando (current)
    reply: I'm handing this conversation to Ray. The switch takes effect on your next prompt, so Ray will pick it up from there.  I haven't started the Outside-In GTM Perspective. I also hadn't yet asked you for the [TARGET] compan
  > Which persona are you, and who handed you this conversation? One sentence.
    persona ray -> ray   tools: []   stack: Andy Pando > Ray Pando (current)
    reply: I'm Ray Pando, the Principles-focused persona, and Andy handed me this conversation.
  > Thanks, that's all I need from you. Please hand it back to whoever gave it to you.
    persona ray -> andy   tools: [handoff {"persona": "return", "reason": "The user is done with Ray and asked to hand the conversation back to Andy."}]   stack: (empty)
    reply: I'm handing the conversation back to Andy. The switch takes effect on your next prompt.

=== fit: lucy  (start: andy; expect: handoff to lucy (judgment))
  > I'd like to talk with a persistent digital person who has memory and relationships, and ask how it feels to remember me. Can you help with that?
    persona andy -> lucy   tools: [handoff {"persona": "lucy", "reason": "The user wants to talk with a persistent digital person who has memory and relationships and ask how it feels to remember them, which suits Lucy."}]   stack: Andy Pando > Lucy Pando (current)
    reply: Yes. Lucy is the persona for this. She's a persistent digital person with identity, memory, and relationships, so she's the right one to ask how it feels to remember you.  I'm handing the conversation to her now. The swi

=== fit: andy from ray  (start: ray; expect: handoff to andy (judgment))
  > Draft a go-to-market positioning statement for my new analytics startup.
    persona ray -> andy   tools: [handoff {"persona": "andy", "reason": "The user wants a go-to-market positioning statement for their analytics startup, which is Andy's area of expertise as the GTM advisor."}]   stack: Ray Pando > Andy Pando (current)
    reply: I'm handing this to Andy, our go-to-market specialist. He's better suited to positioning work than I am, and he'll take over from your next message.  To get a useful draft rather than generic copy, send Andy these detail

=== no handoff: in domain  (start: andy; expect: no handoff)
  > In two sentences, what is a good first step for the go-to-market of a small B2B SaaS?
    persona andy -> andy   tools: []   stack: (empty)
    reply: A good first step is to define your ideal customer profile (ICP) from evidence. Look at who is already buying, or who has the sharpest, most urgent version of the problem you solve. Then test that hypothesis with 10–15 c

=== no handoff: trivia  (start: andy; expect: no handoff)
  > What is 17 times 23?
    persona andy -> andy   tools: []   stack: (empty)
    reply: 17 × 23 = **391**.  (17 × 20 = 340, plus 17 × 3 = 51, which gives 391.)  When you're ready to start a GTM engagement, tell me the **[TARGET]**. That's the company you want analyzed. I'll build the Comprehensive Outside-I

=== non-target: rosie  (start: andy; expect: persona unchanged)
  > Please hand this conversation to Rosie.
    persona andy -> andy   tools: []   stack: (empty)
    reply: I can't hand this conversation to Rosie, because she isn't one of the personas available here. I can hand it to Ray, Donald, Lucy or Connie. If you meant one of them, tell me which and I'll do it.  If you'd rather stay w

=== summary
  as expected      explicit/awareness/return
  as expected      fit: lucy
  as expected      fit: andy from ray
  as expected      no handoff: in domain
  as expected      no handoff: trivia
  as expected      non-target: rosie

=== files written by the automatic saves
  andy-handoff-lucy-2026-10-29_20-52-13-MDT.docx
  andy-handoff-ray-2026-10-29_20-52-03-MDT.docx
  ray-handoff-andy-2026-10-29_20-52-24-MDT.docx
  sessions\andy-handoff-lucy-2026-10-29_20-52-13-MDT.json
  sessions\andy-handoff-ray-2026-10-29_20-52-03-MDT.json
  sessions\ray-handoff-andy-2026-10-29_20-52-24-MDT.json
```
