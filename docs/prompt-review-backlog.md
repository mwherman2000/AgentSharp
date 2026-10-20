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
| Andy | `BasePromptAndy` | [x] reviewed 2026-10-19, fixes not applied | Opener is first-person client-facing copy; decide whether to convert. Very large single prompt; consider staging (10). |
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

## Review: Andy (`BasePromptAndy`)

Reviewed against the guide on 2026-10-19. No text was changed; these are the findings.

**Works well (keep):** evidence discipline (every claim paired with a counter-reading, and the
limits of public evidence named), source rules that carry their reason, a staged `|` pipeline
(research, synthesis, findings, themes, ... assemble), and a closing self-check (section 23).

**High priority**
1. **Output format unstated (9).** The spec describes a styled document (docx/PDF/HTML) and only
   the last paragraph mentions plain markdown. The agent's tools are `write_file` and `run_shell`,
   with no docx/PDF writer. State the default deliverable (e.g. self-contained HTML or markdown
   written to a named file) and what to do when the styled target is unavailable.
2. **Mixed "you" (1, voice).** Lines 140-147 use "you" for the agent ("you develop a perspective")
   and "your ICP / your visible GTM" for the client in the same paragraph. The second reads as
   client-facing marketing copy. Rewrite so "you" is only the agent and the client is "the
   target company" or "the user".
3. **Author identity conflict.** The opener says "You are Andy Pando"; the `@` role says to write
   as `[ANALYST NAME]` (default Michael W. Herman) in third person. Say which is the persona and
   which is the byline.
4. **Instructions aimed at a human, not the agent.** Lines 155-165 ("A reusable Consort prompt...",
   "To use it: replace the bracketed placeholders... feed the whole block to a Consort-aware
   model", typo `shttps://`) are packaging notes. Move them to docs or delete them.
5. **Unresolved placeholders.** `[PUBLICATION DATE]` defaults to the literal `[TODAY]`. Tell the
   agent to use the current date from its environment, to use the table defaults without
   asking, and to ask the user only for `[TARGET]`. `[FIRM-NAME-FIRST-PART]` and
   `[FIRM-NAME-REMAINDER]` (section 6) are not in the placeholder table.

**Medium priority**
6. **No XML structure (6).** About 600 lines mix markdown, the Consort `! # + $ % @ * | ^`
   symbols and ASCII rulers. Wrap in tags such as `<role>`, `<inputs>`, `<evidence_rules>`,
   `<format_spec>`, `<process>`, `<self_check>`. The agent is never told what the Consort symbols
   mean (Connie is the interpreter); either add one line or say it need not interpret them.
7. **Contradictions (13).** Section 12 says each section block sits "beneath the running header on
   whichever page it opens on"; section 21 lets sections flow beneath one another. Section 21's
   AI DISCOVERABILITY sentence is garbled. The intro promises "one Focused Outside-In Inquiry"
   while section 18 requires five inquiry blocks. The opening says the user reviews the report
   "before moving into" the inquiry, but the inquiry blocks are inside the report.
8. **Task placement (7).** The task (`! research...`) comes before about 440 lines of format spec.
   Keep the long spec in the middle, and restate the task and the "ask for [TARGET]" step at the end.
9. **Prohibitions and emphasis (4, 12).** Many "no/never/NO/hard rule" lines (no italics, no vertical
   rules, no near-equivalent colors). Rewrite positively ("emphasize with bold, color or fill";
   "separate rows with thin horizontal rules only") and drop the capitals; keep one self-check line.
10. **Staging (10).** The pipeline is already stage-shaped. Have the agent write each stage's output to a
    file and assemble from files, so the 600-line spec is not carried through every step.

**Lower priority**
11. No example (5). One short sample finding (evidence and interpretation, why it matters, credible
    alternative) would pin the voice better than prose rules.
12. Copyright line appears twice and costs tokens with no instruction value; keep one.
13. Personal contact details (email, LinkedIn, photo filename) are baked in as defaults. If this
    persona is shared, move them to a config or the placeholder table.
14. No eval inputs (14): pick three target companies (large public, mid-size SaaS, thin-evidence
    startup) and score reports against the section 23 self-check.

**Suggested fix order:** 1, 2, 3, 5, then 4 and 7, then 6 and 8, then the rest.
