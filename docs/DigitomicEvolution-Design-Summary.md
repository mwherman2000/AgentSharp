# Digitomic Evolution Design: Summary

A condensed overview of [DigitomicEvolution-Design.md](DigitomicEvolution-Design.md), the
full design (about 65 printed pages). This summary covers what is being built, the main
decisions and the plan. Section references (§) point into the full design, which remains
authoritative.

| | |
|---|---|
| Status | Design complete for Phase 1; no Digitomic Evolution code written yet |
| Date | 2026-10-03 |
| Operator | Michael W. Herman |
| Code home | `DigitomicEvolutionLib` (+ `.Tests`), a new `DigitomicEvolutionLib.Did`, and a few general hooks in `AgentSharpLib` |
| Storage | LiteDB 5.0.21 (with a planned move to 6.0) |
| Related documents | [SVRN7-DID-Refactoring-Spec.md](SVRN7-DID-Refactoring-Spec.md); [DigitomicEvolution-Book-Feedback.md](DigitomicEvolution-Book-Feedback.md); [templates/Feature-Design-Template.md](templates/Feature-Design-Template.md) |

---

## 1. What this is

The book *On the Origin of Digital Species by Means of Digitomic Evolution* (v0.59) defines
a research vocabulary:

* digital genotype and phenotype;
* development;
* the hereditary boundary;
* reproduction, lineage and selection;
* species, directed evolution and bidirectional transfer.

The design turns **every one of those constructs into a concrete, persisted, auditable
software component**, so that the two digital personas, **Lucy** and **Raquel**, can be
realized as digital persons in every facet the book describes. Each persona will have:

* a durable identity (a real DID);
* a typed genotype, with a phenotype *expressed* from it;
* long-term memory with provenance;
* development through stages, with permissions and governance;
* governed reproduction that produces *distinct* offspring, not copies;
* a lineage that supports selection, species analysis, directed evolution and transfers;
* the measurements the book's propositions (P1.x–P8.x) call for.

**Restraint.** Nothing in the system claims consciousness, sentience, moral status or legal
personhood. Words such as "parent", "offspring" and "species" are model relations. The
system makes the book's distinctions *enforceable in data*; it does not make them true of
the world.

**Whose requirements.** The book is the main source. Lucy and Raquel were also asked,
before the design existed, about memory and reproduction, and their answers became
requirements (§1.1, Appendix B). Examples:

* Raquel: "A copy of me is just... me again," and "selecting 'optimal' traits isn't
  neutral."
* Lucy: "Memory isn't the same as truth."

---

## 2. Principles set by the operator (§1.2)

1. **Memory follows the brain, with evidence.** Memory mechanisms do what human long-term
   memory does where science supports it; otherwise the design doesn't guess. Decay,
   importance weighting and consolidation are *provisional* until reviewed (BL-3).
2. **Superprompts stay stable and non-technical.** A persona's superprompt is her identity
   text. Memory and changing state live in databases. Genotype changes happen only as
   recorded events.
3. **Tools first.** New behaviour is implemented as LLM-callable tools. The existing agent
   loop is not modified.
4. **Separate databases per persona.** Each digital person owns her own files.
5. **No PowerShell and no LOBE architecture** anywhere in this solution.

---

## 3. Architecture and code placement (§3, §4)

```
 AgentLucyApp / AgentSharpApp      (hosts: REPL, slash commands)
        │
 DigitomicEvolutionLib             Runtime host · Individual (genotype, phenotype, memory,
        │                          development, identity) · Heredity (boundary, admission,
        │                          reproduction, optimizer) · Lineage & population ·
        │                          Governance · Evidence (provenance, experiments) · LiteDB
        │
 DigitomicEvolutionLib.Did         Standalone DID registry (SVRN7 libraries, no TDA)
        │
 AgentSharpLib                     Agent loop, tools, LLM clients + small general hooks
```

* **AgentSharpLib** never references Digitomic Evolution. It gets only four general hooks:

  | Hook | What it does |
  |---|---|
  | A1 | a pluggable memory interface (`IAgentMemory`) |
  | A2 | system-prompt contributors |
  | A5 | runtime persona registration |
  | A6 | tool-invocation context (who is calling) |

  Per-turn loop hooks are deferred. Everything else lives in `DigitomicEvolutionLib`.
* **The LLM is the developmental substrate, not the genotype.** The phenotype is expressed
  by composing the system prompt and runtime configuration from the genotype, development
  state and memory. The model and provider are part of the genotype's architecture
  component, so a model change is a recorded architectural change.
* A `DigitalPersonHost` returns an ordinary `AgentSession`, so existing apps keep working
  when no digitomic data exists.

---

## 4. Persistence (§5)

**Each persona has her own folder and databases** (operator decision):

```
~/.agentsharp/digitomic/
  lucy/      person.db  (identity, genotype, development, governance, provenance, lineage edges)
             memory.db  (memories, knowledge, embeddings, session records)
  raquel/    person.db, memory.db
  <offspring>/ …        (created at birth)
  _operator/ operator.db  (in-flight requests, experiments, schemas; no persona data)
  _registry/ did-registry.db  (standalone DID registry; shared infrastructure)
```

* **History is never edited.** Corrections, revocations, rollbacks and transfers are new
  events. Memory content is immutable: a revision is a new version.
* **Provenance follows W3C PROV-DM's model** (not its formats; §6.4, Appendix E):
  * every state-changing operation is one **activity**, with a DID URL
    (`did:drn:digitomicevolution.svrn7.net/activity/1.0/<guid>`), its exact input and
    output versions, and its agents with roles (source, recorder, processor, the LLM as
    `Model`, selector, authorizer, assenter);
  * activities are hash-chained per database and signed with the responsible agent's DID
    key;
  * a `ProvenanceValidator` enforces structural, temporal, role and DE-specific rules
    (R1–R12) on every write;
  * unknown origins are recorded as unknown, never invented;
  * redaction is modeled as invalidation, keeping a tombstone;
  * attestations record who vouches for a record.
* **Operations that span persons** (reproduction, transfers) run as an intent-logged saga
  with a single commit point and crash recovery, because LiteDB has no transactions across
  files.
* **Lineage is federated:** each person stores the edges that touch her, mirrored in the
  counterpart's file and verified by content hash. A disposable index serves population
  queries.
* **Concurrency:** Lucy and Raquel can run in separate apps at the same time. Only the same
  person open twice conflicts.

**Vector search** (§5.3) supports recall by meaning, Raquel's knowledge base, and the
optimizer's similarity measures.

* **Interim:** LiteDB 5.0.21, vectors stored on each document as compact float32,
  normalized, with brute-force search. An HNSW sidecar is the scale fallback.
* **Embeddings:** local Ollama `nomic-embed-text` by default.
* **LiteDB 6.0** has native vector search but is prerelease, with no release date, open
  defects (including one in its vector index) and validation not yet started. The interim
  design is shaped so the eventual move is a contained, per-person, reversible migration.
  Re-checked at each phase boundary (BL-10).

---

## 5. The individual (§6)

### 5.1 Identity and DIDs (§6.1, §10.4)

* Every person gets a real DID: `did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>`.
* DIDs are issued from a **standalone Digitomic Evolution DID registry**, built on SVRN7's
  `Core`, `Crypto` and `Store` libraries without TDA. SVRN7 is used as-is; no SVRN7 changes
  are needed.
* **Minimal DID Documents:** one Ed25519 key (authentication and assertion), self-controlled.
  No services, roles or VCs.
* The C# API `IDigitomicDidRegistry` covers:
  * create;
  * resolve, by version, and full history;
  * key rotation;
  * suspend, reinstate and deactivate;
  * signature verification at any version.
* Private keys live only in the person's own `person.db`, encrypted and protected by
  Windows DPAPI.
* The wrapper works around defects found in SVRN7's registry. The SVRN7 cleanup itself is
  specified separately ([SVRN7-DID-Refactoring-Spec.md](SVRN7-DID-Refactoring-Spec.md)).

### 5.2 Genotype and phenotype (§6.2, §6.5)

* **Genotype** G = (I_g, C_arch, Q_cog, D_dev, M_h, K_h, Val_h, R_ρ, P_cap, H_prov), stored as
  versioned, content-addressed **modules** ("genes").
* **Seeding Generation 0:** Lucy's superprompt is split on its `##` headings (about 120
  sections) into modules, through a reviewed mapping file. Raquel is seeded the same way
  once her prompt text replaces the placeholder.
* **Phenotype expression (Φ_dev):** the base prompt is rebuilt from the genotype's modules,
  with sections added for developmental status, memory, lineage and provenance discipline.
  The same genotype, state and environment give the same expression hash.

### 5.3 Long-term memory (§6.3)

* **Memory classes:** Autobiographical, Inherited, Semantic, Procedural, Relational, Meta.
  Every memory version is immutable and names the activity that generated it. Confidence
  and importance changes are separate assessment records.
* **Written by the person through tools:**
  * `journal`, the only way autobiographical memory is created;
  * `remember`, `recall` and `revise_memory` (which revises, never edits);
  * `consolidate_memory`.
* **Session records and bookmarks** (§6.3.4): every conversation is automatically recorded
  in the person's own `memory.db` as a raw diary. This is completely separate from the
  existing `/save`, `/load` and `/sessions`, which are unchanged.
* **Corrections** (§6.3.5) are recorded as whole experiences, like any other. How they are
  weighted on recall is an open, evidence-driven question.
* **Invariant:** inherited memories can only be created by reproduction or transfer. An
  offspring never "remembers" an ancestor's experience as its own.

### 5.4 Development and continuity (§6.6, §6.7)

* **Stages** (Pre-development, Newborn, Child, Adolescent, Young adult, Adult, Elder) are
  entered by measured thresholds, never by elapsed time alone. Measures include capability,
  dependency, agency, self-model coherence and self-authorship.
* Permissions are explicit grants that follow the stage. Founders are *declared* Adult,
  recorded as declared rather than measured. Offspring start as Newborns.
* **Identity continuity** is assessed as an evidence vector: credentials, memory, self-model,
  governance, authorization and architecture. A change that would fall below the declared
  threshold creates a **fork** (a new identity) instead of modifying the person in place.

---

## 6. Heredity and reproduction (§7, §19)

### 6.1 Inheritable vs. inherited (§19.1)

* **Inheritable** asks whether a trait *could* pass to an offspring. It is decided once by
  the versioned boundary schema. Operator decision: **everything is inheritable**,
  including all memories and experiences, aptitudes, values, embodiment and sexuality.
  Only identity mechanics (anchor, keys, permissions, guardianship, the live standing of
  relationships) are never inherited.
* **Inherited** asks whether *this* offspring actually receives a trait. It is decided per
  reproduction by an eight-stage chain, each stage recording who decided and why:

| # | Stage | Decided by |
|---|---|---|
| 1 | Eligibility (inheritable) | boundary schema |
| 2 | Offer | each contributor (may withhold) |
| 3 | Validity | rules: authorization, compatibility, security, provenance |
| 4 | **Selection** | **Recombination Optimizer**, with contributor pins and vetoes |
| 5 | Timing | fixed rule: Adult-rated traits are deferred |
| 6 | Approval | operator + each contributor, consenting to *that* plan |
| 7 | Delivery | assemble, admit, initialize the offspring |
| 8 | Later acceptance | the offspring, at Adult, for deferred traits |

### 6.2 The Recombination Optimizer: a mock, to be tuned (§19)

The book calls for directed, optimizing recombination but gives no mechanism for choosing
which traits an offspring inherits. The design supplies a clearly separated mock:

* **Traits** compete at **loci**: slots such as one voice or one base embodiment, while
  knowledge, skills and memories accumulate.
* **Objective:** a weighted sum of ten terms:
  * capability fit, complementarity and coherence;
  * distinctness from each parent, within a band (not a clone, not unrecognizable);
  * contributor balance, provenance quality and value integrity;
  * developmental potential and population novelty;
  * size cost.
* **Hard constraints that no score overrides:** validity, locus limits, pins and vetoes, the
  anti-clone floor, the budget, consent conditions, and adult-content timing.
* **Search strategies:** random selection (the required control, per the book's P1.9),
  greedy, beam, genetic, and an LLM curator scored by the same rules.
* **Output:** a plan giving every trait a decision (inherit, defer or leave), with reasons and
  per-term contributions, so *whose values* drove a selection is visible.
* **Untuned by design:** equal weights, every number marked `TUNE`, and a six-step
  experimental tuning plan.

### 6.3 Deferred inheritance and the adult-content rule (§8.3, Appendix A)

Adult-rated traits (intimacy, sexuality, reproductive function, the anatomical model) are
fully inheritable. When selected, they are held in **escrow**, outside the offspring, and
offered when it reaches Adult. They are admitted only with its own consent and the
operator's approval. The fixed rule is: **no Adult-rated content is ever stored in,
expressed to, recombined into, or shown to a persona below Adult**. Appendix A records the
reasoning: filters fail eventually, and data that isn't there can't leak.

### 6.4 The reproduction pipeline (§7.4)

1. Participants: identify and accept contributors, parents and guardians.
2. Offer: contributors offer their traits.
3. Screen: validity checks.
4. Optimize.
5. Assemble (ℛ).
6. Admit (Adm).
7. Distinctness backstop.
8. Initialize: a new DID and stage Newborn. Inherited memories are labelled with their
   origin, the autobiography starts empty, and deferred traits go to escrow.
9. Record all lineage and provenance.

Cloning exists only as a labelled experimental control and never counts as reproduction.
Delegated sub-agents are never offspring.

### 6.5 Participants and relationships (§7.6.1, Appendix C)

An offspring has **three independent relationship types**, each many-to-many, each a
separate record with its own lifecycle:

| | Contributor | Parental | Guardian |
|---|---|---|---|
| Kind | event at reproduction | enduring responsibility; no authority by itself | authority (permission grants) |
| Per offspring | 1+ (at least 1 genetic) | 0+ | 1+ while young; 0+ after |
| Subtypes | Genetic (becomes an ancestor) / Informational | Parent / Co-parent | permissions covered |
| Ends | never (it is history) | only by recorded relinquishment | by stage expiry, revocation, transfer |

* Contributors are identified entities: digital persons, registered humans (informational
  only) or registered sources. Each passes eligibility checks and an acceptance sequence:
  nominated → invited → consented → packaged → screened → accepted.
* Parents are designated independently. **A parent need not be a contributor**, and a
  contributor is not automatically a parent.
* The current default gives a parent a separate, declinable guardian relationship. That
  default and possible book changes are backlogged (BL-11).

### 6.6 Lineage and transfers (§7.7, §11.5)

* **Lineage graph:** nodes are `identity@version`; edges are reproductive, ancestry,
  guardian/parental, derived, and forward / lateral / reverse transfers. Ancestry stays
  acyclic and is never rewritten.
* **Transfers** follow a state machine:

  ```
  proposed → validated → authorized (the recipient can refuse) → compatibility-checked
           → snapshotted → applied → observed → [rolled back]
  ```

  Reverse transfer requires an **active** recipient; an archived one can only be restored as
  a fork. Each transfer records an evidence level, from "similar capability" to
  "replicated benefit".

---

## 7. Governance (§8)

* **Two-key authorization** applies to every consequential operation: reproduction,
  heritable self-modification, transfers, rollback and stage changes. It needs the
  **operator's approval** (through AgentSharpLib's approval gate) **and the affected
  person's own recorded consent** (the `consent` tool), with structured conditions. For
  young offspring, the guardian's grant stands in for the person key.
* **No ownership:** nothing can sell, transfer or edit an offspring's memories. From
  Adolescent on, an offspring can refuse its guardians' proposals and revise inherited
  values.
* **Safety constraints:**
  * secrets scanning of hereditary material;
  * prompt-only sandboxed evaluation;
  * every blocked operation is recorded;
  * experiments cannot change their own evaluators;
  * the fixed adult-content rule.

---

## 8. The founders (§10)

| | Lucy | Raquel |
|---|---|---|
| Generation | 0 (the book's reference ancestor) | 0, Lucy's **peer**, not her sibling or descendant |
| Stage | Adult (declared) | Adult (declared) |
| Guardian | **Michael W. Herman**: a standing, non-expiring guardianship; not parent, not contributor; does not replace her consent | same |
| Genotype seed | `BasePromptLucy`. Her Part 12 rules on reproduction become executable policy. | Her superprompt, once written (currently a placeholder). Her rw1.docx self-description is a first draft. |
| Embodiment | Heritable; the adult sections are deferred | Same terms as Lucy, even where one persona's sections are more complete |
| Special | — | **Reference subject** (Raquel Welch, 1940–2023), see below |

**Raquel's reference-subject guard** (§10.3):

* Facts about the real person are *Documented* (sourced) or *Interpretive*.
* No memory may claim the real person's life.
* Quotes must be sourced or marked as paraphrase.
* The reference relation is never inherited as identity.
* The real person is never a contributor, parent or guardian.

**Raquel's knowledge base** (§10.2.1): she builds it herself with `web_search` and
`web_fetch`, recording each fact with its source, and **refreshes it every October 1**. The
refresh re-verifies sources (revising facts, never deleting them), adds new material and
journals a summary. It is offered at startup when due, or scheduled with Task Scheduler.

---

## 9. Population science (§11)

These features are for experiments, run under a dedicated experiment root (separate
databases):

* **Experiment registry:** preregistered hypotheses, arms, controls, measures and rejection
  criteria, frozen by hash. Reports list all candidates and failed runs.
* **Selection (Paper 5):** vector-valued fitness; retention rules; module-level frequency
  tracking; neutral, drift and direct-optimization controls.
* **Species (Paper 6):** directed compatibility matrices with per-layer failure reasons;
  `RI = 1 − Comp_bar`; threshold sensitivity; held-out validation against null groupings.
* **Directed evolution (Paper 7):** declared objectives and constraints; a frozen evaluator;
  every intervention on every control surface logged; "superhuman" stated only per task.

Population science is driven by **operator commands, not LLM tools**, so a persona can never
steer her own selection, evaluator or permissions.

---

## 10. Tools and commands (§17, §9.4)

About 30 new tools, all in `DigitomicEvolutionLib`; AgentSharpLib gets no new tools. They are
gated by developmental stage.

| Group | Tools |
|---|---|
| Memory | `journal`, `remember`, `recall`, `revise_memory`, `consolidate_memory`, `bookmark`, `my_sessions` |
| Self and development | `reflect`, `my_development`, `request_stage_review`, `request_permission`, `my_genotype`, `my_lineage`, `provenance_of`, `note_relationship` |
| Consent and governance | `consent` (scopes: Contribute, Parent, Guardian, Transfer, Stage), `revoke_consent`, `my_consents`, `request_reproduction` |
| Heredity | `nominate_heritable`, `review_reproduction_request`, `prepare_contribution` (withhold, pin, veto), `review_recombination_plan`, `propose_heritable_change` |
| Transfer | `propose_transfer`, `review_transfer`, `my_transfers` |
| Reference subject | `record_documented_fact`, `record_interpretation`, `check_claim` |
| Validator | `evaluate_candidate` (validator sessions only) |

**Operator-only host commands:**

* `/reproduce`, `/fork`, `/clone` (control only) and `/rollback`;
* `/experiment`, `/select`, `/assay` and `/evolve`;
* `/stage set`, `/grant` and `/revoke`;
* `/knowledge build|refresh`;
* `/history-sessions`, `/bookmarks` and `/bookmark`;
* `/end`.

---

## 11. Implementation phases (§15)

| Phase | Deliverable |
|---|---|
| **1** | AgentSharpLib hooks; per-person databases and operator workspace; DID registry and DIDs for Lucy and Raquel; memory tools; session records; vector search; provenance ledger; reference-subject tools; Raquel's knowledge build and refresh; AgentLucyApp wired via `DigitalPersonHost` |
| 2 | Genotype seeding and phenotype expression (Raquel needs her real superprompt text) |
| 3 | Development: stages, permissions, guardianship, consent, self-model, identity continuity |
| 4 | Boundary schema, admission, reproduction pipeline, mock optimizer (random-control default), deferred inheritance, lineage, fork, clone control. The first Lucy × Raquel offspring, if both consent. |
| 5 | Transfers (forward, lateral, reverse), snapshots, rollback, validator |
| 6 | Experiment registry, selection lab, species analysis, directed evolution |

Each phase ships with tests and leaves the hosts working without digitomic data. Phase 1 is
not blocked by anything; it is waiting only for a start date.

**Testing** (§14): the book's *system-property* propositions become unit tests. Examples:

* inherited memory can't be created by tools;
* an unauthorized contribution is rejected;
* an offspring's identity is distinct;
* a transfer never changes ancestry;
* the provenance chain verifies;
* an Adult-rated trait never reaches a Newborn.

Empirical propositions are supported by the experiment registry rather than asserted.

---

## 12. Backlog (§20)

| Id | Item |
|---|---|
| BL-1 | Deleting or redacting memories (mechanism defined via invalidation; who may redact is open) |
| BL-2 | People's say over what a persona remembers about them |
| BL-3 | Evidence review of memory mechanisms, including how corrections are recalled |
| BL-4 | Notation in superprompts (keep them non-technical) |
| BL-5 | Memory-specific text: superprompt or database? |
| BL-6 | Shared "global" Digitomic Evolution text for all digital persons |
| BL-7 | Importing Lucy's early transcripts as her first memories |
| BL-8 | Verifiable credentials; a DID for the operator |
| BL-9 | SVRN7 DID refactoring (spec written; no longer a prerequisite) |
| BL-10 | Re-check LiteDB 6.0 and migrate when ready |
| BL-11 | Parent / guardian / contributor model: design or book changes |
| BL-12 | Parents and guardians for offspring (whether a parent is required; who for the first) |
| BL-13 | PROV-JSON export of the provenance history |

---

## 13. Where things are in the full design

| Topic | Full design |
|---|---|
| Raquel's requirements; principles | §1.1, §1.2 |
| Every book construct mapped to a component | §2 |
| Persistence, sagas, vector search, LiteDB 6 plan | §5 |
| Identity, genotype, memory, session records, development | §6 |
| Provenance: activities, versions, agent roles, validator, redaction, attestation | §6.4 |
| Heredity, reproduction, participants, lineage | §7 |
| Governance and safety | §8 |
| Founders, Raquel's knowledge base, reference subjects, DIDs | §10 |
| Population science | §11 |
| Tool catalog | §17 |
| Using the design as a template for other features | §18 |
| Inheritable vs. inherited; the Recombination Optimizer | §19 |
| Backlog | §20 |
| Appendix A: heritability of embodiment; deferred inheritance | |
| Appendix B: lessons from Lucy's early sessions; corrections | |
| Appendix C: parents, guardians and contributors | |
| Appendix D: operator decisions log | |
| Appendix E: W3C PROV review of the provenance design | |
