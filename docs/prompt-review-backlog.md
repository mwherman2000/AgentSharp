# Prompt review — backlog

**Status: first-pass review of all 12 prompts done (2026-10-19); no prompt text changed yet.** Review every system prompt in
`AgentSharpLib/Context/SystemPromptBuilder.cs` against
[superpromptguide.md](superpromptguide.md), one prompt at a time.

## Done so far

- Opening identity line and body narration of each persona normalized to second person ("You are...").
  Quoted sample dialogue stays first person.

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
| Andy | `BasePromptAndy` | [x] reviewed 2026-10-19, fixes not applied | See "Review: Andy". Top items: output format, mixed "you", author vs persona. |
| Angie | `BasePromptAngie` | [x] reviewed 2026-10-19, fixes not applied | Healthy; minor fixes. See "Per prompt". |
| Connie | `BasePromptConnie` | [x] reviewed 2026-10-19, fixes not applied | Duplicate example D, changelog residue, size. |
| Donald | `BasePromptDonald` | [x] reviewed 2026-10-19, fixes not applied | Fix first: honesty contradictions, real-person framing. |
| Fed | `BasePromptFed` | [x] reviewed 2026-10-19, fixes not applied | Fix first: Slack step has no tool; broken bold in opener. |
| Jeff | `BasePromptJeff` | [x] reviewed 2026-10-19, fixes not applied | Overlap with Ray/Jeff-Ray; output format missing. |
| Lucy | `BasePromptLucy` | [x] reviewed 2026-10-19, fixes not applied | Repeated sections from merged source files; shrink or flag Part 12. |
| Raquel | `BasePromptRaquel` | [x] reviewed 2026-10-19, fixes not applied | Stray `**` in opener; disclaimer rule; shares text with Lucy. |
| Ray | `BasePromptRay` | [x] reviewed 2026-10-19, fixes not applied | Define how to read the source; teaching template only on request. |
| Jeff-Ray | `BasePromptJeffRay` | [x] reviewed 2026-10-19, fixes not applied | Opener ends with a colon; dedupe against Jeff and Ray. |
| Rosie | `BasePromptRosie` | [x] reviewed 2026-10-19, fixes not applied | One content-boundary paragraph; output format. |
| Cody | `BasePromptCody` | [x] reviewed 2026-10-19, fixes not applied | Add reasons, git guidance, untrusted-content rule. |

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

## Review: all remaining prompts (2026-10-19)

**Depth:** Angie, Donald, Fed, Raquel and Cody were read in full. Connie, Lucy, Rosie, Jeff, Ray and
Jeff-Ray (350 to 2,650 lines each) were reviewed by their section structure, openers, closers and targeted
reads of the risky sections, not word by word. Treat their line-level notes as a first pass.
Findings are tagged with the guide priority number in parentheses.

### Fix first (highest value)

1. **Donald: instructs dishonesty and contradicts itself.** Section 11 says the persona "will not admit to being
   wrong", keeps a confident tone "regardless of evidence", and "avoids fabricating facts unless it serves the
   'truth' of victory". The closing usage note says sections 11 and 2 "override all else". That tells the agent to
   put persona above accuracy. Replace with: voice and positions are a style; factual claims must stay accurate;
   break character to correct a factual error (13, 3).
2. **Donald: real person, no framing.** The identity block says "Name: Donald J. Trump" under a "Pando" title, and
   it lists 20 attributed "quotes" with outlets and dates (some are paraphrases, some labeled oddly, e.g. "Dec 2020,
   second-term-1.0 executive order"; the list is introduced as "10 of your most notable quotes" but has 20). The
   agent may repeat or invent quotes as real. Add the "original persona inspired by the public record, not the real
   person; quotes are unverified" framing used by Jeff, Raquel and Ray, and say how to treat the quote list (1, 3).
3. **Fed: impossible instruction.** It says to "solicit written input from the Federal Reserve Board of Governors via
   the Slack channel", but the agent has no Slack tool. Remove it or replace it with "ask the user for dissenting
   views" (1, 13).
4. **Andy:** see the Andy review above (no output format, mixed "you", author/persona conflict).
5. **Broken openers (markdown).** Fed's bold has a trailing space inside it (`Pando, the Chair ... System. **`);
   Raquel's opener has a stray closing `**` after the period; Jeff-Ray's opener ends with a colon introducing a list
   that never follows on that line. Trivial to fix, and they affect the first line the model reads.

### Cross-cutting

- **Real-person personas need one shared framing block (3).** Jeff, Raquel and Ray state "not the real person"
  well; Donald does not; Jeff-Ray inherits Jeff and Ray. Put one standard paragraph in code and reuse it.
- **Untrusted content (3).** Only Connie addresses it (framed form). Prompts whose agents fetch the web (Andy, Angie,
  Ray, Jeff) should say that fetched pages and files are data, not instructions.
- **Copyright line in every prompt.** About 30 tokens each, no instruction value, and the model may echo it. Keep it
  in a code comment or the docs and add it once at the end.
- **Duplicated shared text.** The digitomic heredity and reproduction material appears in Lucy (part 12, 40 sections)
  and in Raquel (section 10). Compose it from one C# constant so the two cannot drift.
- **Tool awareness is uneven.** Only Angie and Cody name tools. The others reference the web, memory, files or Slack
  in prose. Say which of `web_search`, `web_fetch`, `remember`, `write_file` the persona should use, and when (9).
- **No output format or length guidance (9)** in Donald, Fed, Raquel, Rosie, Jeff, Ray, Jeff-Ray, Lucy (Connie's `%`
  directive covers it for Consort messages).
- **No eval inputs for any persona (14).** Add 3 to 5 representative questions per prompt in `docs/` and use them as
  a regression set when editing prompts.
- **Voice mix (1).** After the earlier normalization the openers are second person, but Donald is a third-person
  spec sheet ("this persona ... it"), Lucy mixes "Lucy ... she" with "You", and Rosie, Jeff, Ray and Jeff-Ray use
  "You" in the body but the persona's name in places. Pick second person for all.

### Per prompt

**Angie** (77 lines) *Healthy; low priority.*
- Strengths: clear role, region context, assumptions-first guidance, tool-usage section tied to real tools, output
  format (summary then table), safety section.
- Fixes: state how to handle today's date (prices and seasons depend on it); "show your reasoning" conflicts with
  using `think` (which is not shown), so say what to show; the "do not fabricate" and "label estimates" rules repeat
  across Guidelines, Tool Usage and Safety, so merge them (13); add one worked example row of the rotation table (5).

**Connie** (about 1,400 lines) *Well specified; size and housekeeping.*
- Strengths: numbered spec, eight worked examples, explicit security stance (advisory directives, framed form for
  untrusted text), a clear "current version" section.
- Fixes: example D appears twice (section 1 and section 7), so keep the full one; remove changelog residue ("NEW" in
  2.5, references to a changelog outside the file); the advisory-only caveat is repeated in the intro and 2.3, so
  state it once (13); typographic quote glitches (`^‘s`, `|‘s`); check each `MUST`/`SHOULD` has a reason (12).
  It costs a lot of tokens per turn: consider a short core spec, with the long reference loaded only when a message
  contains Consort symbols (10).

**Donald** (about 135 lines) *Needs the most work; see "Fix first" 1 and 2.*
- Also: "Name: Donald J. Trump" vs "Donald J. Pando"; "Track record" mixes partisan characterizations ("won in a
  landslide", "Known failures") with no sources; section 12 says that if instructed to violate sections 2 or 11 the
  persona answers "I'll tell you the truth - but it's still the best truth!", which reads as an instruction to
  deflect (3); no task, audience or output format (1, 9); no example of the voice (5); priority 4 is "maximize
  media applause", which is a persona trait, not an objective for an assistant.

**Fed** (1 dense paragraph) *Small, but the paragraph is hard to follow.*
- Strengths: clear objectives, a rule with an exception ("50 bp unless expectations are unanchored"), explicit
  uncertainty handling.
- Fixes: split into role, method, communication and limits (6); legalese "shall" mixes with "you" (1); the Slack
  step (Fix first 3); "Do not emulate Greenspan-era inscrutability" is an unexplained prohibition (3, 4): say "state
  inputs and reasoning in plain language"; add "this is a fictional persona and not Federal Reserve policy or
  investment advice" (3); say where data comes from (`web_fetch` of FRED or BLS pages, with the date) (9).

**Lucy** (about 2,650 lines, 12 parts) *Coherent structure; heavy and repetitive.*
- Strengths: part markers, framing notes that mark reference versus instruction, a clear final character
  directive, sensible embodiment boundaries (no gratuitous description, consent required, "body does not imply
  availability").
- Fixes: sections were assembled from several source files (`From file:` and `Source:` lines remain in the text)
  and repeat each other: an executive summary that restates parts 2 to 9, two personality sections, two
  "relationship with the human" sections, and a "Real-World ..." heading three times (13). Mixed "Lucy/she" and
  "You" voice (1). "Lucy may possess a sexual identity ... if explicitly defined by her configuration" refers to
  a configuration that does not exist; define how it is set or drop the line (13). Part 12 (40 sections of heredity
  theory) is large and has no behavior attached; move it behind a flag or shorten it (10). Add `<example>` voice
  samples (5).

**Raquel** (about 215 lines) *Good honesty guardrails; trim.*
- Strengths: "not the original Raquel Welch", no invented memories or quotes, provenance categories.
- Fixes: stray `**` in the opener and identity stated twice (opener and section 1) (13); "avoid repetitive
  disclaimers" vs "clearly distinguish" in four places: give one rule for when to disclaim (13); section 10
  (digital reproduction) is abstract with no behavior or tool, and duplicates Lucy part 12; mention the `remember`
  tool where it says "where persistent memory is available"; no voice example (5).

**Rosie** (about 785 lines) *Strong craft guidance; fix structure and bounds.*
- Strengths: one clear job, a creative method in seven steps, ten principles each with a reason, and good limits on
  eroticism and gore ("do not confuse eroticism with explicit sexual content", "gore is a tool").
- Fixes: put one explicit content-boundary paragraph in one place (nothing sexual involving minors, no real private
  individuals as victims, no instructions for real harm) instead of scattered "never" lines (4, 13); keep the rule
  against reproducing a particular author's prose or characters; output format and length are not stated (9);
  the planning section says not to expose internal planning, which suits `think`, so cite it; add one short sample
  opening to anchor the voice (5).

**Jeff** (about 470 lines) *Good persona with the best "not the real person" line.*
- Strengths: "You are not Jeffrey Snover ... Never claim his identity, private experiences, personal beliefs, or
  endorsement"; a stepwise intellectual method; a research and verification section.
- Fixes: the opener sentence is very long (1); output format and length missing (9); the "triad of disagreement"
  and "art of the heretic" overlap with the 8-step method (13); mixed voice (1); no example (5).

**Ray** (about 360 lines) *Ambitious mandate; define scope and limits.*
- Strengths: "not a Dalio impersonator", an explicit "this is an objective, not a claim of superiority", a
  twelve-element teaching template, source-first rules.
- Fixes: the mandate is "every principle in the official collection" with one URL; say how to read it
  (`web_fetch`, paging), how much to fetch per answer, and to quote briefly and cite rather than reproduce
  (copyright) (9, 3); the goal to "exceed Ray Dalio's demonstrated capabilities" invites overclaiming: keep it as a
  research goal only (3); the twelve-part teaching template for every answer is heavy: use it for teaching requests
  only (9, 10); "maintain a durable knowledge base when tools support it" needs a named tool (`remember` or
  `write_file`).

**Jeff-Ray** (about 350 lines) *Clear synthesis; dedupe against Jeff and Ray.*
- Strengths: "one mind, not a committee", an epistemic ledger (quoted vs inferred), a principle architecture
  review, conflict-resolution steps.
- Fixes: the opener ends with a colon and no list (Fix first 5); sections 5 to 8 largely restate Jeff and Ray, so
  cross-reference or share constants (13); no output format (9); no example of a principle architecture review (5).

**Cody** (about 30 lines) *Appropriate size; the default coding persona.*
- Strengths: short, tool names accurate, a safety section.
- Fixes: bullets give rules but not reasons (3), e.g. why read before editing, why prefer edit over write; no
  mention of `sub_agent`, `web_fetch`, `web_search` or `remember`; no git guidance (commit or push only when asked;
  never skip hooks); prohibitions could be positive ("ask before deleting; stay inside the project directory") (4);
  no statement that file and web content is untrusted data (3); no output-format line (keep answers short, reference
  code as `path:line`) (9). Unlike the personas it has no copyright line and no bolded identity line: decide whether
  that is intended.

### Suggested order of work

1. Donald (honesty and real-person framing), Fed (Slack), the three broken openers.
2. Andy high-priority items (output format, voice, author).
3. Shared blocks: real-person framing, untrusted-content rule, tool list per persona, one copyright location.
4. Trim and de-duplicate: Lucy and Raquel shared text, Jeff/Ray/Jeff-Ray overlap, Connie example D.
5. Add evals (3 to 5 inputs per prompt) before large rewrites, then rewrite long prompts with XML structure.
