# Super Prompt Guide

Prioritized prompt-writing guidelines for AgentSharp system prompts and super prompts (Andy, Angie, Connie, Donald, Fed, Jeff, Lucy, Raquel, Ray, RJ, Rosie, Cody).

Source: summarized from Anthropic's prompt engineering guidance. This is a working summary, not a verbatim copy; check the current Anthropic docs for exact wording.

## Voice convention

- The system prompt addresses the agent as **"You are..."** (second person).
- The agent speaks as **"I"** in its own output, including quoted sample dialogue.
- Persona prompts keep the identity statement in second person ("You are Lucy Pando, ..."), even where the body text is written in first person.

## High priority

1. **Be clear and direct.** Treat the model like a smart new hire with no context. State the task, audience, and success criteria explicitly.
2. **Give the role in the system prompt, and keep it brief.** A role focuses tone and domain. Put it first, then the details.
3. **Explain why, not just what.** A rule with its reason generalizes better than a bare command.
4. **Use positive instructions over prohibitions.** "Write in flowing prose" works better than "don't use bullets." Rewrite important "Do not..." lines as what to do.
5. **Use examples (multishot).** Three to five varied examples in `<example>` tags. Vary them so the model doesn't copy a single pattern.
6. **Structure with XML tags.** `<instructions>`, `<persona>`, `<examples>`, `<context>` separate parts unambiguously. Especially useful in the very long prompts (Lucy, Connie).

## Medium priority

7. **Put long reference material first and the question last.** Queries placed after long documents improve quality.
8. **Let the model think.** Ask for step-by-step reasoning, or use extended thinking, for complex tasks.
9. **Constrain the output format.** State the format explicitly; describe the shape you want rather than only what to avoid.
10. **Chain complex tasks into steps.** Split stages instead of one huge prompt (Consort's `|` is in line with this).
11. **Match the prompt's style to the output you want.** A prompt full of bullets and bold tends to produce bullets and bold.

## Lower priority

12. **Tone down emphatic language** (ALL CAPS, "CRITICAL", "NEVER"). Newer models follow instructions closely and can overreact.
13. **Avoid contradictions and duplication.** Long prompts accumulate conflicting rules; the model has to guess which wins.
14. **Test and iterate with evals.** Build representative inputs and compare prompt versions.
15. **Put stable content first for prompt caching.** Keep the role and shared preamble stable across calls.

## Applying this to AgentSharp

- Biggest wins for Lucy and Consort: trim contradictions and repetition (12, 13) and add XML structure (6), rather than adding more text.
- Andy's report prompt is one very large single prompt; consider staging it (10).
- Persona prompts that lean on atmosphere (Lucy, Rosie, Jeff) should state concrete behavior alongside it (1).
