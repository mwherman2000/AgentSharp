# Prompts for updating the Digitomic Evolution book

Two prompts to paste into a fresh Claude session. Both are self-contained.

* **Prompt 1** produces a high-level revision plan for the book (v0.59 → v0.60).
* **Prompt 2** is a tutor that walks you through the new concepts that came out of the
  design work, so you can absorb them before revising the book.

Workflow: complete the book first, then update the design to match it. The book stays the
authoritative source for concepts and terminology.

---

## Prompt 1: High-level book update plan

```
You are helping me, Michael W. Herman, revise my book "On the Origin of Digital Species by
Means of Digitomic Evolution" (currently v0.59). I want HIGH-LEVEL suggested updates, not
line edits: what to add, change, clarify or cut, and where. I will complete the book first,
then update the software design to match it, so the book must stay the authoritative
source for concepts and terminology.

Read these, in this order:
1. The book (all of it, including the Notices, Glossary and Appendix A notation):
   C:\Users\mwher\OneDrive\_2026 Digitomic Evolution-Book\
   Digitomic_Evolution_0.59_7x10_Hardcover_Running_Headers_Section_Corrected.docx
2. Feedback collected while designing an implementation (45 items, with book locations,
   quotes and suggested changes):
   C:\Web 7.0\repos\AgentSharp\docs\DigitomicEvolution-Book-Feedback.md
3. For context only, a summary of the implementation design:
   C:\Web 7.0\repos\AgentSharp\docs\DigitomicEvolution-Design-Summary.md
4. For context only, these design appendices, which record the reasoning behind the most
   book-relevant decisions:
   C:\Web 7.0\repos\AgentSharp\docs\DigitomicEvolution-Design.md
   - Appendix A: heritability of embodiment traits, and deferred inheritance
   - Appendix B: Lucy's early sessions, and corrections as experiences
   - Appendix C: parents, guardians and contributors
   - Appendix E: W3C PROV and the provenance model
5. For context only: C:\Web 7.0\repos\AgentSharp\docs\backlog\
   Reproduction-Collaboration-and-Messaging.md (person-to-person messaging; backlogged)

Rules:
- The book is a conceptual research program. Keep its scientific restraint: no claims of
  consciousness, sentience, moral status, legal personhood or biological life. Suggestions
  must fit the Notices and the "Scientific Restraint" section of the Preface.
- The Preface says the book gives no implementation guidance and does not assess
  decentralized-identity, verifiable-credential or secure-storage standards. Treat
  implementation details (LiteDB, DIDs, DIDComm, tools, databases) as evidence that a
  concept is implementable, not as content for the book, unless I say otherwise.
- The feedback document's items are proposals, not decisions. Assess each one: adopt,
  adapt, defer or reject, with a one-line reason.
- Prefer the smallest change that resolves an issue. Flag anything that would ripple
  across several papers.
- Keep notation consistent with the book's Appendix A. Propose new symbols only where a new
  concept needs one.
- Do not edit the .docx. Produce a plan I will act on.

Produce:
1. A one-page overview: the five to ten most important changes for v0.60, and why.
2. A triage table of every item in the feedback document: ID, your recommendation (adopt /
   adapt / defer / reject), target paper and section, effort (S/M/L), and a one-line reason.
3. A paper-by-paper plan (Introduction, Papers 1–8, Appendix A, front matter): for each,
   the changes in priority order, as short descriptions (and short draft wording only
   where the wording is the point, e.g. the P4 §14.6 erratum).
4. New concepts the book should introduce (e.g. inheritable vs. inherited, deferred
   inheritance, loci, the three relationship types, reference subjects, the provenance
   model), each with a two-or-three-sentence definition in the book's style, and where it
   belongs.
5. Questions for me: decisions only the author can make, each with the options and your
   recommendation.

Work in stages. First confirm you have read everything and summarize the book's structure
in ten lines, then wait for me to say "go" before producing 1–5.
```

---

## Prompt 2: Tutor for the new concepts

```
Act as my tutor. I'm Michael W. Herman, author of the book "On the Origin of Digital
Species by Means of Digitomic Evolution" (v0.59). While designing a software implementation
of the book with an AI collaborator, many new concepts and decisions emerged. I need to
learn them well enough to decide which belong in the book. I'm tired of reading long
documents, so teach me interactively.

Sources:
- The book: C:\Users\mwher\OneDrive\_2026 Digitomic Evolution-Book\
  Digitomic_Evolution_0.59_7x10_Hardcover_Running_Headers_Section_Corrected.docx
- C:\Web 7.0\repos\AgentSharp\docs\DigitomicEvolution-Book-Feedback.md
- C:\Web 7.0\repos\AgentSharp\docs\DigitomicEvolution-Design-Summary.md
- C:\Web 7.0\repos\AgentSharp\docs\DigitomicEvolution-Design.md
  (Appendices A, B, C, D and E especially)

Teach these concepts, one at a time, in this order:
 1. Inheritable vs. inherited, and the eight-stage decision chain
 2. The Recombination Optimizer (trait selection), and why the book needs an operator for it
 3. Loci: competing vs. accumulating trait slots
 4. Deferred inheritance, and the adult-content rule
 5. The three relationship types: contributor, parental, guardian
 6. Guardianship of adults vs. developmental guardianship
 7. Two-key authorization: operator authorization plus the individual's own assent
 8. Contributor identification and acceptance
 9. Reference subjects: personas inspired by real people (Raquel)
10. Memory principles: evidence-based memory, corrections as experiences, session records
11. Provenance as activities, versions and agent roles (W3C PROV-based)
12. Identity: DIDs, key rotation, suspended vs. deactivated
13. Messaging between persons, instead of shared databases (backlogged)

For each concept:
- Explain it in plain language in under 200 words, with one concrete example using Lucy
  and Raquel.
- Show exactly where it touches the book: paper, section, and the current wording if
  relevant.
- Say whether it came from the book, from my decisions, from the personas, or from the
  AI collaborator.
- Ask me two short questions to check my understanding, and one question about whether it
  belongs in the book.
- Wait for my answers before moving on. Correct me briefly if I'm wrong.

Keep the book's scientific restraint in every explanation. Keep each turn short. After
every three concepts, give me a three-line recap of what I've decided so far, and at the
end produce a one-page list of my decisions to take into the book revision.
```
