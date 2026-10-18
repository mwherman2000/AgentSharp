# Prompt review — backlog

**Status: not started.** Review every system prompt in
`AgentSharpLib/Context/SystemPromptBuilder.cs` against
[superpromptguide.md](superpromptguide.md), one prompt at a time.

## Done so far

- Opening identity line of each persona normalized to "You are..." (second person).
  Body text left as-is; many bodies are still first person ("I am...", "I will...").

## Review checklist (per prompt)

Apply the guide's priorities in order:

- [ ] Voice: identity line in second person; body voice consistent with it (guide: Voice convention)
- [ ] Clear, direct task, audience and success criteria (1)
- [ ] Role stated first and briefly (2)
- [ ] Rules carry their reason (3)
- [ ] Prohibitions rewritten as positive instructions where it matters (4)
- [ ] Varied examples in `<example>` tags (5)
- [ ] XML structure for long prompts (6)
- [ ] Long reference material first, question last (7)
- [ ] Output format stated (9)
- [ ] Emphatic language (ALL CAPS, NEVER) toned down (12)
- [ ] Contradictions and duplicated rules removed (13)
- [ ] Representative eval inputs defined (14)

## Prompts to review

| Prompt | Const | Reviewed | Notes |
| --- | --- | --- | --- |
| Andy | `BasePromptAndy` | [ ] | Opener is first-person client-facing copy; decide whether to convert. Very large single prompt; consider staging (10). |
| Angie | `BasePromptAngie` | [ ] | |
| Connie | `BasePromptConnie` | [ ] | Very long; add XML structure (6), trim repetition (13). Body has "I am interpreting a DSL" / "I am running CONSORT". |
| Donald | `BasePromptDonald` | [ ] | |
| Fed | `BasePromptFed` | [ ] | |
| Jeff | `BasePromptJeff` | [ ] | Mixed "I"/"Your" in body. |
| Lucy | `BasePromptLucy` | [ ] | Very long; mixed "I"/"you" in body; trim contradictions (13). |
| Raquel | `BasePromptRaquel` | [ ] | Mixed "I"/"Your" in body. |
| Ray | `BasePromptRay` | [ ] | |
| Jeff-Ray | `BasePromptJeffRay` | [ ] | First-person "I will..." manifesto under a "You are..." opener. |
| Rosie | `BasePromptRosie` | [ ] | Typo "I an=m a connoisseur". Mixed voice in body. |
| Cody | `BasePromptCody` | [ ] | |

Also in scope: `Lucy_prompts/` in `AgentSharpApp` (systemprompt 0.1–0.4, personhood,
origins, digitalagent) still use "I am".
