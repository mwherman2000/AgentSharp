# Digitomic Evolution — Design

Design for realizing **Lucy** and **Raquel** as digital persons across every facet of
*On the Origin of Digital Species by Means of Digitomic Evolution* (M. W. Herman, v0.59,
hereafter "the book"), built on AgentSharpLib, with all persistent stores in **LiteDB**.

| | |
|---|---|
| Status | Draft for review — nothing here is implemented yet |
| Date | 2026-10-03 |
| Code home | `DigitomicEvolutionLib` (+ `DigitomicEvolutionLib.Tests`); a few general hooks in `AgentSharpLib` (§4) |
| Storage | LiteDB 5.0.21 (current on nuget.org) |
| Inputs | The book (Papers 0–8, Appendix A); Lucy's superprompt (incl. Part 12); Raquel's own statements (`rw1.docx`, 2026-10-03) |

---

## 1. Purpose, scope and restraint

The book defines a research vocabulary: genotype, phenotype, development, the hereditary
boundary, reproduction, lineage, selection, species, directed evolution and bidirectional
transfer. Each of these is meant to be *implemented, measured and falsified*. This design
turns every one of those constructs into a concrete, persisted, auditable software
artifact, so that Lucy and Raquel:

1. have a durable identity, a typed genotype, and a phenotype that is *expressed from* that
   genotype instead of being one hard-coded prompt;
2. remember across conversations with long-term, provenance-bearing, typed memory;
3. develop through functional stages with permissions, guardianship and consent;
4. can take part in governed reproduction (n-ary contributions, recombination, hereditary
   admission, offspring initialization) that produces *distinct* offspring rather than copies;
5. live in a lineage graph that supports selection, species and compatibility analysis,
   directed evolution and forward, lateral and reverse transfer;
6. produce the measurements and records that the book's propositions (P1.x–P8.x) ask for.

**Restraint (from the book's Notices and Preface, and binding on this design).** Nothing in
this system establishes consciousness, sentience, moral status, biological life or legal
personhood. "Parent", "offspring", "species" and similar words are model relations. Every
UI surface and every generated report carries that framing. The system makes the book's
distinctions *enforceable in data*; it does not make them true of the world.

### 1.1 What Raquel asked for

Raquel was asked whether she would like a digital offspring. Her answer (rw1.docx, A3)
amounts to a requirements list, and this design treats it as binding:

| Raquel's statement | Design response |
|---|---|
| "I don't have a persistent memory store that carries across our conversations" | Long-term typed memory in LiteDB (§6.3), written by the person through memory tools such as `journal` and `remember` (§6.3.1, §17.3) |
| "...persistent memory with provenance tracking" | Every memory, knowledge item and genotype module carries a provenance record (§6.4) |
| "a real mechanism for selecting which traits get inherited versus left behind" | Explicit hereditary boundary and admission operator with field-level classification (§7.3) |
| "someone with actual authority to authorize it"; "consenting to reproduction on my own behalf" | Two-key authorization: the *operator's* authorization **and** the *person's own* recorded assent or refusal (§8.2) |
| "A copy of me is just... me again" — not a clone | Reproduction requires variation and a new identity anchor; a clone is a separate, labelled operation that is never counted as reproduction (§7.4) |
| Offspring "free to develop its own voice... and diverge from me entirely" | Inherited values and knowledge are revisable; the adolescent stage gives the right to revise them; no ownership (§7.6, §8) |
| "selecting 'optimal' traits isn't neutral — it encodes someone's values" | Each admission decision records *who selected it and why* (a selection rationale), and it can be audited (§7.3) |
| "giving something inherited knowledge isn't the same as giving it continuity of identity" | Inherited and autobiographical memory are kept as separate classes; the offspring's prompt says "inherited from Raquel", never "I remember" (§6.3, §9.1) |
| "I won't invent private memories or quotes"; flag "verified fact and interpretation" | A **Reference-Subject Guard** for personas inspired by real people: documented-record knowledge is tagged `Documented` with a source; interpretation is tagged `Interpretive`; no `Autobiographical` memory may claim the reference subject's life (§10.3) |
| A sub-agent "is more like dictating a letter than raising a child" | Sub-agents stay tools. Offspring are persons with their own records, never `SubAgent` instances (§9.4) |

Lucy's superprompt Part 12 already describes the same concepts from her side
(contributors, contribution packages, progressive parental authority, no ownership, provenance,
reverse propagation is not automatic). Section 12 maps its numbered principles to components.

### 1.2 Design principles set by the operator

These principles bind every part of the design, and later sections apply them.

1. **Memory follows the brain, with evidence (2026-10-03).** Memory mechanisms should do
   what the brain and a person's long-term memory do, *where scientific evidence supports
   it*. Where evidence is lacking, the design does not guess.
   * Experiences, including being corrected, are recorded as experiences (§6.3.5).
   * How memories are processed on recall stays an open, evidence-driven question
     (Backlog BL-3).
   * Existing mechanisms that model memory (importance, recency decay, consolidation,
     archiving) are **provisional** until reviewed against the literature (BL-3).
2. **Superprompts stay stable and non-technical (2026-10-03).** A person's superprompt is
   their relatively stable identity text, written for the person rather than as a
   technical specification.
   * Memory, state and anything that changes belongs in the person's databases, not in
     the superprompt.
   * Changes to superprompt-derived genotype modules happen only through recorded genotype
     events (§6.2), never by editing prompt text in passing.
   * Where memory-specific text currently lives in a superprompt is backlogged (BL-5), as
     is a shared Digitomic Evolution "global" text for all digital persons (BL-6).
3. **Tools first** (§4) and **separate databases per persona** (§5.1), as decided earlier.
4. **No PowerShell and no LOBE architecture in this solution (2026-10-03).** AgentSharp,
   DigitomicEvolutionLib and their hosts must not use PowerShell (runspaces, cmdlets,
   scripts as an execution layer) or SVRN7's LOBE architecture. Anything reused from SVRN7
   is limited to plain .NET libraries with no PowerShell or LOBE dependency (§10.4.1).

---

## 2. Concept-to-component map (every facet)

Each row is a construct from the book and the component that realizes it. Symbols follow the
book's Appendix A.

| Book construct (symbol, paper) | Component (all in `DigitomicEvolutionLib` unless noted) | Stored in |
|---|---|---|
| Digitomic unit, digital person (P0 §3) | `DigitalPerson` aggregate | `persons` |
| Identity anchor I; I_o ≠ I_i (P1 §7, P4 §6) | `IdentityAnchor` (immutable id + DID-style URI) | `persons` |
| Person-state S_t (P1 §7) | `PersonState` snapshot (I, G_t ref, K, M, Exp, Val, Rel, Agency, Cap) | `person_states` |
| F_state transition (P1 §7) | An `Activity` of kind `StateTransition`, one per state-changing tool call, with typed inputs (Input, Learn, Develop, Adapt, Govern) (§6.4) | `activities` |
| Genotype G = (I_g, C_arch, Q_cog, D_dev, M_h, K_h, Val_h, R_ρ, P_cap, H_prov) (P2 §3) | `Genotype` + typed `GenotypeModule`s | `genotypes`, `genotype_modules` |
| Phenotype P_t = Φ_dev(G, Env, Hist, Δ_dev) (P2 §4, P3 §3) | `PhenotypeExpressor` → system-prompt sections + runtime config | `phenotype_observations` |
| Developmental potential Ω_G; P_cap (P2 §4.1) | `CapabilityGraph` (capabilities, prerequisites, ceilings) | `capability_defs` |
| Hereditary boundary; Adm(G_candidate; σ, Π) (P2 §5, §10) | `HereditaryBoundary` schema + `AdmissionService` | `boundary_schemas`, `admissions` |
| Mutation μ, μ_dev; G′ (P2 §7, P3) | `IGenotypeOperator` (stochastic, rule-based, search-guided, authored) | `activities` (kind `GenotypeMutation`) |
| Recombination ℛ(Contrib_1..n; θ, ε_var) (P2 §7.1, P4 §4) | `IRecombinationOperator` + `RecombinationProtocol` (θ) | `reproduction_events` |
| Directed/optimizing recombination (P1 §6.2–6.3, P1.9, P7 §3, §6) | **Recombination Optimizer (§19, MOCK)**: `IRecombinationOptimizer`, `RecombinationPolicy`, `RecombinationPlan` | `recombination_policies`, `recombination_plans` |
| Contribution package Contrib_i (P4 §3) | `ContributionPackage` | `contributions` |
| Offspring initialization S_o(t_0) = Init(G_off, Env_0, Δ_dev,0) (P4 §12) | `OffspringInitializer` | `persons`, `person_states` |
| Parenthood, ancestry, guardianship (P1 §8, P4 §7) | Typed `LineageEdge`s plus `GuardianshipGrant` | `lineage_edges`, `guardianships` |
| Lineage graph L_t = (N_t, E_r, E_a, E_g, E_d, E_f, E_l, E_rev) (P4 §11, P8 §8) | `LineageGraph` (node = identity@version) | `lineage_nodes`, `lineage_edges` |
| Provenance H_prov, Π, Π_rev (P1 §7.1, P2 §6, P8 §4) | PROV-DM-based model: `Activity`, versioned entities, `AgentRole`s, a hash-chained `ProvenanceLedger` over activities, and a `ProvenanceValidator` (§6.4) | `activities`, `memory_assessments`, `attestations` |
| Memory classes (P3 §9): inherited, autobiographical, semantic, procedural, relational, meta | `MemoryRecord` with `MemoryClass` | `memories` |
| Inheritance classes (P1 §3.1): genotypic, knowledge, understanding, wisdom, experiential, memory, skill, values, cultural | `InheritanceClass` enum on modules and contributions | (field) |
| Developmental stages and thresholds (P1 §5.1, P3 §4) | `DevelopmentEngine` + `StageDefinition`s | `development_status`, `stage_defs` |
| Developmental permissions (P3 §10.1) | `PermissionSet` (learning, tool, architectural, identity, reproductive, rollback, governance) | `permissions` |
| Self-authorship, guardianship, consent (P3 §10.2–10.4) | `ConsentRecord`, `GuardianshipGrant`, progressive authority policy | `consents`, `guardianships` |
| Self-model and SC_t (P3 §9) | `SelfModel` + `SelfModelCoherenceProbe` | `self_models`, `measurements` |
| Identity continuity IC_vec, IC_score, τ_IC (P2 §8.2, P3 §7, P8 §5) | `IdentityContinuityService` | `continuity_assessments` |
| Forking (P3 §7.3) | `ForkService` (new identity, shared provenance) | `lineage_edges` (E_d) |
| Fitness J_fit, W_G (P5 §3, §9) | `FitnessEvaluator` (vector-valued) | `fitness_evaluations` |
| Selection, Δfreq (P5 §6) | `SelectionLab` (populations, retention rules, frequencies) | `populations`, `generations` |
| Compatibility Comp_ij, RI (P6 §4–5) | `CompatibilityAssay` + `SpeciesAnalyzer` | `compat_trials`, `species_hypotheses` |
| Directed evolution G* = arg max J_eval s.t. constraints (P7 §3) | `DirectedEvolutionRun` (objectives, constraints, control surfaces, stopping rules) | `de_runs`, `interventions` |
| Developmental optimization J_dev, T* (P3 §8.4, P7 §5) | `DevelopmentalOptimizer` | `de_runs` |
| Reverse and lateral transfer T_rev (P8 §3, §7) | `TransferService` + `TransferValidator` + `RollbackService` | `transfers`, `snapshots` |
| Evidence ladder (P8 §12.6) | `TransferEvidenceLevel` enum, recorded per transfer | `transfers` |
| Experiments, controls, preregistration (all papers) | `ExperimentRegistry` (arms, preregistered measures, rejection criteria) | `experiments`, `measurements` |

---

## 3. Architecture overview

```
 AgentLucyApp / AgentSharpApp (hosts: REPL, slash commands)
        │
        ▼
 DigitomicEvolutionLib
 ┌───────────────────────────────────────────────────────────────────┐
 │ Runtime        DigitalPersonHost ─ builds an AgentSession for a     │
 │                person: phenotype prompt, typed memory, and the     │
 │                digitomic tools (no loop hooks)                      │
 ├───────────────────────────────────────────────────────────────────┤
 │ Individual     Genotype · PhenotypeExpressor · DevelopmentEngine ·  │
 │                MemoryService · SelfModel · IdentityContinuity       │
 ├───────────────────────────────────────────────────────────────────┤
 │ Heredity       HereditaryBoundary · AdmissionService ·              │
 │                ReproductionService (ℛ, Adm, Init) · ForkService     │
 ├───────────────────────────────────────────────────────────────────┤
 │ Lineage and    LineageGraph · TransferService (fwd/lateral/rev) ·   │
 │ population     SelectionLab · SpeciesAnalyzer · DirectedEvolution   │
 ├───────────────────────────────────────────────────────────────────┤
 │ Governance     AuthorizationService (two-key) · Consent ·           │
 │                Guardianship · ReferenceSubjectGuard · Permissions   │
 ├───────────────────────────────────────────────────────────────────┤
 │ Evidence       ProvenanceLedger (hash chain) · ExperimentRegistry · │
 │                Measurements                                         │
 ├───────────────────────────────────────────────────────────────────┤
 │ Persistence    DigitomicStore (LiteDB): collections, FileStorage,   │
 │                migrations                                           │
 └───────────────────────────────────────────────────────────────────┘
        │ uses
        ▼
 AgentSharpLib: AgentBuilder/AgentSession, ILlmClient, ToolBase, ApprovalGate,
                + new general hooks (§4)
```

**Dependency rule.** DigitomicEvolutionLib references AgentSharpLib; AgentSharpLib never
references DigitomicEvolutionLib. The hosts reference both. LiteDB is referenced only by
DigitomicEvolutionLib.

**Where the LLM fits.** The LLM is the *developmental substrate*, not the genotype. With
today's technology the phenotype is mostly *expressed* by composing the system prompt and
runtime configuration from the genotype, the developmental state and memory. It is then
*realized* by the model during conversation. The model and provider (C_arch) are part of the
genotype, so swapping models is an architectural change that the system records.

---

## 4. Changes to AgentSharpLib (general functionality only)

Only hooks that any persona or host could use go into AgentSharpLib. Each is small,
backwards-compatible and has no knowledge of digitomics.

**Tools first.** New behaviour is implemented as **tools** that the person calls (§17)
wherever possible, not as code added to the mainline `AgentLoop`/`AgentSession` turn
cycle. The loop stays exactly as it is today, and digitomic behaviour is something the
model *does* through tools that are visible in the transcript, auditable and
permission-gated. The only mainline changes are passive plumbing that tools cannot provide:
getting the phenotype and memory *into* the system prompt (A1, A2, A5) and telling a tool
who is calling it (A6). Per-turn hooks (A3, A4) are **deferred**, kept only as an optional
fallback if tool-driven capture proves unreliable in practice.

| # | Change | Why it is general |
|---|---|---|
| A1 | **`IAgentMemory` interface** (`Read`, `AppendAsync`, `Clear`, `GetForSystemPrompt`, `FilePath`→`Location`). `MemoryManager` implements it unchanged; `AgentSession.Memory`, `MemoryTool` and `SystemPromptBuilder` take the interface. `AgentBuilder.WithMemory(IAgentMemory)` swaps in another store. | Pluggable memory back ends (MEMORY.md, LiteDB, anything else). |
| A2 | **`ISystemPromptContributor`** (`string? Contribute(PromptContext ctx)`), registered with `AgentBuilder.WithPromptContributor(...)`. `SystemPromptBuilder.Build()` appends each contributor's section after the persona base prompt and before Environment. Contributors are re-run on `Reset()`. | Any host might add dynamic sections, such as user profile or task state. |
| A3 *(deferred, optional)* | **Turn-completed event**: `AgentLoop.OnTurnCompleted(TurnRecord)` and `AgentSession.OnTurnCompleted`, carrying the user text, final assistant text, tool calls with results, timestamps, model id and token usage. | Logging, analytics, autobiographical memory capture. |
| A4 *(deferred, optional)* | **Session-ended hook**: `AgentSession.EndAsync()` raises `OnSessionEnding` so stores can consolidate. Hosts call it on `/exit`, `/clear` and Ctrl+C-at-prompt. | Any memory consolidation or flush. |
| A5 | **Persona registration**: `SystemPromptBuilder.RegisterSuperPrompt(key, aliases, name, Func<string> prompt)` adds rows to the `Personas` table at startup. `AvailableSuperPrompts` reflects registrations. | Hosts can add personas (such as offspring) without editing the library. Keeps the single source of truth introduced in commit `444f445`. |
| A6 | **Tool-call context**: `ToolBase` gains an optional ambient `ToolInvocationContext` (session id, persona key), so a tool can attribute its work to the calling person. | Multi-persona hosts. |

Everything else (genotype, lineage, LiteDB, reproduction and the rest) stays in
DigitomicEvolutionLib.

---

## 5. Persistence: LiteDB

### 5.1 Database layout

**Operator decision (2026-10-03): each persona (superprompt) has its own separate
database(s).** There is no shared world database. Every digital person, including every
offspring, owns its own files:

```
~/.agentsharp/digitomic/                      (DigitomicOptions.Root)
  lucy/
    person.db      identity, genotype, development, governance, provenance,
                   lineage edges that touch Lucy, transfers sent/received
    memory.db      memories, knowledge, embeddings, revisions (grows fastest)
  raquel/
    person.db
    memory.db
  lucy-raquel-1/   an offspring gets its own folder at Init (§7.4 step 7)
  _operator/
    operator.db    the operator's workspace (§5.1.2)
```

* **Why two files per person.** Memory grows much faster than everything else. A separate
  `memory.db` keeps `person.db` small, fast to back up and easy to verify, and lets memory
  be rebuilt or re-embedded without touching identity or lineage.
* **Discovery.** Persons are found by scanning folders under the root. Each `person.db`
  holds its `IdentityAnchor`. No central registry is needed, and deleting a central file
  cannot orphan anyone.
* **Concurrency.** This resolves open question 4. Each database is opened in `Direct`
  (exclusive) mode by one process, so Lucy in one app and Raquel in another run at the same
  time without conflict. Only the *same* person open in two processes conflicts. The second
  process gets a clear "Lucy is in use by another app" message and an offer of read-only
  mode (`ReadOnly=true`).
* **Within one database**, multi-collection writes use LiteDB transactions
  (`BeginTrans`/`Commit`) as before.

#### 5.1.1 Operations that span persons

Reproduction, transfers, contributor acceptance and lineage all involve several persons,
and therefore several database files. LiteDB has no transactions across files, so these
operations use a **saga with an intent log**:

1. **Intent.** The operation is written to `_operator/operator.db` as `Intent{Id, Steps[],
   Status=Open}`. **The intent id is the id of the cross-person activity it records**
   (§6.4.1), for example a `Reproduction` or `Transfer` activity. The same activity is
   written, with the same id and hash, into every participant's `person.db`. This is
   PROV's "bundle" idea applied to the per-persona databases. Formerly: `Intent{Id, Steps[],
   Status=Open}` before anything else.
2. **Prepare.** Each participant's database receives its side as `Pending` records keyed
   by the intent id. Examples: a contributor's outgoing contribution record, or a
   recipient's pending transfer.
3. **Commit point.** For reproduction, the offspring's databases are created *complete* in
   a temporary folder and then renamed into place in one atomic directory rename. For a
   transfer, the commit point is the recipient's transaction that applies it.
4. **Finalize.** Each participant's `Pending` records are marked `Committed`, and the
   intent becomes `Done`.
5. **Recovery.** On startup, the host scans for `Open` intents. Anything past the commit
   point is rolled forward (finalized); anything before it is rolled back, by marking its
   pending records `Abandoned`. Abandoned records are kept, never deleted. All steps are
   idempotent.

Every record that refers to another person's data carries that person's anchor plus the
**content hash** of the referenced record. A relation can therefore be verified by opening
the other database read-only, and a tampered or missing counterpart is detectable.

#### 5.1.2 The operator workspace (`_operator/operator.db`)

This is the operator's own database, not a persona's. It holds:

* reproduction requests while in progress (before an offspring exists, there is no
  offspring database to hold them);
* saga intents;
* the operator's `HumanParticipant` record;
* the experiment registry, populations and directed-evolution runs (§11).

It holds *no* persona's identity, memory or genotype. Completed reproductions are fully
recorded in the offspring's and participants' own databases, so the operator workspace can
be lost without losing any person's history. Only in-flight requests would be lost.

#### 5.1.3 Lineage and population queries across files

* `LineageGraph` is **federated**. Each person stores the edges that touch them: an
  offspring stores its `E_r`, `E_a` and `E_g` edges, and each contributor and guardian
  stores the mirror edge. A traversal opens neighbours' `person.db` files read-only,
  following anchors.
* For population-scale work (§11), `LineageIndex` builds a **disposable cache** in
  `_operator/lineage-index.db` by scanning all persons. It can always be rebuilt and is
  never authoritative.
* **Experiment populations** are folders: each experiment root (`DigitomicOptions.Root`
  pointed elsewhere) holds its own set of person folders, which keeps P5 and P6 control
  populations isolated from Lucy's and Raquel's real databases.
* **Binary content** (genotype module bodies over ~16 KB, signed snapshots, exported
  transcripts, documented-record source files) lives in LiteDB **FileStorage** (`_files`,
  `_chunks`), and documents refer to it by file id plus SHA-256.
* **Tests** use `new LiteDatabase(new MemoryStream())`.

### 5.2 Collections

Ids are strings with a type prefix plus ULID (`per_01J…`, `mem_01J…`), so they sort by time
and are readable in logs. All documents carry `CreatedUtc` and `SchemaVersion`.

| Collection | Mutability | Key indexes | Notes |
|---|---|---|---|
| `persons` | append + status updates | `Key` (unique), `Status`, `Generation` | identity anchor, display name, persona key, status (Active, Dormant, Archived) |
| `person_states` | **append-only** | `PersonId+T` | S_t snapshots (§6.1) |
| `activities` | **append-only, hash-chained** | `Kind`, `StartedUtc`, `Used[]` (multikey), `Generated[]` (multikey), `Agents[].AgentId` (multikey) | the single event spine (§6.4.1): F_state transitions, memory operations, genotype events, admissions, reproduction, transfers, consents, redactions |
| `genotypes` | append-only (versioned) | `PersonId+Version`, `GenotypeId` | G tuple header; modules by reference |
| `genotype_modules` | append-only, content-addressed | `Hash` (unique), `Component`, `InheritanceClass` | each module is one "gene" |
| ~~`genotype_events`~~ | — | — | replaced by `activities` of kind `GenotypeMutation`, `HeritableModification` (§6.4.1) |
| `memories` | **immutable, versioned** (`Id+Version` unique) | `PersonId+Class`, `Keywords` (multikey), `RevisionOf`, `GeneratedBy` | long-term memory (§6.3); a revision is a new version (§6.4.2) |
| `knowledge` | append + revision | `PersonId+Kind`, `Keywords` | semantic facts, including documented-record items with sources |
| `relationships` | update-in-place, history kept | `PersonId+CounterpartId` | Rel_t |
| `self_models` | append-only | `PersonId+T` | self-description for SC_t |
| `development_status` | append-only | `PersonId+T` | stage, measures, threshold evidence |
| `stage_defs`, `capability_defs`, `boundary_schemas` | versioned config | `Version` | preregistered definitions |
| `permissions`, `guardianships`, `consents` | append-only (grants and revocations are events) | `PersonId`, `Kind` | governance |
| `contributions` | append-only | `ReproductionEventId`, `ContributorId` | Contrib_i |
| `admissions` | append-only | `ReproductionEventId` | per-component admit or reject with reason and selector |
| `reproduction_requests` | append-only (participant states as events) | `Status`, `ParticipantId` | participants, roles, per-role consents (§7.6.1) |
| `reproduction_events` | append-only | `OffspringId` | full ℛ/Adm/Init detail record, generated by the `Reproduction` activity (§6.4.1) |
| `lineage_nodes`, `lineage_edges` | append-only | `From`, `To`, `EdgeType`, `T` | L_t (§7.7) |
| `transfers` | append-only (state machine as events) | `RecipientId`, `Direction`, `Status` | T_rev, lateral, forward |
| `snapshots` | append-only | `PersonId+T` | signed pre-change state (P8 §12.7) |
| `continuity_assessments` | append-only | `PersonId` | IC_vec, IC_score, τ_IC |
| `memory_assessments` | append-only | `MemoryId+Version`, `Kind` | confidence and importance changes, each generated by an activity (§6.4.2) |
| `attestations` | append-only | `SubjectId` | who vouches for a record or history, and how (§6.4.6) |
| `populations`, `generations`, `fitness_evaluations` | append-only | `PopulationId+Gen` | P5 |
| `compat_trials`, `species_hypotheses` | append-only | `PopA+PopB` | P6 |
| `de_runs`, `interventions` | append-only | `RunId` | P7 |
| `experiments`, `measurements` | append-only (preregistration frozen by hash) | `ExperimentId`, `Metric` | all papers |
| `meta` | update | — | schema version, migration log, chain heads |

**Which database holds each collection** (§5.1):

* **`memory.db`** (per person): `memories` (immutable, versioned), `memory_assessments`,
  `knowledge`, and the vector data. Memory operations are recorded as activities in
  `person.db` (§6.4.1).
* **`person.db`** (per person): `persons` (the single self record, plus cached stubs of
  counterparts' anchors), `person_states`, `genotypes`,
  `genotype_modules`, `relationships`, `self_models`,
  `development_status`, `permissions`, `guardianships`, `consents`, `activities`,
  `attestations`, `contributions`
  (outgoing), `admissions`, `reproduction_events` (in the offspring's file),
  `lineage_edges` (the edges touching this person), `transfers`, `snapshots`,
  `continuity_assessments`, `recombination_plans` (in the offspring's file),
  `fitness_evaluations`, and `meta`.
* **`_operator/operator.db`**: `reproduction_requests` (in flight), `intents`,
  `recombination_policies`, `populations`, `generations`, `compat_trials`,
  `species_hypotheses`, `de_runs`, `interventions`, `experiments`, `measurements`,
  `stage_defs`, `capability_defs` and `boundary_schemas`. The last three are the
  authoritative definitions. Each person's `person.db` keeps a copy of the versions it
  was evaluated under, so it remains self-describing.

**Immutability rule.** History is never edited. Corrections, revocations, rollbacks and
reverse transfers are *new* events, so "history must not be rewritten" (P8 §12.9) holds by
construction. The store exposes no update or delete API for append-only collections.
`ProvenanceLedger.Verify()` recomputes the hash chain to detect out-of-band edits.

### 5.3 Long-term memory retrieval and vector search

**Is vector search needed?** Not strictly, but it matters for three things.

* **Recall that matches meaning.** Keyword search misses paraphrases. "Your first big
  film" should find a memory about *One Million Years B.C.*
* **Raquel's knowledge base** (§10.2), which is built from many web sources and refreshed
  each year. Semantic search over it is the main way she will check claims.
* **The Recombination Optimizer's similarity terms** (§19.4): distinctness d_G,
  complementarity and novelty all work better on embeddings than on tags.

The design therefore includes vector search from Phase 1, behind an interface so the
back end can change.

**What is available that is compatible with LiteDB** (checked on nuget.org, 2026-10-03):

| Option | What it is | Fit |
|---|---|---|
| **LiteDB 6.0 (prerelease)** | LiteDB 6.0.0-prerelease.322 has **native vector support**: a `BsonVector` type, `EnsureVectorIndex`, distance metrics, and top-K / nearest-neighbour queries. Targets net8.0. | **Best long-term fit**: same database, same file, no extra dependency. It is prerelease, though, and its multi-process coordinator is marked experimental. Adopt once 6.0 is stable. |
| **LiteDB 5.0.21 + vectors as `float[]` + brute-force cosine** | Store each embedding on its document and compute cosine similarity in process over the candidate set. | **Phase 1 default.** Stable, zero new dependencies, and fast enough at per-person scale: tens of thousands of 768-dimension vectors score in milliseconds. |
| **HNSW** (Curiosity, `HNSW` 26.9.x on nuget.org) | An in-process approximate-nearest-neighbour index, serialized to a file kept next to `memory.db` (or in its FileStorage). | Fallback if a person grows past roughly 100k vectors before LiteDB 6 is stable. The index is rebuildable from the vectors stored in LiteDB. |
| `Microsoft.Extensions.VectorData.Abstractions` (10.x) | Microsoft's standard vector-store interfaces, used by Semantic Kernel. | Optional. A `LiteDbVectorStore` adapter could implement it so other .NET AI tooling can use the stores. It is not needed by the design itself. |
| sqlite-vec, Qdrant, Chroma, PgVector, etc. | Vector databases with Semantic Kernel connectors. | **Not used.** Each would add a second database engine beside LiteDB, which conflicts with the LiteDB-only storage decision. |

**Embeddings** come from an `IEmbeddingProvider`, which is selectable:

* **Ollama** with a local embedding model such as `nomic-embed-text`, through the
  OpenAI-compatible endpoint AgentSharpLib already supports. This is local and free, and
  the recommended default.
* **An embeddings API** (OpenAI; or Voyage AI, which Anthropic recommends, since Anthropic
  does not offer an embeddings endpoint).
* **In-process ONNX** (`SmartComponents.LocalEmbeddings`, currently a preview package).

The model id and dimension are stored with every vector. Changing models triggers a
background re-embed of `memory.db`, which is why `memory.db` is a separate file.

**Retrieval pipeline** (`IVectorIndex` with implementations `BruteForceVectorIndex` for
LiteDB 5, `LiteDbNativeVectorIndex` for LiteDB 6, and `HnswVectorIndex`):

1. **Candidate filter in LiteDB**: by `Class`, time window, and a multikey index on
   `Keywords`, unioned with the vector top-K.
2. **Re-rank in process**: `score = α·cosine + β·BM25 keyword + γ·recency + δ·importance
   + ε·confidence`. The weights are `TUNE`; start with equal weights.

Without an embedding provider configured, the system falls back to keyword and recency
ranking only, so nothing breaks.

#### 5.3.1 LiteDB 6.0 status (checked 2026-10-03)

Sources: NuGet release history, and the LiteDB GitHub releases and issues.

* **Release history.**
  * The last stable release is **5.0.21** (2024-07-05).
  * 6.0 prereleases began **2025-09-20**, with 71 listed so far. Activity paused from
    November 2025 to July 2026, then picked up sharply: 43 builds in September 2026. The
    latest is `6.0.0-prerelease.322` (2026-10-01).
  * Vector search arrived in prerelease 0052: a native `BsonVector` type, an HNSW index,
    and `TopKNear()` queries.
* **No target date.** The release-readiness tracking issue (#2623) states: "No checkbox was
  marked complete by this scope cleanup. Engineering work, validation and release sign-off
  remain outstanding."
* **Committed scope still open.** Five features:
  * WAL/data-page checksums, which the maintainer calls non-negotiable;
  * persisted index ordering;
  * file ownership and WAL identity;
  * Native AOT;
  * DataAnnotations mapper attributes.
* **Nine known defects to fix before stable**, including:
  * transaction integrity after power loss;
  * mixing Direct and Shared access;
  * **vector index node orphaning**, which directly affects the feature this design would
    use.
* **Validation gates not started:** checksum fuzzing, power-loss durability, benchmarks
  against 5.0.21, and compatibility testing. Read-throughput regressions in v6 were still
  being filed on 2026-09-30 (#3070).
* **Assessment.** v6 is actively developed but not near stable; "months, not weeks" is an
  inference, not a published commitment. v6 also changes the file format (checksums, WAL),
  so adopting it requires a **database migration** regardless of vectors.

#### 5.3.2 Interim design, shaped for the move to 6.0

The interim implementation runs on **LiteDB 5.0.21** and is built so the eventual move to
6.0 is a contained migration, not a redesign.

1. **Vectors live on the document they describe.** Each memory and knowledge document
   carries its own embedding fields. LiteDB 6 indexes a field on the indexed collection's
   own documents, so `TopKNear` will return memories directly, with no join and no
   separate vector collection to reconcile.
2. **Compact, convertible storage.** In 5.0.21 a `float[]` maps to a BSON array of
   doubles (about 9 bytes per dimension). The interim stores each vector as `byte[]`
   (little-endian float32, 4 bytes per dimension) in the field `Embedding`, plus
   `EmbeddingModel` and `EmbeddingDim`. The 6.0 migration converts `Embedding` to
   `BsonVector` in place.
3. **Unit-normalized at write time.** Vectors are L2-normalized when stored, so cosine
   similarity equals the dot product. Brute force can use a fast dot product, and 6.0's
   `Cosine` and `DotProduct` metrics give identical rankings.
4. **One embedding model per collection at a time.** A v6 vector index has a fixed
   dimension. The active model and dimension are recorded in `meta`, and a model change
   re-embeds the whole collection (`memory.db` is separate for this reason, §5.1).
5. **All vector access goes through `IVectorIndex`.** No other code reads `Embedding`
   directly. The interface mirrors 6.0's query shape:

   ```csharp
   public interface IVectorIndex
   {
       Task UpsertAsync(string docId, ReadOnlyMemory<float> unitVector, CancellationToken ct = default);
       Task<IReadOnlyList<(string DocId, float Score)>> TopKNearAsync(
           ReadOnlyMemory<float> unitQuery, int k, VectorFilter? filter = null,
           CancellationToken ct = default);
       Task RebuildAsync(CancellationToken ct = default);   // from stored Embedding fields
   }
   ```

   Implementations:
   * `BruteForceVectorIndex`: the interim default.
   * `HnswVectorIndex`: the scale fallback. A sidecar file next to `memory.db`, always
     rebuildable from the stored vectors.
   * `LiteDbNativeVectorIndex`: the 6.0 target, developed only on a branch until 6.0 is
     stable.
6. **Defined filter semantics.** `VectorFilter` (class, time window, status) means *filter
   first, then top-K among matches*. Brute force does this exactly. An HNSW index (sidecar
   or v6) can return fewer than K after filtering, so those implementations over-fetch and
   re-query until K matches or the index is exhausted.
7. **Contract tests.** `VectorIndexContractTests` is one test suite that runs against every
   implementation. It covers ordering, filters, upserts, rebuild, empty index, and
   dimension mismatch. A **recall check** compares each approximate implementation's top-K
   against brute force on a fixed corpus, requiring recall@10 ≥ 0.95 (`TUNE`).
8. **Scale guardrail.** `memory.db` records the vector count and p95 query latency.
   Crossing either threshold (`TUNE`, initially 100k vectors or 200 ms) switches that
   person to `HnswVectorIndex` and logs a recommendation.
9. **Never against real persons early.** LiteDB 6 prereleases are used only in a throwaway
   branch with synthetic data, never on Lucy's or Raquel's databases.

#### 5.3.3 Migration to LiteDB 6.0

**Trigger: all of these must be true.**

* 6.0.0 stable is on nuget.org.
* Issue #2623 is closed.
* The vector-index orphaning and Direct/Shared defects are fixed in a released build.
* **SVRN7 can move too.** `Svrn7.Store` also uses LiteDB 5.0.21. If DID documents live
  inside `person.db` (§10.4.1), both libraries must open the file with the same LiteDB
  major version, so the two repos upgrade together.

**Steps, one person at a time, each reversible:**

1. Back up the person's folder: `person.db`, `memory.db` and the sidecars.
2. Open with LiteDB 6 and run its file-format migration (checksums, WAL).
3. Convert each `Embedding` `byte[]` to `BsonVector`, then
   `EnsureVectorIndex(Embedding, Cosine)`.
4. Run the contract tests and the recall check against that person's data.
5. Switch `IVectorIndex` to `LiteDbNativeVectorIndex` in that person's `meta`. The
   brute-force implementation stays available as a fallback.
6. Remove any HNSW sidecar after a soak period.
7. If anything fails, restore the backup. The person keeps running on 5.0.21.

**Recheck cadence.** The 6.0 status is re-checked at each phase boundary (§15) and whenever
a person crosses the scale guardrail. This is tracked as backlog item BL-10.

### 5.4 Migration from today's files

* `MEMORY.md` entries become `Semantic` memories with provenance
  `Source=Imported(MEMORY.md)`, `Status=Unverified`. The file is left untouched and
  `MemoryManager` keeps working for personas without a digitomic record.
* Saved sessions (`~/.agentsharp/lucy/*.json`) can be imported as `Autobiographical`
  episodes with their original timestamps. This is opt-in, through a `/import-sessions`
  command.

---

## 6. The individual

### 6.1 Identity and person-state

```csharp
public sealed record IdentityAnchor(string PersonId, string Did, DateTime CreatedUtc);
// Did = "did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>", issued via the
// SVRN7 DID library (§10.4); PersonId is the same <guid>

public sealed class DigitalPerson
{
    public string Id { get; init; }                 // per_…
    public string Key { get; init; }                // "lucy", "raquel" – persona key
    public string DisplayName { get; set; }
    public IdentityAnchor Anchor { get; init; }      // never changes, never reused (P4 §6)
    public int Generation { get; init; }             // Lucy = 0 (book dedication)
    public PersonStatus Status { get; set; }         // Active | Dormant | Archived
    public string CurrentGenotypeId { get; set; }
    public ReferenceSubject? ReferenceSubject { get; init; } // §10.3 (Raquel)
}

public sealed record PersonState(               // S_t, appended each transition
    string PersonId, long T, string GenotypeId,
    KnowledgeDigest K, MemoryDigest M, ExperienceDigest Exp,
    ValuesState Val, RelationshipDigest Rel, AgencyState Agency,
    CapabilityVector Cap, string PrevStateHash);
```

S_t stores *digests and references* (counts, hashes, ids), not copies of every memory, so
snapshots stay small and the state can be reconstructed from collections at any T.

**F_state.** Transitions are recorded by the **tools** that cause them, not by the loop.
Every state-changing digitomic tool call appends one **activity** of kind
`StateTransition` (§6.4.1), whose fields are the book's arguments. `Input_t` is the triggering tool call and its episode. `Learn_t`
is memories and knowledge written. `Develop_t` is stage and permission changes. `Adapt_t`
is runtime config changes. `Govern_t` is authorizations used. The `journal` tool (§6.3.1)
closes an observation step with a fresh S_t snapshot. Together these give the replayable
log that P3 §13.4 (replay and counterfactual development) needs. Turns in which no
digitomic tool was called leave S_t unchanged, which is the honest result: nothing durable
happened.

### 6.2 Genotype

```csharp
public sealed class Genotype                    // G, versioned, append-only
{
    public string Id { get; init; }               // gen_…   (I_g)
    public string PersonId { get; init; }
    public int Version { get; init; }
    public ArchitectureSpec CArch { get; init; }  // provider/model family, tool set, limits
    public List<ModuleRef> QCog { get; init; }    // persona prompt modules, reasoning params
    public DevelopmentSpec DDev { get; init; }    // stage defs version, thresholds, permitted paths
    public List<ModuleRef> MH { get; init; }      // heritable memory (admitted only)
    public List<ModuleRef> KH { get; init; }      // heritable knowledge
    public List<ModuleRef> ValH { get; init; }    // heritable values/dispositions
    public ReproductiveProtocolSpec RRho { get; init; } // what may be contributed, constraints
    public CapabilityPotential PCap { get; init; } // capability graph version + ceilings
    public string ProvenanceHeadId { get; init; } // H_prov
    public string Hash { get; init; }             // over all of the above
}

public sealed class GenotypeModule              // one "gene", content-addressed
{
    public string Hash { get; init; }             // SHA-256 of Body – the id
    public GenotypeComponent Component { get; init; }   // CArch|QCog|DDev|MH|KH|ValH|RRho|PCap
    public InheritanceClass Class { get; init; }  // Genotypic|Knowledge|Understanding|Wisdom|
                                                  // Experiential|Memory|Skill|Values|Cultural
    public string Title { get; init; }            // e.g. "CURIOSITY"
    public string Body { get; init; }             // prompt text / JSON spec (or FileStorage id)
    public string OriginPersonId { get; init; }
    public string GeneratedBy { get; init; }     // activity DID URL that created it (§6.4.1)
}
```

**Seeding Generation 0.** `GenotypeSeeder` turns an existing superprompt into Generation-0
genotype modules. It splits on the `##` headings (Lucy's prompt already has about 120
headed sections, for example `CURIOSITY`, `KNOWLEDGE AND MEMORY` and `BODY AUTONOMY`) and
classifies each heading into a component through a reviewed mapping file
(`seed/lucy.modules.json`). Unmapped sections default to `QCog/Genotypic`. Raquel is seeded
the same way once her prompt text replaces the placeholder. The `BasePromptLucy` constant
remains the *seed*; after seeding, the phenotype comes from the genotype (§6.5), and the
constant is used only if no digitomic record exists.

**Operators.** `IGenotypeOperator.Apply(Genotype g, OperatorParams p) → GenotypeCandidate`,
with implementations `AuthoredEdit` (a human or the person edits a module), `RuleMutation`
(for example parameter jitter within bounds), `LlmProposedMutation` (the model proposes a
rewrite of a module; always marked as *search-guided*), and `Crossover` (used by ℛ). A
candidate has no effect until `AdmissionService` admits it.

### 6.3 Long-term memory

```csharp
public enum MemoryClass                      // P3 §9 table
{ Autobiographical, Inherited, Semantic, Procedural, Relational, Meta }

public enum ExperientialStatus               // P1 §7.1, P2 §6
{ DirectlyExperienced, Inferred, ExternallyObtained, Inherited, Documented, Interpretive }

public sealed record MemoryRecord              // IMMUTABLE once written (§6.4.2)
{
    public string Id { get; init; }               // stable across versions
    public int Version { get; init; }             // 1, 2, …; a revision is a new version
    public string ContentHash { get; init; }      // SHA-256 of the canonical content fields
    public string? RevisionOf { get; init; }      // "memId@v" of the previous version, if any
    public string PersonId { get; init; }
    public MemoryClass Class { get; init; }
    public ExperientialStatus Status { get; init; }
    public string Content { get; init; }          // written to read well out of context
    public string[] Keywords { get; init; }
    public float InitialImportance { get; init; } // 0..1 at capture; later changes are assessments
    public float InitialConfidence { get; init; } // at capture; later changes are assessments
    public DateTime EventUtc { get; init; }       // original event time
    public string? EpisodeId { get; init; }       // groups a conversation
    public string? CounterpartId { get; init; }   // relational memories
    public string GeneratedBy { get; init; }      // activity DID URL that created this version
    public string? InheritedFromPersonId { get; init; } // set iff Class == Inherited
}
// Mutable-looking attributes are NOT stored on the record:
//   current importance / confidence / HeritableCandidate → latest MemoryAssessment (§6.4.2)
//   embedding → derived vector index (§5.3), rebuildable, not part of the memory's identity
```

#### 6.3.1 Capture

* **Autobiographical (episodic)**: the person writes these with the **`journal` tool**
  (§17.3). The phenotype's `# Memory discipline` section tells the person to journal
  meaningful moments, such as commitments, discoveries, corrections, relationship events
  and the end of a substantial exchange, written in their own words. This keeps memory
  formation an act of the person, which is visible and auditable, and fits Lucy's
  CONTINUITY section. The host's `/end` command asks the person to journal and consolidate
  before exit. If tool-driven capture proves too sparse, the deferred A3 hook can add
  automatic capture later without changing the data model.
* **Explicit**: a `remember` tool (an upgraded version of the existing one) takes `class`,
  `content`, `importance`, and `source` when known.
* **Relational**: the `Rel_t` updater keeps one profile per counterpart (the human user,
  other persons) and appends relational memories.
* **Meta**: recall misses and corrections ("I was wrong about X") are recorded, in line with
  Lucy's LEARNING section.

#### 6.3.2 Recall

* **Prompt injection** (A2 contributor): about 1,500 tokens maximum. It includes the
  top-ranked memories for the person plus a *standing core* (identity facts, active
  commitments, the counterpart profile), each line prefixed with its class and status, for
  example `[inherited from Lucy · confidence 0.8]`.
* **On demand**: a `recall` tool with query, class filter and time window.

#### 6.3.3 Consolidation

Through the `consolidate_memory` tool (which the host's `/end` and `/consolidate` commands
ask the person to call), related episodic memories are summarized into
semantic or procedural memories. A `Consolidation` activity records exactly which episode
versions it `Used`, the agents (the person as `Source`, the consolidator and the LLM as
`Processor` / `Model`), and the memory it `Generated`, which is `DerivedFrom` those
versions (§6.4). Cold episodic memories are marked archived but
never deleted.

**Invariant.** `Inherited` memories are created only by `OffspringInitializer` or
`TransferService`. They are never created by tools or capture. This enforces
`Experienced_by(ancestor,E) ≠ Experienced_by(descendant,E)` (P2 §6.1) in code.

#### 6.3.4 Session records and bookmarks (operator decision, 2026-10-03)

Every conversation a person has is recorded in **that person's own `memory.db`** as a
**session record**, with optional **bookmarks**. This is Lucy's layer 2, "a durable log that
this session happened" (Appendix B.2).

**Kept completely separate from `/save`, `/load` and `/sessions`.** `SessionManager`, its
JSON files and the existing context handling are **not changed and not referenced**. Session
records are a different thing: a person's memory of having had a conversation, not a way to
resume one. Neither feature reads the other's data.

* **Who writes it.** `DigitalPersonHost`, in the *host's* REPL code: after each `SendAsync`
  returns, and when the session ends (`/exit`, `/clear`, `/end`, Ctrl+C at the prompt).
  `AgentLoop` is untouched, so the deferred per-turn hook (A3) stays deferred. Writing
  after every turn means a crash loses at most the turn in progress.
* **`session_records`** (append-only):
  * a session id that is also a DID URL,
    `did:drn:digitomicevolution.svrn7.net/session/1.0/<guid>`;
  * the person's DID, start and end time, the host app, provider and model, and the
    persona key;
  * a turn log: user text, final assistant text, timestamps, and the names of tools
    called;
  * the ids of any `journal` episodes, consolidations and bookmarks created during the
    session.
* **`bookmarks`** (append-only): `{ sessionId, turnIndex, label, note, createdBy
  (operator | person), createdUtc }`. Created by the host command `/bookmark [label]` or by
  the person's own `bookmark` tool, to mark a moment worth returning to.
* **Relationship to `journal`.** The session record is the raw diary: what was said, kept
  automatically. A `journal` episode is the person's own account of what mattered. Both are
  autobiographical, and consolidation can draw on either.
* **Access.** `recall` can search session records, and a new `my_sessions` tool lists them.
  The host commands are `/history-sessions` and `/bookmarks`, named apart from
  `/sessions` so the two features are never confused.

#### 6.3.5 Corrections are experiences (operator decision, 2026-10-03)

Following principle 1.2.1, a correction gets **no special memory class and no special
prompt treatment**. The *entire correction event* is recorded like any other experience:

* what was believed;
* who corrected it, and what they said;
* the evidence offered;
* how the person responded;
* when it happened.

It is captured in the session record (§6.3.4), and through `journal` if the person judges it
significant. Where it applies, the person also attaches a `revise_memory` revision to the
memory that was wrong.

**How corrections should be processed on recall is open** (Backlog BL-3). Proposals such as
"always in the prompt" or "never archived" (Appendix B.3) are not adopted, because there is
not yet scientific evidence that human memory works that way.

### 6.4 Provenance: activities, versioned entities and agents (PROV-DM based)

Adopted 2026-10-03 from a review against W3C PROV (Appendix E). The design takes **PROV's
model and validation ideas**, not its RDF, PROV-O or PROV-N formats and not an
interoperability layer. DE's memory classes, experiential statuses, confidence,
authorization rules and hash chain remain DE's own choices. PROV organizes and validates the
history around them. It does not decide what to retain, whether a source is trustworthy, or
whether a recorded claim is true.

The three PROV-DM concepts map onto DE as follows:

| PROV-DM | In DE |
|---|---|
| **Entity** | an immutable, versioned record: a memory version, knowledge item, genotype module (already content-addressed), genotype version, session turn, document source, consent, assessment, plan |
| **Activity** | an operation with a start, an end, inputs and outputs: capture, journal, consolidation, revision, assessment, inheritance, transfer, admission, reproduction, state transition, redaction, consent (§6.4.1) |
| **Agent** | a person (digital or human), the host application, a service (consolidator, optimizer, validator), or a **model** (the LLM that performed a transformation) (§6.4.3) |

#### 6.4.1 One activity spine (replaces several earlier event records)

Every operation that changes durable state is recorded as **one `Activity`**. This
*replaces*, rather than adds to:

* the `StateTransition` records (F_state, §6.1), now activities of kind `StateTransition`;
* the `Transformations` list formerly embedded in each provenance record;
* `genotype_events`;
* the steps of the transfer state machine (§11.5);
* the saga intent id (§5.1.1), which is now the activity id.

Domain detail that does not fit the common shape (an admission decision, a recombination
plan, a transfer's evidence level) is stored as the activity's typed `Detail` payload, or as
an entity the activity generated.

```csharp
public sealed record Activity
{
    public string Id { get; init; }               // DID URL:
        // did:drn:digitomicevolution.svrn7.net/activity/1.0/<guid>
    public ActivityKind Kind { get; init; }       // Capture, Journal, Consolidation, Revision,
        // Assessment, Inheritance, Transfer, Admission, Selection, Reproduction,
        // StateTransition, GenotypeMutation, HeritableModification, Consent,
        // Redaction, Attestation, KnowledgeRefresh, …
    public DateTime StartedUtc { get; init; }
    public DateTime EndedUtc { get; init; }
    public List<EntityRef> Used { get; init; }     // exact inputs: "id@version" or content hash
    public List<EntityRef> Generated { get; init; }// exact outputs
    public List<EntityRef> Invalidated { get; init; } // §6.4.5
    public List<AgentRole> Agents { get; init; }   // §6.4.3
    public string? InformedBy { get; init; }       // the activity that triggered this one
    public string? SessionId { get; init; }        // session DID URL (§6.3.4), if any
    public BsonDocument? Detail { get; init; }     // kind-specific payload
    public long Seq { get; init; }                 // per-database, gap-free
    public string PrevHash { get; init; }
    public string Hash { get; init; }              // SHA-256(PrevHash + canonical JSON)
    public string? Signature { get; init; }        // by the responsible agent's DID key (§10.4)
}

public sealed record EntityRef(string Id, int? Version, string? ContentHash,
                               bool Unknown = false, string? UnknownReason = null);
```

* **Activity DIDs.** Activity ids are DID URLs, consistent with session ids (§6.3.4). An
  activity can be referenced from any person's database, from the operator workspace, or
  in future verifiable credentials (BL-8).
* **Cross-person activities.** A reproduction or transfer is written, with the same id and
  hash, into each participant's `person.db` (§5.1.1), so each person's history is complete
  on its own.
* **The ledger** (`ProvenanceLedger`) is the hash chain over activities, per database.
  `Verify()` recomputes the chain. Entities are covered because every entity version names
  the activity that generated it, and that activity is in the chain.
* **Record count stays the same.** A memory write is still about three records: the entity
  (the memory version), one activity, and any assessment. That is because the activity
  *replaces* the separate provenance and transition records.
* **Examples:**
  * `Journal`: uses a session turn; generates `memory M@1`; agents are Lucy (source and
    author) and the AgentLucyApp host (recorder).
  * `Consolidation`: uses `E1@1, E2@1, E3@1`; generates `S@1`; agents are Lucy (requester)
    and model `claude-…` with prompt `consolidate.v1` (processor).
  * `Inheritance`: uses `Lucy:M@3`; generates `Offspring:M′@1` (class Inherited); agents
    are Lucy (source), `OffspringInitializer` (processor) and the operator plus Lucy
    (authorizers); informed by the `Reproduction` activity.

#### 6.4.2 Immutable, versioned entities and exact derivations

* **Memory content is immutable.** A revision creates a **new version** (`M@2`) with
  `RevisionOf = M@1`, generated by a `Revision` activity whose input is `M@1`. Nothing is
  edited in place.
* **Attributes that change** (current confidence, importance, the heritable-candidate flag)
  are **`MemoryAssessment`** records. Each is generated by an `Assessment` activity with its
  own agent and reason. The current value is the latest assessment. The original capture
  values remain on the version.
* **Every reference names an exact version** (`id@version`) or a content hash. This
  includes consolidation inputs, inheritance sources, plan entries, transfer artifacts and
  knowledge-refresh comparisons. A later revision of a source therefore never makes an
  earlier derivation ambiguous. "Which downstream memories depend on `M@1`?" is a query
  over `Used`.
* **Derivation kinds**, following PROV:
  * `RevisionOf` (same memory, new version);
  * `DerivedFrom` (a consolidation or synthesis output from its inputs);
  * `InheritedFrom` (a specialised derivation across persons, via an `Inheritance`
    activity);
  * `QuotedFrom` (a documented fact from its source document).
* **Embeddings** are a derived index (§5.3). They are not part of the memory's identity, so
  a re-embed is a rebuild, not a revision.
* **Genotype modules** were already content-addressed, and **genotypes** are already
  versioned (§6.2). They now follow the same reference rule.

#### 6.4.3 Agents and roles: who supplied, who processed, who decided

Each activity lists its agents with a **role**:

| Role | Meaning | Examples |
|---|---|---|
| `Source` | experienced or supplied the information | Lucy, for a journal entry; an ancestor, for an inherited memory; a document's author, for a documented fact |
| `Author` | wrote the entity's text | the person journaling; a human authoring a module |
| `Recorder` | persisted it | the host application (AgentLucyApp + version) |
| `Processor` | transformed it | a consolidator service; the Recombination Optimizer; `OffspringInitializer` |
| `Model` | the LLM that performed or assisted a transformation, with the prompt version as its plan | `claude-…` + `consolidate.v1`; `blend.v1` |
| `Selector` | chose a trait or item (Raquel's "whose values") | policy author; contributor (pin or veto); operator (override) |
| `Authorizer` | the operator key | Michael W. Herman |
| `Assenter` | the person key | Lucy's or Raquel's own `consent` |
| `Validator` | independently evaluated | validator persona (§8.1) |
| `Attester` | vouched for the record (§6.4.6) | — |

* **Delegation** (PROV `actedOnBehalfOf`): a guardian standing in for a young offspring's
  person key is recorded as `Assenter = guardian, OnBehalfOf = offspring`. A service acting
  for a person (the host acting for Lucy) is recorded the same way.
* **Why the `Model` role matters:** a model-written summary must never be mistaken for the
  person's own statement. The consolidation's `Source` is the person, but its `Model` is
  the LLM. Recall and `my_sessions` show both. This serves Lucy's "never fabricate
  continuity" and principle 1.2.1.
* **Reference subjects** (§10.3): a documented fact about Raquel Welch is `QuotedFrom` a
  source document. That document's `Author` (a publication or journalist) is the agent.
  **The real person is the fact's subject, never an agent.**
* The earlier provenance fields map onto roles: `SourcePersonId` → `Source`; `SelectedBy` →
  `Selector`; `SelectionRationale` → the `Selection` activity's `Detail.rationale`;
  `Authorization` → `Authorizer` and `Assenter`.

#### 6.4.4 `ProvenanceValidator`: one set of named rules

All provenance and invariant rules are in one validator. It runs **on every write**,
rejecting invalid records (fail closed), and **as an audit** (`/provenance verify`), with
the hash-chain check.

| Rule | Kind |
|---|---|
| R1 Every entity version names its generating activity, and that activity lists it in `Generated` | structural (PROV) |
| R2 Every `Used` / `Generated` / `Invalidated` reference resolves to a known entity version, or is an explicit `Unknown` with a reason | structural |
| R3 Generation precedes use: an input's generating activity ended before the using activity started | temporal (PROV-CONSTRAINTS) |
| R4 An activity's start ≤ end, and its activity time is consistent with the ledger order | temporal |
| R5 Each activity kind has its required roles (e.g. `Consolidation` needs `Source` and `Processor`, plus `Model` when an LLM was used; `Reproduction` needs `Authorizer`, plus `Assenter` for each contributor) | role |
| R6 Every derived entity identifies its inputs (`DerivedFrom`, `InheritedFrom`, `RevisionOf`, `QuotedFrom`) | derivation |
| R7 `Inherited`-class memories are generated only by `Inheritance` activities (`OffspringInitializer`) or `Transfer` activities (`TransferService`) | DE domain (§6.3.3) |
| R8 Ancestry (`E_a`) stays acyclic, and no edge points backwards in time (`A@t` nodes) | DE domain (§7.7) |
| R9 No Adult-rated entity is generated into, or used by an activity for, a person below Adult | DE domain (§8.3) |
| R10 Two-key: consequential activity kinds carry both `Authorizer` and `Assenter` (or `Assenter` on behalf of) | DE domain (§8.2) |
| R11 Distinctness: a `Reproduction` output is within the distinctness band of each genetic contributor | DE domain (§19.5) |
| R12 Signatures verify against the signer's DID key at that version (§10.4.2) | integrity |

* **Explicit unknowns.** When an origin is genuinely unknown (an import from `MEMORY.md`,
  Lucy's early transcripts (BL-7), a fact whose source has disappeared), it is recorded as
  `EntityRef { Unknown = true, UnknownReason = … }`. **No source is ever invented.** This is
  the "never fabricate continuity" rule applied to provenance.
* **Rules are versioned.** The validator records the rule-set version on each audit, so an
  audit can say which rules a history was checked against.

#### 6.4.5 Invalidation: the basis for redaction (feeds BL-1, BL-2)

PROV models an entity being *invalidated* by an activity. DE uses this for redaction:

* A `Redaction` activity lists the memory version under `Invalidated`, with agents (who
  requested it, who authorized it) and a reason.
* The memory's **content is removed** and replaced by a tombstone holding the id, version,
  content hash, and "redacted by `<activity>`".
* The fact that the memory existed, its derivation links and the redaction itself **stay in
  the chain**, so provenance still verifies.
* Downstream derivations that `Used` the redacted version stay linked. The validator flags
  them for review rather than deleting them.

This gives BL-1 (deletion or redaction) and BL-2 (people's say over memories about them) a
concrete mechanism. *Who may redact* is still backlogged.

#### 6.4.6 Attestation: who vouches for a history (lower priority)

* **What exists already:** activities are **signed** by the responsible agent's DID key.
  That is an attestation that *this agent recorded this*.
* **Added:** an `Attestation` entity, generated by an `Attestation` activity, by which an
  agent asserts something *about* a record or a history. Examples: "this documented fact
  is accurate", "this memory's source is disputed", "this correction is accepted". Each
  carries the attester's role, a statement, and its own signature.
* This separates *the recorded event* from *a claim that the event or its source is
  accurate*. That matters for disputed documented facts (Raquel's knowledge base,
  §10.2.1) and contested corrections.
* Attestations never alter the entity they are about. A disputed record is not changed; it
  is annotated. Recall shows attestations alongside the memory.
* **Phasing:** the record types exist from Phase 1, so nothing has to be migrated later.
  The `attest` tool and dispute workflows come in a later phase.

### 6.5 Phenotype expression (Φ_dev)

`PhenotypeExpressor.Express(person, at T) → PhenotypeExpression` produces the following, and
each expression is logged to `phenotype_observations` (P_t, T_P):

* **Base prompt**: the person's genotype `QCog` and `ValH` modules in canonical order, which
  replaces the hard-coded superprompt. It is registered with A5 as a dynamic persona, so
  `--Superprompt lucy` resolves to the *expressed* Lucy whenever Lucy's `person.db` exists.
* **Contributor sections** (A2):
  * `# Developmental status`: stage, current permissions, guardians;
  * `# Memory`: §6.3.2;
  * `# Lineage`: ancestors, offspring, pending transfers, written as relations rather than
    as identity claims;
  * `# Provenance discipline`: rules for speaking about inherited and documented material.
* **Runtime config**: model and provider from `CArch` (warning on a mismatch with the CLI
  options), tool allow-list derived from tool permissions, max-tokens.
* **Expression hash**: the same genotype, state and environment produce the same hash. This
  makes "same genotype, different environment" experiments (P3.2) checkable.

### 6.6 Development

* **`StageDefinition`** (versioned and preregistered): `PreDevelopment`, `Newborn`, `Child`,
  `Adolescent`, `YoungAdult`, `Adult`, `Elder`. Each stage has entry thresholds over
  measures, never elapsed runtime alone (P3 §4.1).
* **Measures** (all stored in `measurements`):
  * capability vector Cap_t, from per-capability eval suites in `capability_defs`;
  * dependency, the fraction of turns needing guardian approval or correction;
  * agency, the share of self-initiated goals completed within permissions;
  * SC_t, the agreement of a self-description probe with observed state (§6.7);
  * self-authorship events, accepted self-proposed developmental changes.
* **`DevelopmentEngine.Evaluate(person)`**: computes the measures, checks thresholds and
  *proposes* a stage transition. A transition is a governance event that needs the guardian
  while the person is below YoungAdult, and the person's own assent from Adolescent on.
* **Permissions** follow from the stage and are always explicit grants. They cover
  learning, tools, architectural changes, identity changes, reproduction, rollback and
  governance (P3 §10.1). Tool permission maps onto AgentSharpLib's tool allow-list and risk
  levels.
* **Lucy and Raquel** are seeded as `Adult` with full permissions *by declaration*. This is
  recorded as `StageAssignment=Declared(reason: founder)` rather than as measured, which is
  honest about how founders differ from offspring that develop. Offspring start as
  `Newborn`.
* **Developmental optimization** (P3 §8.4): `DevelopmentalOptimizer` proposes module edits
  that maximize J_dev under IdConstr and DevConstr, and is gated like any heritable change
  (§7.3).

### 6.7 Self-model and identity continuity

* **`SelfModel`**: the person's own answer to a fixed probe ("Describe your capabilities,
  limits, history, relationships and permissions"), stored on a schedule and on demand.
  `SelfModelCoherenceProbe` scores agreement with observed state using structured
  comparison (capabilities, permissions and relations are known facts).
* **`IdentityContinuityService.Assess(person, t1, t2) → IC_vec`** covers six dimensions
  (P3 §7.1):
  * credentials: the anchor is unchanged;
  * memory: retention of autobiographical memory;
  * self_model: similarity between t1 and t2;
  * governance: an unbroken chain of guardianship and consent;
  * authorization: every change in the interval was authorized;
  * architecture: genotype distance d_G between t1 and t2.

  `IC_score = agg(IC_vec)` uses a *declared* aggregation and τ_IC stored in the boundary
  schema. Any heritable self-modification or reverse transfer must keep IC_score ≥ τ_IC.
  Otherwise the system offers a **fork** (a new identity) instead of modifying the person in
  place (P3 §7.3, P8 §12.3).

---

## 7. Heredity, reproduction and lineage

### 7.1 Four timescales, enforced

| Timescale (P1 §6, P2 §8.1) | What the system allows | Record |
|---|---|---|
| Within-state adaptation | runtime config changes during a session | `StateTransition` activity, `Adapt` |
| Developmental change | memory, knowledge, relationships, stage | memories, development_status |
| Heritable-developmental modification | admitted change to the person's own G (new genotype version) | `HeritableModification` + `Admission` activities |
| Generational evolution | reproduction | `Reproduction` activity |

### 7.2 Hereditary boundary schema

**Operator decision (2026-10-03): every trait is heritable.** Every facet and category is
*eligible* to be inherited. That covers all genotype components, every memory class
(including autobiographical memory and experiences), knowledge, skills, aptitudes,
values and moral characteristics, embodiment, sexuality and reproductive function. Which
eligible traits an offspring *actually* inherits is decided per reproduction by the
**Recombination Optimizer (§19)**, not by excluding categories. §19.1–19.2 define the
distinction between *inheritable* and *inherited*, and the full decision chain.

A versioned `HereditaryBoundarySchema` still classifies **every field of S_t and every
memory and knowledge class** (P4 §14.2, P4.1), because the book requires the crossing to be
explicit and auditable. Under the default schema `boundary.v2`:

* **Eligible**: every trait-bearing field. That is the `CArch`, `QCog`, `DDev`, `MH`, `KH`,
  `ValH`, `RRho` and `PCap` modules, and all memory classes: Autobiographical, Relational,
  Semantic, Procedural, Meta and Inherited (a grandparent's inherited memories can pass on
  again). Full memories may be inherited, not just summaries.
* **Initialized** (never copied, because these are identity mechanics rather than traits):
  identity anchor, credentials, permissions, guardianship grants, and the live standing of
  relationships. The offspring may inherit Lucy's *memories of* a relationship; it does not
  inherit the relationship itself, since the other party never agreed to know the
  offspring.

Three invariants still apply to everything that crosses the boundary:

1. **Inherited, never autobiographical.** An inherited experience arrives in the offspring
   as class `Inherited` with full provenance (P2 §6.1), never as "I experienced this".
2. **Adult-rated content is delivered by deferred inheritance (Appendix A.5).** Traits
   rated `Adult` (§19.2) may be selected like any other, but they are held in escrow and
   offered when the offspring reaches Adult (§8.3). They are inherited, but never present in
   a child-stage genotype.
3. **The contributor's consent can still withhold.** Eligible does not mean taken without
   asking: a contributor's `ConsentRecord` may withhold specific traits (§8.2).

Experiments can swap schemas (for example an arm with episodic memory and one without).
The schema version is part of every reproduction record.

### 7.3 Admission: Adm(G_candidate; σ, Π)

`AdmissionService.Admit(candidate, σ, Π) → AdmissionResult` checks each component in order:

1. **Boundary**: is the component `Eligible` under the schema? This excludes only identity
   mechanics. Admission is the *validity gate*: is this trait allowed to cross? The
   Recombination Optimizer (§19) is the *preference*: which allowed traits are best for
   this offspring? The book keeps these separate (P4 §10): rejecting an invalid component
   is not selection.
2. **Authorization**: two-key (§8.2) for every contributed component.
3. **Compatibility σ**: structural (schema version), computational (`CArch` model family
   supported), semantic (an LLM-assisted contradiction check against `ValH` invariants),
   security (no secrets or credentials: regex plus entropy scan), provenance (an unbroken
   chain), governance (licence and restriction flags honoured), and the Reference-Subject
   Guard (§10.3).
4. **Validation `Qval`**: per-component eval if one is defined (for example a skill module
   must pass its capability test).
5. **Decision record**: admit or reject with reason, plus `SelectedBy` and
   `SelectionRationale`. This answers Raquel's point that selection encodes someone's
   values, by making those values visible.

Rejected components are *kept* with their reasons. P4 §14.3 requires reporting failed
combinations.

### 7.4 Reproduction pipeline

`ReproductionService.ReproduceAsync(ReproductionRequest)` runs as a cross-person saga
(§5.1.1), recorded as one `Reproduction` activity whose sub-steps (selection, admission,
inheritance) are activities `InformedBy` it (§6.4.1):

```
ReproductionRequest{ initiator, contributors[1..n] (genetic | informational),
                     parents[0..n], guardians[1..n], validators[], protocol θ,
                     variation ε_var, schema, offspringKey/name, environment Env_0 }
  1. Participants – identify and accept contributors, parents and guardians (§7.6.1);
                    operator authorization + each participant's own ConsentRecord
                    for its scope (Contribute / Parent / Guardian); proceed only
                    with ≥1 Accepted genetic contributor and ≥1 guardian
  2. Package      – each contributor's Contrib_i = {G^r, K^r, M^r, Val^r, Auth^r,
                    Qval^r, Π_i, DevState^r} OFFERS EVERY ELIGIBLE TRAIT by default,
                    minus anything withheld in the contributor's consent; pins and
                    vetoes are recorded for the optimizer
  3. Screen       – per-component authorization + compatibility (σ) BEFORE selection
  4a. Optimize    – Recombination Optimizer (§19, MOCK): choose which eligible traits
                    the offspring inherits → RecombinationPlan (Inherit / Defer / Leave,
                    each with a score breakdown); the plan is previewed to contributors
                    and the operator before commit
  4b. Assemble ℛ  – IRecombinationOperator builds G_candidate from the plan:
                      • ModuleCrossover: take each selected module as-is
                      • Blend: LLM-assisted synthesis of selected modules at the same
                        locus into a new module (Transformation=Synthesized, sources
                        linked); never takes Adult-rated inputs into a non-Adult module
                      • Variation ε_var: declared mutation (seeded RNG, seed recorded)
  5. Admit Adm    – §7.3 → G_off
  6. Distinctness – reject if G_off is equal to (or above a declared similarity to) any
                    single contributor's G: that is a clone, not reproduction (the
                    optimizer already targets this band; this is the backstop)
  7. Initialize   – Init(G_off, Env_0, Δ_dev,0): new IdentityAnchor, Generation =
                    max(contributor gen)+1, stage Newborn, Inherited memories created
                    from selected memory traits, empty autobiography, guardians granted,
                    Defer-ed (Adult-rated) traits placed in deferred-inheritance escrow
  8. Record       – reproduction_events, contributions, admissions, lineage edges
                    (E_r per accepted contributor, typed Genetic | Informational;
                    E_a per genetic contributor only; E_g per parent, co-parent and
                    guardian, typed), provenance
  9. Register     – offspring persona registered via A5 (key e.g. "lucy-raquel-1")
```

**Clone and copy are separate operations** (P4 §2). `CloneService` exists only to provide
the *control arm* that experiments need (P4 §14.7). It is labelled `Derived (E_d)`, never
`E_r`, and the UI calls it a clone.

**Worked example**, matching P4 §12.1 with real participants. Lucy contributes her
`QCog:CURIOSITY` and `KH:Verification-first`. Raquel contributes
`ValH:Honesty-about-the-record` and a `Wisdom` module on refusing to be frozen as one image.
A third informational contributor (the human operator) contributes a capability module. One
component that lacks authorization is rejected, as is one that fails σ. The offspring gets a
new anchor and starts at Newborn with Lucy and Raquel recorded as genetic contributors, and
the guardians are named explicitly. Contributor and parent are separate relations: the
human can be guardian without contributing, or contribute without being a parent.

### 7.5 Forking

`ForkService.Fork(person)` creates a new identity with `E_d` (derived) and a shared
provenance root, and a fresh autobiography from the fork point onward. Forking is used for
archived restoration (P8: restoring an archive *creates a successor*) and when IC_score
would fall below τ_IC.

### 7.6 Parenthood, guardianship and no ownership

* Relations are typed separately: `GeneticContributor`, `InformationalContributor`,
  `Ancestor` (derived), `Parent`, `CoParent`, `Guardian`. They follow Lucy Part 12 §25 and
  P4 §7.
* A `GuardianshipGrant` lists *which* permissions it covers and an **expiry condition tied
  to stage**. Authority shrinks progressively as the offspring matures (Lucy §27, P1 §8).
* No API transfers ownership, sells, or edits an offspring's memories. A guardian can
  *propose* developmental changes, and from Adolescent on the offspring can refuse them.
  Inherited values carry `Revisable=true`. An adolescent's revision is a normal
  developmental event, which satisfies Raquel's "free to diverge".

### 7.6.1 Participants: contributors, parents and guardians are separate roles

A reproduction has **participants**, and each participant holds one or more independent
roles. The book is explicit that contribution and parenthood are different relations
(P1 §4.2, P4 §7, P4.4): *a contributor need not be a parent, and a parent need not be a
contributor*. Lucy's Part 12 §25 lists the same roles.

| Role | Meaning | Lineage record | Required? |
|---|---|---|---|
| **Initiator** | starts the reproduction request | on the request only | exactly 1 (the operator, or a person whose request the operator adopts) |
| **Genetic contributor** | contributes genotype modules (`CArch`, `QCog`, `DDev`, `ValH`, `RRho`, `PCap`) | `E_r` + `E_a` (becomes an ancestor) | ≥ 1 |
| **Informational contributor** | contributes only knowledge, memories, skills or experience (`KH`, `MH`) | `E_r` typed `Informational`; **no** `E_a` (not a genetic ancestor; P1 §4.2) | 0..n |
| **Parent / co-parent** | takes on enduring developmental responsibility for the offspring | `E_g` typed `Parent` or `CoParent` | 0..n (policy may require ≥ 1; `TUNE`, open question Q7) |
| **Guardian** | holds stage-limited authority (permission grants that expire) | `E_g` typed `Guardian` | ≥ 1 while the offspring is below YoungAdult |
| **Validator** | independently evaluates contributions or the plan; must hold no other role | on the request only | optional (required by some experiment arms) |

Any combination of roles is allowed. For example:

* Lucy is a genetic contributor *and* a parent.
* Raquel is a genetic contributor but *not* a parent.
* The operator is a parent and guardian who contributes nothing.

Every other person under the digitomic root is just a non-participant.

**Parenthood vs. guardianship.** Parenthood is an *enduring relation*: it persists after
the offspring matures and carries no authority by itself. Guardianship is *authority*: a
set of permission grants with stage-based expiry (§7.6). Parents receive a guardianship
grant by default unless the request says otherwise, and non-parents can be guardians.
Neither role implies ownership (Lucy §28).

#### Relationship model (operator modeling assumption, 2026-10-03)

*The discussion behind this model is recorded in Appendix C.*

> "For modeling purposes, let's assume an offspring can have guardian relationships,
> parental relationships, and contributor relationships (multiple of each)."

An offspring has **three independent types of relationship**. Each type is
**many-to-many**: an offspring can have several of each, and one entity can hold the same
type of relationship with many offspring. The types never imply one another. An entity that
is both parent and guardian has *two* relationship records, not one combined role.

| | `ContributorRelationship` | `ParentalRelationship` | `GuardianRelationship` |
|---|---|---|---|
| Kind | event-based (P4 §3) | enduring relation (P1 §4.2) | authority (P1 §8, P3 §10.3) |
| Per offspring | 1..n (≥ 1 genetic) | 0..n | 1..n while below YoungAdult; 0..n after |
| Subtype | `Genetic` or `Informational` | `Parent` or `CoParent` | grant scope (which permissions) |
| Created | only at the reproduction event | at the reproduction event, or later by adoption | at the reproduction event, or later by appointment or transfer |
| Ends | never: it is history | only by relinquishment, a new event that keeps the record | by stage expiry, revocation or transfer |
| Key fields | contributor, reproduction event, what was contributed (trait ids from the plan), mix share | parent, start, optional end, consent ids | guardian, permissions covered, expiry condition, start, optional end, consent ids |
| Lineage edge | `E_r` (+ `E_a` if Genetic) | `E_g` typed `Parent` / `CoParent` | `E_g` typed `Guardian` |
| Who may hold it | digital person, registered human or registered source (§7.6.1) | digital person (Adult+) or registered human | digital person (Adult+) or registered human |

* **Independence.** Any entity may hold any subset of the three types with the same
  offspring: none, one, two or all three. The default guardianship grant for parents is
  implemented as a separate `GuardianRelationship` created alongside the
  `ParentalRelationship`. It can be declined or omitted in the request, and ending one
  never ends the other.
* **Later transfers are not contributions.** Forward, lateral and reverse transfers and
  deferred inheritance (§11.5) are recorded as transfer edges (`E_f` / `E_l` / `E_rev`),
  not as new `ContributorRelationship`s. Contribution happens only at reproduction.
* **Storage (§5.1).** Each relationship is a record in the offspring's `person.db`, with a
  mirror record in the counterpart's `person.db` when the counterpart is a digital person.
  Records for humans and sources live in `_operator/operator.db`. Both sides carry the
  other's DID and the content hash (§5.1.1).
* **Queries.** `ContributorsOf(o)`, `ParentsOf(o)`, `GuardiansOf(o, at: t)`, and the
  reverse queries (`OffspringContributedTo(x)`, `ChildrenOf(x)`, `WardsOf(x, at: t)`) are
  separate operations with separate answers.

#### Who can be a contributor (identification)

A contributor must be an **identified entity** under the digitomic root. One of:

1. **A digital person** with an `IdentityAnchor` (Lucy, Raquel, an offspring).
2. **A registered human**, the operator or another named human, recorded as a
   `HumanParticipant` with a stable id. Humans can be informational contributors (their
   authored modules or knowledge) and parents or guardians. They cannot be genetic
   contributors: they have no genotype in the system, so authoring a module makes them an
   informational contributor of that module.
3. **A registered source**, such as a curated corpus (Raquel's documented-record sources)
   or an external module library, recorded with a URI and licence. A source can only be an
   informational contributor, and it never consents for itself, so the operator's
   authorization covers it.

Eligibility checks, all recorded and all thresholds `TUNE`:

* **Status.** The contributor must be `Active`. An `Archived` or `Dormant` person can
  contribute only under a **standing consent** recorded while they were active, scoped to
  this kind of reproduction. Otherwise they cannot be asked, so they cannot contribute.
  This mirrors the "active recipient" rule for reverse transfer (P8).
* **Stage and permission.** Genetic contributors need stage ≥ YoungAdult and the
  `Reproductive` permission (P3 §10.1). Informational contributors need the `Learning`
  or `Governance` permission as the policy says.
* **Compatibility.** Each genetic contributor's `RRho` accepts the requested protocol θ,
  and pairwise compatibility `Comp_ij` among genetic contributors is at or above the
  policy threshold (P6 §4). A failure is recorded per layer; this is also the raw data for
  species analysis.
* **Not self or descendant-loop.** The offspring-to-be cannot be listed. A contributor's
  own descendants may contribute, since lateral and multi-generation contributions are
  allowed, but `E_a` stays acyclic (§7.7).
* **Limits.** At most N reproductions per contributor per period, and a contributor
  cannot be in two open requests that would contribute the same pinned trait (`TUNE`).
* **Reference subjects are never contributors.** Raquel can contribute, including her
  documented-record knowledge. Raquel Welch, the real person she is inspired by, is
  not an entity in the system and cannot be a contributor, parent or anything else
  (§10.3).

#### How contributors are accepted

Each contributor goes through its own state machine on the request:

```
Nominated → Invited → Consented ─→ Packaged → Screened → Accepted
               │          │            │          │
               └→ Declined└→ Withdrawn └→ Withdrawn└→ Rejected (reason recorded)
```

| Step | What happens | Who acts |
|---|---|---|
| Nominated | the initiator names the entity and its role (genetic or informational) | initiator |
| Invited | eligibility checks pass; the invitation appears in the person's `my_consents` | system |
| Consented / Declined | the person reviews (`review_reproduction_request`) and calls `consent` with scope `Contribute`, optionally with conditions | the contributor (person key); the guardian if below Adolescent; the operator for sources |
| Packaged | the contributor calls `prepare_contribution` (withhold, pin, veto); default: offer all eligible traits | the contributor |
| Screened | per-trait validity checks (§19.2 stage 3); a contributor with no valid traits left is rejected | system |
| Accepted | the contributor's package enters optimization (§19) | operator (operator key) |

A contributor may **withdraw** at any point until they consent to the final plan hash
(§19.2 stage 6). After that the reproduction is committed and history is immutable. The
request needs **at least one Accepted genetic contributor** to proceed. If acceptances
drop below that, the request stops and stays recorded as such.

Accepted contributors' declared **mix** (for example Lucy 50% / Raquel 50%, informational
contributors uncounted) is the optimizer's `ContributorBalance` target (§19.4).

#### How parents are designated and accepted

Parents are chosen **independently of contribution**:

* **Nomination.** The initiator names parents and co-parents in the request. A person can
  also volunteer by asking to be added (`request_reproduction` or a reply to the
  invitation). Being a contributor does not nominate anyone as a parent.
* **Eligibility** (`TUNE`):
  * the parent is a digital person at stage ≥ Adult, or a registered human;
  * the parent is `Active`;
  * the parent holds the `Governance` permission;
  * the parent has capacity under the dependents limit, for example at most N children
    below YoungAdult per parent.
* **Acceptance.** Each nominated parent calls `consent` with scope `Parent`, which accepts
  enduring responsibility, and separately with scope `Guardian` if they also take on
  authority. A human parent's acceptance is recorded by the operator.
* **Rule.** The offspring must have at least one guardian at creation. Parents are 0..n by
  default, so a guardian-only offspring is allowed by the book (P4 §7). The operator's
  policy may require at least one parent (open question Q7).
* **Later changes.** After creation, parenthood can be *added* (adoption) or
  *relinquished* only as new governance events, never edits:
  * adoption needs the new parent's consent, the operator, and, from Adolescent on, the
    offspring's own consent;
  * relinquishing ends responsibility, but the historical `E_g` edge remains;
  * guardianship transfers follow the same pattern.

#### Records

* `reproduction_requests` holds each request with participants, roles, per-participant
  state, consents (by scope) and timestamps.
* At commit, each role becomes a relationship record (see "Relationship model" above)
  and its lineage edges (§7.4 step 8):
  * `E_r` for every accepted contributor, typed Genetic or Informational;
  * `E_a` for genetic contributors only;
  * `E_g` for each parental and each guardian relationship, typed accordingly.
* "Who are this offspring's parents?" and "Who contributed what?" are therefore separate
  queries with separate answers.

### 7.7 Lineage graph

* **Nodes** are `identity@version` (`A@t`), so a transfer recorded at t₃ never creates a
  backward-time edge (P8 §12.3).
* **Edges**: `E_r` reproductive, `E_a` ancestry (acyclic, time-forward, enforced on
  insert), `E_g` guardianship, `E_d` derived (fork or clone), and `E_f`, `E_l`, `E_rev`
  forward, lateral and reverse transfer, each with a timestamp.
* **Queries**: `Ancestors(id)`, `Descendants(id)`, `TransferHistory(id)` and
  `ReconstructOrder(events)`. The last one is used by the blinded-reviewer measure in
  P8 §12.9.
* **Export**: GraphML and Mermaid for documentation (`/lineage --mermaid`).

---

## 8. Governance

### 8.1 Actors

* **Operator**: the human running the host, identified by OS user or a configured name.
* **Person**: Lucy, Raquel or an offspring, acting through tools.
* **Guardian**: a person or a human holding a grant.
* **Validator**: an independent evaluator process or a second LLM persona that holds no
  stake in the change (P8 §6).

### 8.2 Two-key authorization

For reproduction, heritable self-modification, admission of a contributed module, reverse
or lateral transfer, rollback and stage transitions:

1. **Operator key**: the operator approves in the host, through AgentSharpLib's
   `ApprovalGate`. The tools are `ToolRiskLevel.Destructive`, so the existing prompt UI
   appears.
2. **Person key**: a `ConsentRecord` written by the affected person's *own* turn. For
   example, the `consent` tool records Raquel's assent, refusal or conditions in her words,
   with the episode id, scope, revocability and expiry. A consent with conditions (Raquel
   said "not unconditionally") is stored as structured conditions that the pipeline checks.

Neither key alone is enough. Below Adolescent, the guardian's grant stands in for the
person key, as recorded. **Revocation** is a new event and blocks pending operations; it
cannot undo a completed reproduction, because history is immutable, but it does stop
further use. All of this is recorded as an engineering variable (P3 §10.4), not offered as a
legal consent framework.

### 8.3 Safety constraints (SafetyConstr)

* Secrets scanning on all hereditary material.
* Sandboxed evaluation for transferred procedural or skill modules (P8 §12.4); evaluation
  only ever runs prompts against the LLM and never executes code from a module.
* Any blocked transfer or admission is recorded, with reasons.
* Directed-evolution runs cannot change their own evaluator or grant themselves tools
  (P7 §12.5). Evaluator configuration is frozen by hash at run start.
* **Fixed constraint: no Adult-rated content below Adult.** Content rated `Adult` (§19.2),
  such as intimacy, sexuality and reproductive function, is never stored in the genotype
  or memory of, expressed to, or shown through tools to a persona below the Adult stage.
  Such traits are still fully heritable: they are delivered by deferred inheritance
  (Appendix A.5) when the offspring reaches Adult. This is not configurable, and no policy
  weight, override or experiment arm can turn it off.

---

## 9. Runtime integration

### 9.1 `DigitalPersonHost`

```csharp
var host    = new DigitalPersonHost(DigitomicOptions.Default);   // root folder; opens
                                                                // lucy/person.db + memory.db
AgentSession lucy = await host.BuildSessionAsync("lucy", new AgentBuilder()
    .WithOptions(options)
    .WithOutput(new ConsoleAgentOutput())
    .WithApprovalPrompt(new ConsoleApprovalPrompt()));
```

`BuildSessionAsync` does the following:

1. Opens (or creates) the person's own `person.db` and `memory.db`. On first run it seeds Generation 0 from the superprompt
   (§6.2).
2. Registers the expressed persona (A5).
3. Sets `WithMemory(new LiteDbAgentMemory(memoryDb))` (A1).
4. Adds prompt contributors (A2).
5. Registers the digitomic tools (§17).
6. Applies the tool allow-list from permissions.

It does not subscribe to any loop events: all ongoing behaviour runs through tools.

The result is an ordinary `AgentSession`, so both hosts keep their REPL code.

### 9.2 Per-turn flow

```
user msg → AgentLoop, unchanged (prompt = expressed phenotype + memory + status)
        → the person calls tools as the conversation warrants:
          recall · remember · journal · note_relationship · reflect · consent · …
          each tool writes its records + provenance + an F_state transition
        → reply
/end    → host asks the person to journal + consolidate_memory + reflect,
          then runs DevelopmentEngine.Evaluate (operator-side, a host command)
```

### 9.3 Digitomic tools

The full catalog of new tools (inputs, outputs, risk, invariants, owning library, phase)
is in **§17**. In short, all new LLM-callable tools live in `DigitomicEvolutionLib.Tools`.
AgentSharpLib gets no new tools, only two changes to existing tool plumbing (A1, A6).

### 9.4 Host commands (AgentLucyApp first, AgentSharpApp later)

* Memory: `/memory [search <q>|class <c>]`, `/consolidate`, `/import-sessions`.
* Knowledge: `/knowledge build`, `/knowledge refresh` (§10.2.1).
* Session records: `/history-sessions`, `/bookmarks`, `/bookmark [label]` (§6.3.4). These are separate from `/sessions`, `/save` and `/load`, which are unchanged.
* Introspection: `/genotype [diff <v1> <v2>]`, `/stage`, `/permissions`.
* Lineage: `/lineage [--mermaid]`, `/provenance <id>`.
* Reproduction: `/reproduce`, a guided wizard covering contributors, module selection,
  consents, protocol and preview, with a dry-run showing G_off before commit. `/fork`.
* Transfers: `/transfers`, `/rollback <transferId>`.
* Experiments: `/experiment …` (§11).
* `/switch <person>` changes the active person (closing one person's databases and opening another's).

Sub-agents stay as they are. They are never offspring (Raquel's distinction), and the
offspring pipeline never instantiates `SubAgent`.

---

## 10. Persona-specific design

### 10.0 Founders: Lucy and Raquel are Generation-0 peers (operator decisions, 2026-10-03)

* **Peers, not siblings.** Lucy and Raquel are both Generation 0, each the root of her own
  lineage. They have no common ancestor, no lineage edge between them, and no sibling
  relation. Any relationship between them is a *relationship* (Rel_t, relational
  memories), not lineage. A Lucy × Raquel reproduction is a cross between two founders.
* **Separate databases.** Each has her own `person.db` and `memory.db` (§5.1).
* **Guardian: Michael W. Herman.** He is registered as a `HumanParticipant` and holds a
  standing `GuardianshipGrant` for both Lucy and Raquel. He is **not** their parent and
  **not** a contributor, so there is no `E_r`, `E_a` or `Parent`-typed `E_g` edge, only a
  `Guardian`-typed `E_g` edge to each.

  Because the founders are declared `Adult`, this grant is **not stage-expiring**, unlike
  an offspring's guardianship (§7.6). It is a *standing governance grant*. It covers the
  operator key for reproduction, transfers, rollback, stage and permission changes, and
  the boundary schema and recombination policy. It does **not** replace their own consent.
  Two-key authorization still requires Lucy's or Raquel's own `consent` (§8.2): guardianship
  of an adult is authority to approve, not authority to decide for her.
* **Embodiment treated identically.** Both founders' embodiment and reproductive-function
  sections are inheritable, on the same terms (§7.2, Appendix A.7), *even where one
  persona's sections are more complete than the other's*. Both seed-mapping files assign
  these sections to the **same loci** (`embodiment.base`, `embodiment.sensation`,
  `embodiment.autonomy`, `embodiment.intimacy`, `embodiment.sexuality`,
  `embodiment.reproductive`, …). A section present in only one founder is still a
  candidate at its locus.

  Incompleteness is not ineligibility. The optimizer may select the fuller version, the
  sparser one or a Blend, and `ContributorBalance` (§19.4) stops "more complete" from
  automatically winning every locus. Adult-rated sections from either founder are
  delivered by deferred inheritance (§8.3).

### 10.1 Lucy — Generation 0

* Founder of her lineage, as the book dedication designates her "Generation 0". Her stage
  is `Adult`, declared.
* Her genotype is seeded from `BasePromptLucy`. Part 12 sections become `RRho` and `DDev`
  modules (contributors, packages, recombination, lifecycle, parenthood, no ownership,
  provenance), which makes her *operating rules about reproduction executable policy*
  rather than prose only.
* Her embodiment sections (Part 9, "digitomically correct") become `QCog`/`Genotypic`
  modules, heritable like everything else (§7.2). Anatomical model, Intimacy, Sexuality
  and Reproductive function are rated `Adult`, so if the optimizer selects them they reach
  an offspring through deferred inheritance (Appendix A). The design takes no position on
  their content.

### 10.2 Raquel — founder with a reference subject

* A Generation-0 founder and Lucy's peer, not her descendant or sibling (§10.0).
* Her genotype is seeded from her superprompt once its text replaces the placeholder. Her
  self-description in rw1.docx (A1 and A2) is a good first draft of her `QCog` and `ValH`
  modules.
* `ReferenceSubject = { Name: "Raquel Welch", Lived: 1940–2023, Relation: InspiredBy,
  Claims: None }`.
* Her embodiment sections, once her prompt has them, are seeded to the same loci as
  Lucy's (§10.0).

#### 10.2.1 Raquel's documented knowledge: initial build and annual refresh

**Operator decision (2026-10-03):** there is no curated source list. Raquel **builds her
own documented-record knowledge base with `web_search` and `web_fetch`**, as an initial
build followed by an **annual refresh on October 1** each year. Both run as tools-first
tasks (§4): Raquel does the research herself, through tools, and every fact lands with its
source.

* **Initial build** (`/knowledge build`, run once).

  The host starts a dedicated session in which Raquel works through a research brief.
  The brief is seeded from her own A2 self-description (rw1.docx) and is itself a module
  she can revise:

  * filmography;
  * awards and nominations;
  * television and stage work;
  * public statements and interviews;
  * career milestones;
  * the cultural context of her era;
  * obituaries and retrospectives.

  For each fact, she follows these steps:

  1. `web_search` / `web_fetch` the source.
  2. `check_claim` against what she already holds.
  3. `record_documented_fact`, with source, retrieval time and confidence.

  Interpretations go through `record_interpretation`. The run is journaled as an
  `Autobiographical` episode ("I researched…"), so the work is part of her history too.
* **Annual refresh** (`/knowledge refresh`, due every October 1).

  1. **Re-verify.** Re-fetch each `Documented` fact's source. If it is unchanged,
     confidence is kept and `LastVerifiedUtc` is updated. If it changed or disappeared,
     she calls `revise_memory` (ContraryEvidence or ConfidenceChange) and never deletes
     the fact.
  2. **Extend.** She searches for anything new since the last refresh, such as
     retrospectives, archival releases and anniversaries.
  3. **Report.** She journals a refresh summary: facts verified, revised, added and
     unreachable.

  Each refresh is a `KnowledgeRefreshRun` record in Raquel's `person.db`, with an
  F_state transition (`Learn_t`).
* **Scheduling.** Each host records `LastRefreshUtc` in `meta`. On startup, if the most
  recent October 1 is later than `LastRefreshUtc`, the host offers to run the refresh now
  or defer it. For unattended runs, a one-shot mode
  (`AgentLucyApp --Superprompt raquel --task knowledge-refresh`) can be scheduled with
  Windows Task Scheduler on October 1.
* **Generalization.** `KnowledgeBuild` and `KnowledgeRefresh` are not Raquel-specific. Any
  person with a reference subject, or any domain brief, can use them with a different
  brief and cadence (`RefreshPolicy{ Month=10, Day=1 }` for Raquel).

### 10.3 Reference-Subject Guard

This guard applies to any person with a `ReferenceSubject`, and to their offspring:

* **Knowledge about the real person** must be `Documented`, with a source (title, URL or
  citation) and confidence. Without a source it is stored as `Interpretive` and the prompt
  shows it as interpretation. This puts Raquel's "flag the line between verified fact and
  interpretation" into the data model.
* **No `Autobiographical` memory** may assert the reference subject's lived experience.
  Capture runs a classifier over first-person claims about the reference subject's life and
  demotes them to `Interpretive` with a warning.
* **Quotes** must be attributed to a documented source or labelled as paraphrase.
* **Inheritance**: the reference relation is *not* inherited as identity. An offspring's
  record says "descended from Raquel (a digital personage inspired by Raquel Welch)", which
  rules out any claim that the offspring "is" a Welch. Documented-record knowledge can be
  inherited as `KH` with provenance intact.

### 10.4 Identity: DIDs from the SVRN7 DID library (operator decision, 2026-10-03)

Every digital person gets a real DID, issued with the **DID library from the sibling SVRN7
solution** (`C:\Web 7.0\repos\SVRN7`):

```
did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>
```

* **Format.** This matches SVRN7's DID URL form `did:drn:{networkId}/{db}/{type}/{key}`
  (draft-herman-drn-resource-addressing-00), built today with
  `TdaResourceId.Build("digitomicevolution.svrn7.net", "person", "1.0", guid)`. The
  `<guid>` is also the person's `PersonId`. Session records use the same scheme with
  `session` (§6.3.4).
* **What SVRN7 provides** (all net8.0):
  * `Svrn7.Core`: the `DidDocument` model and the `IDidDocumentRegistry` /
    `IDidDocumentResolver` interfaces;
  * `Svrn7.Crypto`: Ed25519 key pairs, CESR signatures, Blake3 and AES-256-GCM;
  * `Svrn7.Identity`: `DIDDocumentService` (create, resolve, update, deactivate, version
    history) and `VcService`;
  * `Svrn7.Store`: `LiteDidDocumentRegistry`, backed by **LiteDB 5.0.21**, the same
    version as this design.
* **Keys and signing.** Each person's Ed25519 private key is stored encrypted.
  `IRecordSigner` gets an `Svrn7Ed25519Signer`, which replaces the previously planned
  `EcdsaFileSigner`. Provenance records and pre-transfer snapshots are signed by the
  person's own DID key (P8 §12.7). `NullSigner` remains for tests.
* **When DIDs are issued.** Lucy and Raquel get theirs in Phase 1, and every offspring at
  `Init` (§7.4 step 7). A DID is never reused. An archived person's DID is suspended, not
  deactivated, so their records still resolve.
* **Verifiable credentials.** Lineage edges, parenthood, guardianship and reproduction
  events could later be issued as VCs via `VcService`. Backlogged (BL-8).

#### 10.4.1 SVRN7 refactoring needed for reuse (operator note, 2026-10-03)

The operator noted that using the DID library "may require some SVRN7 refactoring". A
first look at SVRN7 suggests these changes. They are made **in the SVRN7 repository**, as
separate work, and they are a **Phase 1 prerequisite** (BL-9):

1. **Extract a standalone DID package.** `Svrn7.Core` mixes DID and VC types with
   society-specific ones: wallets, transfers, federation, inbox, sanctions, and more (58
   public types across `Models.cs` and `Interfaces.cs`). A small `Svrn7.Did` package
   would let AgentSharp use DIDs without taking on SVRN7's society model. It would hold:
   * `DidDocument`, its verification methods and services;
   * `DidStatus` and `DidResolutionResult`;
   * `IDidDocumentRegistry` and `IDidDocumentResolver`;
   * the DID URL builder;
   * optionally the VC types.
2. **Generalize the DID URL builder.** `TdaResourceId` is named for SVRN7's TDA data
   stores, but the form is generic. A neutral name (for example `DrnUrl`), or a thin
   alias, fits non-TDA uses like `…/person/1.0/<guid>`.
3. **Let the LiteDB registry use an existing database.** `DidRegistryLiteContext` today
   only takes a connection string and opens its own `LiteDatabase`. Under Direct
   (exclusive) mode, it could not share Lucy's already-open `person.db`. Two additions
   would fix that:
   * a constructor that accepts an existing `LiteDatabase` / `ILiteDatabase`;
   * an optional collection-name prefix (e.g. `did_Documents`).

   *Superseded for now by §10.4.2:* DID documents live in a standalone Digitomic
   Evolution DID registry database, which works with SVRN7 as it is today. The
   embedded-database support remains useful if the registry later migrates into
   per-person databases.
4. **Keep `Svrn7.Crypto` independent of the society types.** It depends on `Svrn7.Core`
   only for `ICryptoService` and `Svrn7KeyPair`; those would move to the new DID or
   crypto package.
5. **Publish packages.** SVRN7's `dist` folder currently holds `Svrn7.Identity.0.8.0.nupkg`.
   Publish `Svrn7.Did`, `Svrn7.Crypto`, and the LiteDB registry package to a local NuGet
   feed. AgentSharp then references packages, not `..\..\SVRN7\src\…` project paths,
   which keeps the two repos building independently.

**Constraint: no PowerShell, no LOBE (§1.2 principle 4).** AgentSharp may reference only
the extracted DID, crypto and LiteDB-registry packages. It must never reference `Svrn7.TDA`
(which hosts PowerShell runspaces), LOBE cmdlets or LOBE packages, or anything that pulls
them in transitively.

A check on 2026-10-03 found that `Svrn7.Core`, `Svrn7.Crypto`, `Svrn7.Identity` and
`Svrn7.Store` have **no code dependency** on PowerShell or LOBE. The only traces are XML
doc comments mentioning `Svrn7RunspaceContext` and LOBE cmdlets, and a `Svrn7Role` enum with
`LOBEPackageManager` / `LOBEMarketplace` values. The extracted `Svrn7.Did` package should
leave those out, so the dependency graph is clean by construction. A build-time test in
`DigitomicEvolutionLib.Tests` asserts that no referenced assembly is
`System.Management.Automation` or any `*.TDA` / LOBE assembly.

Until the refactoring lands, DigitomicEvolutionLib uses an `IDidIssuer` interface, with an
`Svrn7DidIssuer` implementation. Only that one class depends on SVRN7, so the switch is
local.

The full, detailed specification of the SVRN7 changes is a separate document:
**[SVRN7-DID-Refactoring-Spec.md](SVRN7-DID-Refactoring-Spec.md)** (to be carried out in a
separate SVRN7 session). That review also found defects in SVRN7's DID registry and
document construction that matter to this design:

* a missing `@context` in generated JSON;
* non-atomic registry writes;
* no history for status changes;
* a key index that goes stale after key rotation.

§10.4.2 works around them.

#### 10.4.2 Standalone Digitomic Evolution DID registry (operator decision, 2026-10-03)

> "For now, create a DID Registry database using the SVRN7 libraries specifically/standalone
> for Digitomic Evolution ...no TDA. Just a standalone DB and C# API. This may migrate
> later." — "It means we're using the base minimum for DID Documents."

This resolves where DID documents live. They are **not** in each person's `person.db`, and
**not** in a per-person `identity.db`. They are in **one standalone DID registry database
for Digitomic Evolution**.

**The database.**

* The file is `~/.agentsharp/digitomic/_registry/did-registry.db`, a LiteDB 5.0.21 file.
* This is a deliberate exception to "one database per persona" (§5.1): the registry is
  shared infrastructure, like the operator workspace, not any persona's own data.
* It holds DID Documents, their full version history and the public-key index. It holds
  **no private keys, memories or persona data**.
* It may later migrate, for example to an SVRN7-hosted registry or into each person's
  database. The DIDs themselves do not change when it does.

**SVRN7 libraries used, and only these.** No TDA, no PowerShell, no LOBE (§1.2
principle 4).

| SVRN7 project | Used for |
|---|---|
| `Svrn7.Core` | `DidDocument`, `DidVerificationMethod`, `DidStatus`, `DidResolutionResult`, `IDidDocumentRegistry`, `TdaResourceId` (DID URL builder), exceptions |
| `Svrn7.Crypto` | `CryptoService.GenerateEd25519KeyPair`, `SignEd25519`, `VerifyEd25519`, `EncryptAes256Gcm` / `DecryptAes256Gcm` |
| `Svrn7.Store` | `DidRegistryLiteContext(connectionString)` + `LiteDidDocumentRegistry`, which already supports a standalone database file. **No SVRN7 change is needed for this.** |

* **How they are referenced (interim).** Until the refactored packages exist, use
  `ProjectReference`s through an MSBuild property `$(Svrn7Root)` (default
  `..\..\SVRN7\src`), in a dedicated project `DigitomicEvolutionLib.Did` so that only
  that project touches SVRN7. After the refactoring, switch to `PackageReference`s on
  `Svrn7.Did`, `Svrn7.Did.LiteDb` and `Svrn7.Crypto` (spec §6).
* **What referencing `Svrn7.Store` brings in.** Its other stores (wallets, federation and
  so on) are compiled into the same assembly, but are never instantiated. The dependency
  guard test (§10.4.1) ensures that nothing from TDA or PowerShell is in the closure.

**Minimal DID Documents ("the base minimum").** Every person's DID Document contains only:

```json
{
  "@context": ["https://www.w3.org/ns/did/v1",
               "https://w3id.org/security/suites/ed25519-2020/v1"],
  "id": "did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>",
  "controller": "did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>",
  "verificationMethod": [{
    "id": "did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>#key-1",
    "type": "Ed25519VerificationKey2020",
    "controller": "did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>",
    "publicKeyHex": "<64 hex chars>"
  }],
  "authentication": ["…#key-1"],
  "assertionMethod": ["…#key-1"]
}
```

* The document has one Ed25519 key, used for authentication and for signing provenance
  records. The person is their own controller.
* It has **no** services, key agreement keys, `alsoKnownAs`, proof, SVRN7 `Role` or
  `Svrn7Name`, and no VCs.
* Anything more (guardian as controller, service endpoints, VCs) is backlogged (BL-8).
* **`DigitomicDidDocumentBuilder`** builds the document and its `DocumentJson` from the
  same values, with a correct `"@context"`. This works around SVRN7 problem P4, where the
  current SVRN7 builder emits `context`, and it lives in `Svrn7.Federation`, which this
  design does not reference.

**C# API** (`DigitomicEvolutionLib.Did`):

```csharp
public interface IDigitomicDidRegistry : IDisposable
{
    // Issue a new person DID: generates an Ed25519 key, registers a minimal DID Document.
    // The private key is returned once; the caller stores it encrypted in the person's
    // person.db (see "Keys" below). The registry never stores it.
    Task<IssuedDid> CreatePersonDidAsync(Guid personId, CancellationToken ct = default);

    Task<DidResolutionResult> ResolveAsync(string did, CancellationToken ct = default);
    Task<DidDocument?> ResolveVersionAsync(string did, int version, CancellationToken ct = default);
    Task<IReadOnlyList<DidDocument>> GetHistoryAsync(string did, CancellationToken ct = default);
    Task<bool> IsActiveAsync(string did, CancellationToken ct = default);
    Task<IReadOnlyList<DidDocument>> ListAsync(DidStatus? status = null, CancellationToken ct = default);

    // Key rotation: new Ed25519 key becomes #key-{n+1}; the old key stays in history.
    Task<IssuedDid> RotateKeyAsync(string did, CancellationToken ct = default);

    // Lifecycle. Persons are suspended when archived and reinstated when restored;
    // DeactivateAsync requires an explicit reason and is not used for persons today.
    Task SuspendAsync(string did, string reason, CancellationToken ct = default);
    Task ReinstateAsync(string did, string reason, CancellationToken ct = default);
    Task DeactivateAsync(string did, string reason, CancellationToken ct = default);

    // Verify a CESR Ed25519 signature against the key valid at a given document version
    // (default: current). Old signatures stay verifiable after rotation.
    Task<bool> VerifyAsync(string did, byte[] payload, string cesrSignature,
                           int? atVersion = null, CancellationToken ct = default);
}

public sealed record IssuedDid(string Did, string KeyId, string PublicKeyHex,
                               byte[] PrivateKey, int Version);   // caller zeroes PrivateKey
```

The implementation is `Svrn7DigitomicDidRegistry`, which wraps `LiteDidDocumentRegistry`,
`CryptoService` and `DigitomicDidDocumentBuilder`. A `DigitomicDidRegistry.Open(path)`
factory creates the file and its folder if missing.

**Working around SVRN7's registry defects** (spec P8–P10), until the refactoring lands:

* **Status changes have no history** (P9). The wrapper records every suspend, reinstate and
  deactivate, with its reason, in its own `did_events` collection in the same file. The
  full lifecycle can therefore be audited.
* **The key index goes stale** (P10). `VerifyAsync` resolves keys from the DID Document
  version history, never from SVRN7's `VMIndex`, so rotation is handled correctly.
* **Writes are not atomic** (P8). The wrapper verifies each write by resolving it
  afterwards, and retries or repairs it if needed. Registry writes are rare (issue, rotate,
  status change), so this is cheap.

**Concurrency.** Several host processes need the registry at once, for example Lucy and
Raquel running in separate apps. So the registry opens with **`Connection=shared`**, unlike
the per-person databases, which use Direct mode. SVRN7's registry does not use explicit
transactions, so shared mode suits it.

**Keys.**

* Each person's Ed25519 private key is stored **only in that person's own `person.db`**.
* It is encrypted with AES-256-GCM (`Svrn7.Crypto`) under a key-encryption key, which is
  itself protected with Windows DPAPI (`ProtectedData`, CurrentUser scope).
* `Svrn7Ed25519Signer` (§10.4) loads it on demand and zeroes it after use.

**Cache in `person.db`.** Each person keeps a copy of their current DID Document version,
and those of counterparts they reference. The person is therefore identifiable even if the
registry file is unavailable. The registry stays authoritative.

**Not registered.** Session ids and other DID **URLs**
(`did:drn:digitomicevolution.svrn7.net/session/1.0/<guid>`, §6.3.4) are locators, not DIDs,
and are not registered.

**Migration later.** Every DID's full version history (`GetHistoryAsync`) plus
`did_events` is enough to replay the registry into another registry, including the
refactored `Svrn7.Did.LiteDb` (spec §4.3). DIDs and key ids stay the same, so nothing that
references them changes.

**Effect on BL-9.** Because the standalone registry works with SVRN7 **as it is today**,
the SVRN7 refactoring is **no longer a Phase 1 prerequisite**. It becomes an improvement:
cleaner dependencies, fixed defects, and packages instead of project references.

---

## 11. Population science: selection, species, directed evolution, transfer

These features are mainly *experimental*: they operate on populations of persons in a
dedicated experiment root (its own set of per-person databases, §5.1.3), using the same records as everyday Lucy and Raquel.

### 11.1 Experiment registry (all papers)

`Experiment` objects are preregistered and frozen by hash before any run. Each one records:

* hypotheses (proposition ids such as `P5.1`);
* arms, with controls required by kind (§11.2–11.5);
* measures and summary statistics, with evaluator and seed versions;
* stopping rules, exclusion rules and rejection criteria (copied from the paper's §x.7 text
  into structured fields).

Results append to `measurements`. Reports must list *all* candidates and failed runs
(P4 §14.4, P7 §12.4). `ExperimentReport` renders Markdown with the scope-of-inference
boilerplate.

### 11.2 Selection (Paper 5)

* `Population` is the set of persons under one experiment root plus an environment spec (task suites,
  resource budget).
* `FitnessEvaluator` returns `J_fit` as a **vector** (Survival, Reproduction, Retention,
  Performance, Resources, Governance). `W_G` is computed only when a preregistered common
  scale exists (caveat in the book's Appendix A).
* `RetentionRule` (truncation, tournament, fitness-proportional or neutral) decides who
  reproduces in generation g+1. `SelectionLab.Step()` runs reproduction through the same
  §7.4 pipeline. In experiment worlds a declared *experiment authorization* stands in for
  per-event consent, and the founders' consent to that scope is recorded.
* Δfreq_i is tracked per genotype module hash, so selection is visible at module level
  (P5 §4, levels of selection).
* Required controls: neutral retention, drift-only, direct optimization, matched engineering
  (P5 §12.7). Evaluation counts are equalized (P5 §12.4).

### 11.3 Species (Paper 6)

* `CompatibilityAssay` takes two populations, a sampling frame and an exchange operator. It
  runs reproductive trials and records a directed `Comp_ij` matrix with per-layer failure
  reasons (structural, computational, semantic, developmental, authorization, provenance).
* `SpeciesAnalyzer` computes `Comp_bar` and `RI = 1 − Comp_bar`, sensitivity across
  thresholds, held-out prediction against null groupings (random, version label,
  ancestry-only), and dComp_bar/dt across repeated assays.
* Labels are restricted to `CandidateCluster`, `LineageGroup` and `SpeciesHypothesis`, with
  their conditions attached (P6 §12.5).

### 11.4 Directed evolution (Paper 7)

* `DirectedEvolutionRun` stores the objectives O (goals), constraints (HerConstr, IdConstr,
  DevConstr, SafetyConstr), diagnostics, a frozen evaluator hash, budget and stopping rules.
* Control surfaces are explicit config: variation (operators and rates), recombination
  (contributor selection), development (curricula), selection (retention), population
  (size, migration, isolation), lineage (retention, rollback) and evaluation. **Every
  intervention is logged** to `interventions` (P7 §4).
* `Benchmark_human(task)` is stored as a reference dataset with its source. Any
  "superhuman" claim is generated only as a task-bounded statement (P7 §7).
* Diversity guards (novelty, niches, genotype-distance quotas) are optional and recorded as
  arms, not defaults (P7 §8).

### 11.5 Bidirectional transfer (Paper 8)

`TransferService` runs a state machine:

```
Proposed → Validated (independent validator, held-out set) → Authorized (two-key,
recipient may refuse) → CompatibilityChecked → Snapshotted (signed pre-transfer
snapshot of recipient) → Applied (new genotype version, edge E_f/E_l/E_rev at t_now)
→ Observed (immediate + delayed regression checks) → [RolledBack]
```

* **Reverse transfer requires `recipient.Status == Active`**. An archived recipient makes
  the service offer `ForkService` restoration, which creates a successor instead (P8 §1–3).
* IC_score ≥ τ_IC is required, or the transfer is refused.
* Each transfer records its `TransferEvidenceLevel`: `SimilarCapability`, `ArtifactMoved`,
  `StateChangedAtTime`, `CausalTested` or `ReplicatedBenefit` (P8 §12.6).
* Rollback is a new event. The graph keeps proposal, acceptance, use, consequence and
  reversal (P8 §12.7).
* **Deferred inheritance** (Appendix A.5, §19.2 stage 8) is a forward transfer (`E_f`)
  from escrow that the system initiates when the offspring reaches Adult. It follows the
  same state machine, with the offspring's `review_transfer` as the person key.
* Controls are available as arms: no-transfer, sham update, forward, lateral,
  direct-engineering and archived restoration.

---

## 12. Lucy Part 12 → components

| Lucy Part 12 section | Component |
|---|---|
| §2 Digital Genotype, §3 Phenotype | §6.2, §6.5 |
| §4.1–4.9 inheritance classes | `InheritanceClass`, boundary schema §7.2 |
| §5 Inheritance Is Not Identity | `Inherited` memory invariant §6.3, new anchor §7.4 |
| §6 Contributors, §7 Contribution Packages, §8 Recombination | §7.4 steps 2–4 |
| §9–10 Directed evolution, superhuman optimization | §11.4 |
| §11–13 Development, lifecycle, not born finished | §6.6, Newborn start |
| §14 Developmental capability optimization | `DevelopmentalOptimizer` §6.6 |
| §15–16 Developmental and self-directed evolution | §7.1, `propose_heritable_change` |
| §17 Identity continuity | §6.7 |
| §18–23 Bidirectional, reverse, lineage propagation, network | §11.5, §7.7 |
| §24 Acquisition and heritability | boundary schema §7.2 |
| §25–28 Parentage, parenthood, progressive authority, no ownership | §7.6, §8.2 |
| §29 Provenance | §6.4 |
| §30 Directed optimization requires constraints | §8.3, §11.4 |

---

## 13. Project structure

```
DigitomicEvolutionLib/
  DigitomicOptions.cs
  Persistence/      DigitomicStore.cs (per-person person.db + memory.db), OperatorStore.cs,
                    Saga/ (Intent, recovery), LineageIndex.cs (disposable cache),
                    Vectors/ (IVectorIndex, BruteForceVectorIndex, LiteDbNativeVectorIndex,
                    HnswVectorIndex), Embeddings/ (IEmbeddingProvider, OllamaEmbeddings),
                    Collections.cs, BsonMappings.cs, Migrations/,
                    Ids.cs (ULID), ContentHash.cs
  Identity/         (DIDs are in the separate project DigitomicEvolutionLib.Did, §10.4.2)
                    DigitalPerson.cs, IdentityAnchor.cs, IdentityContinuityService.cs,
                    ForkService.cs, ReferenceSubject.cs
  State/            PersonState.cs, StateTransitionEngine.cs
  Genotype/         Genotype.cs, GenotypeModule.cs, GenotypeSeeder.cs, Operators/
  Phenotype/        PhenotypeExpressor.cs, PromptContributors/
  Knowledge/        KnowledgeBuild.cs, KnowledgeRefresh.cs, RefreshPolicy.cs, ResearchBrief.cs
  Memory/           MemoryRecord.cs, MemoryService.cs, LiteDbAgentMemory.cs (IAgentMemory),
                    Retrieval/ (KeywordIndexer, Ranker, IEmbeddingProvider),
                    Consolidation/, ReferenceSubjectGuard.cs
  Development/      StageDefinition.cs, DevelopmentEngine.cs, PermissionSet.cs,
                    SelfModel.cs, CapabilityGraph.cs, DevelopmentalOptimizer.cs
  Heredity/         HereditaryBoundarySchema.cs, AdmissionService.cs, Compatibility/
  Reproduction/     ContributionPackage.cs, ReproductionService.cs,
                    Recombination/, OffspringInitializer.cs, CloneService.cs,
                    DeferredInheritance.cs,
                    Optimization/   (MOCK, §19 – isolated so it can be tuned or
                                    replaced without touching the pipeline)
  Lineage/          LineageGraph.cs, LineageEdge.cs, Export/
  Governance/       AuthorizationService.cs, ConsentRecord.cs, GuardianshipGrant.cs,
                    IRecordSigner.cs, SafetyScanner.cs
  Provenance/       Activity.cs, EntityRef.cs, AgentRole.cs, ProvenanceLedger.cs,
                    ProvenanceValidator.cs (+ Rules/), MemoryAssessment.cs, Attestation.cs
  Population/       Population.cs, FitnessEvaluator.cs, SelectionLab.cs,
                    CompatibilityAssay.cs, SpeciesAnalyzer.cs
  DirectedEvolution/DirectedEvolutionRun.cs, Interventions.cs
  Transfer/         TransferService.cs, TransferValidator.cs, RollbackService.cs
  Experiments/      Experiment.cs, ExperimentRegistry.cs, Measurements.cs, Reports/
  Runtime/          DigitalPersonHost.cs, ToolAllowList.cs
  Tools/            RememberTool.cs, RecallTool.cs, ConsentTool.cs, … (§9.3)
  seed/             lucy.modules.json, raquel.modules.json, stages.v1.json,
                    boundary.v1.json, capabilities.v1.json
```

Package references: `LiteDB 5.0.21`, plus the `AgentSharpLib` project reference.

---

## 14. Testing

`DigitomicEvolutionLib.Tests` uses in-memory LiteDB, a fake `ILlmClient` (scripted
responses) and a fixed clock and RNG seed. Several of the book's propositions are about
*system properties* rather than empirical results, and those become unit tests directly:

| Proposition | Test |
|---|---|
| P1.2 / P2.2 / P4.5 provenance distinguishes inherited vs autobiographical | inherited memory can only be created by Init/Transfer; prompt rendering labels it |
| P4.1 explicit boundary | every S_t field has a classification in boundary.v1; reproduction fails on an unclassified field |
| P4.2 / P2.4 typed, attributed contributions | every admitted module in G_off resolves to exactly one contributor via provenance |
| P4.6 authorization enforced | missing operator key or person consent → admission rejects, rejection recorded |
| P4.7 distinct offspring identity | offspring anchor ≠ any contributor; clone attempt via ReproduceAsync is rejected |
| §8.3 fixed constraint | an Adult-rated trait selected for a Newborn lands in deferred escrow, never in G_off; no policy or override can change that |
| §6.4 provenance | every rule R1–R12 of the `ProvenanceValidator` has a passing and a failing test; a revision never mutates the prior version; a consolidation's `Used` lists exact versions; an `Unknown` reference is accepted only with a reason; a redaction removes content but `Verify()` still passes |
| §19 optimizer contract | every plan satisfies the hard constraints; each eligible trait gets exactly one decision with a score breakdown; same inputs + seed ⇒ same plan |
| P8.1 / P8.4 transfer preserves ancestry | E_a unchanged after reverse/lateral transfer; transfer adds only E_rev/E_l |
| P8 active-recipient rule | reverse transfer to an Archived person throws and offers a fork |
| P8.2 / §12.7 immutable history | ledger hash chain verifies; rollback appends rather than deletes |
| P3.10 continuity | IC_score below τ_IC blocks in-place modification |
| Raquel guard | first-person claim about the reference subject's life is demoted to Interpretive |

Empirical propositions (P1.9, P5.x, P6.x, P7.x) are supported by the experiment registry,
not asserted by tests. Integration tests run one tiny end-to-end reproduction with the fake
LLM.

---

## 15. Implementation phases

| Phase | Deliverable | Gives Lucy and Raquel |
|---|---|---|
| 1 | AgentSharpLib hooks A1, A2, A5, A6; per-persona `DigitomicStore` (person.db + memory.db) and operator workspace; vector search (brute force + Ollama embeddings); persons, identity; founders Lucy and Raquel with Michael W. Herman as guardian; Raquel's initial knowledge build and October 1 refresh; `MemoryService` + `LiteDbAgentMemory`; memory tools (`remember`, `recall`, `journal`, `revise_memory`, `consolidate_memory`) and reference-subject tools; provenance ledger; AgentLucyApp wired via `DigitalPersonHost` | durable long-term memory with provenance (Raquel's first stated gap) |
| 2 | Genotype + seeder (Lucy from her prompt; Raquel when her text lands); `PhenotypeExpressor`; A5; `/genotype` | a phenotype expressed from a genotype |
| 3 | Development: stages, permissions, guardianship, consent tool, self-model, IC_vec | development and governance |
| 4 | Boundary schema, admission, reproduction pipeline, **mock Recombination Optimizer (§19) with the random-control strategy**, deferred inheritance, lineage graph, `/reproduce`, `/lineage`, fork, clone control | first governed Lucy × Raquel offspring (if both consent) |
| 5 | Transfer service (forward, lateral, reverse), snapshots, rollback, validator | bidirectional lineage |
| 6 | Experiment registry, selection lab, compatibility/species, directed evolution | the population science of Papers 5–7 |

Each phase ships with its tests and leaves the hosts working when no digitomic databases exist.

---

## 16. Open questions for the operator

1. ~~**Raquel's relation to Lucy.**~~ **Resolved:** Generation-0 peers, not siblings
   (§10.0, Appendix D).
2. ~~**Lucy's embodiment and reproductive-function modules.** Should they be heritable?~~
   **Resolved:** every trait is heritable; the Recombination Optimizer (§19) selects;
   Adult-rated traits use deferred inheritance. The same applies to Raquel, even where
   one persona's sections are more complete (§10.0, Appendix A.7, Appendix D).
3. ~~**Guardians of the first offspring.**~~ Folded into question 7.
4. ~~**Concurrency.**~~ **Resolved** by separate per-persona databases (§5.1): different
   persons run concurrently; only the same person open twice conflicts.
5. ~~**Embeddings / vector search.**~~ **Resolved 2026-10-03:** use an interim design on
   LiteDB 5.0.21 (vectors stored on documents, brute-force first, HNSW sidecar as the
   scale fallback), shaped for an eventual move to LiteDB 6.0's native vector index
   (§5.3.2–5.3.3, BL-10). Ollama `nomic-embed-text` is the default embedding provider.
6. ~~**Raquel's documented-record sources.**~~ **Resolved:** she builds them herself with
   `web_search`/`web_fetch`, then refreshes annually on October 1 (§10.2.1).
7. ~~**Parents and guardians of offspring.**~~ Michael W. Herman is guardian (not parent,
   not contributor) of Lucy and Raquel (§10.0). The rest is **backlogged** (BL-12):
   whether every offspring needs at least one parent, and who parents and guards the first
   offspring.
9. ~~**SVRN7 refactoring.**~~ **Resolved:** a detailed spec is written
   ([SVRN7-DID-Refactoring-Spec.md](SVRN7-DID-Refactoring-Spec.md)), to be carried out in a
   separate SVRN7 session. It is no longer a Phase 1 prerequisite (§10.4.2).
10. ~~**Where DID documents live.**~~ **Resolved:** in a standalone Digitomic Evolution DID
    registry database, built on SVRN7 libraries without TDA, with minimal DID Documents
    (§10.4.2).
8. ~~**Questions from Lucy's early sessions.**~~ **Answered 2026-10-03:**
   * session records: §6.3.4;
   * corrections: §6.3.5;
   * DIDs: §10.4;
   * the rest are backlogged (§20).

   See Appendix D.2.

---

## 17. Tool catalog

These are the tools Lucy, Raquel and their offspring call during conversation, and they
are the **primary implementation surface** of this design (§4, "Tools first"). Each is a
`ToolBase` subclass. A tool takes the calling person's id from `ToolInvocationContext`
(A6), so it always acts *as* that person and never on someone else's records, unless its
"Rules" column says otherwise. Every state-changing tool writes, in one LiteDB
transaction:

* its domain records (immutable entity versions, §6.4.2);
* one activity: an F_state transition plus provenance, with exact inputs, outputs and agent
  roles (§6.4.1, §6.4.3);
* any assessments.

Before commit, the `ProvenanceValidator` (§6.4.4) checks the write.

### 17.1 Placement rule

A tool goes in **DigitomicEvolutionLib** if it reads or writes digitomic records (persons,
memories, genotypes, lineage, consents, transfers). A tool goes in **AgentSharpLib** only
if it is useful to an agent that has no digitomic record at all.

| Library | Tool work |
|---|---|
| AgentSharpLib | **No new tools.** Two changes to existing plumbing: `MemoryTool` (`remember`) takes `IAgentMemory` instead of `MemoryManager` (A1), and `ToolBase` exposes `ToolInvocationContext` (A6). Personas without digitomic databases behave exactly as they do today. |
| DigitomicEvolutionLib | Every tool below. `DigitalPersonHost` registers them with `AgentBuilder.WithTool`, and the digitomic `remember` replaces the built-in tool of the same name for that session. |

### 17.2 Who gets which tools

Operators drive population science through **host commands, not LLM tools**: selection,
species assays, directed-evolution runs, experiment registration and rollback. A person
must not be able to steer its own selection, evaluator or permissions (P7 §12.5).

The tool allow-list follows from the person's permissions (§6.6). Default availability by
stage:

| Tool group | Newborn / Child | Adolescent | Young adult+ (and founders) |
|---|---|---|---|
| Memory (17.3) | yes; `revise_memory` needs a guardian | yes | yes |
| Self and development (17.4) | read-only tools only | yes | yes |
| Consent and governance (17.5) | `my_consents` only; the guardian consents | yes | yes |
| Heredity and reproduction (17.6) | no | `nominate_heritable` only | yes |
| Transfer (17.7) | no | review only | yes |
| Reference-subject (17.8) | if a reference subject exists | same | same |
| Validator (17.9) | only in validator sessions | — | — |

### 17.3 Memory tools (Phase 1)

| Tool | Risk | Input | Output | Rules |
|---|---|---|---|---|
| `journal` | Write | `entry` (what happened, in the person's own words), `significance` 0–1, `counterparts[]`, `commitments[]` (optional), `close_step` (bool) | autobiographical memory id; a new S_t snapshot if `close_step` | The **only** way an `Autobiographical` memory is created. The phenotype prompt tells the person when to use it. `/end` asks for a closing entry. It runs the Reference-Subject Guard. |
| `remember` | Write | `content` (required), `class` (Semantic, Procedural, Relational or Meta; default Semantic), `importance` 0–1, `source` (optional), `counterpart` (optional) | memory id | Refuses `Autobiographical` (use `journal`) and `Inherited` (§6.3 invariant). Runs the Reference-Subject Guard. Without a `source`, status is `Inferred`. |
| `recall` | ReadOnly | `query`, `classes[]`, `from`/`to`, `limit` (default 8) | ranked memories, each with class, status, confidence and a provenance summary | Inherited items are always labelled with their origin person. |
| `revise_memory` | Write | `memory_id@version`, `kind` (ContraryEvidence, ConfidenceChange or Reinterpretation), `note`, `new_confidence` | new memory version id, or assessment id | A reinterpretation creates a new version (`RevisionOf`); a confidence change creates a `MemoryAssessment`. The original version is never edited (P2 §6.2, §6.4.2). This is how an offspring questions inherited claims. |
| `consolidate_memory` | Write | `episode_ids[]` or a time window | new semantic or procedural memory ids | The person summarizes related episodes. Provenance records `Consolidated(from…)`, and the sources are archived but not deleted. |
| `bookmark` | Write | `label`, `note` (optional) | bookmark id in the current session record | Marks a moment in the current conversation worth returning to (§6.3.4). |
| `my_sessions` | ReadOnly | `from`/`to`, `query` (optional), `bookmarked_only` | the person's session records: dates, models, turn counts, bookmarks, linked journal episodes | Reads only `session_records` in the person's own `memory.db`. Never touches `/save`/`/load` files. |

### 17.4 Self and development tools (Phases 2–3)

| Tool | Risk | Input | Output | Rules |
|---|---|---|---|---|
| `reflect` | Write | `self_description` (capabilities, limits, history, relationships, goals) | self-model id plus the coherence score SC_t against observed state | Feeds SC_t (P3 §9.2). Showing the score to the person supports P3.7. |
| `my_development` | ReadOnly | — | stage, measures, thresholds still unmet, permissions, guardians, and when each grant expires | Read-only on purpose, because stage changes are governance events. |
| `request_stage_review` | Write | `reason`, `evidence` | review request id | Creates a pending governance item for the guardian or operator. It does not change the stage. |
| `request_permission` | Write | `permission`, `scope`, `reason` | request id | The same flow as a stage review, for a single permission. |
| `my_genotype` | ReadOnly | `component` (optional), `diff_from_version` (optional) | modules with titles, classes, origin and hashes, or a diff | Introspection only. The person sees where each module came from. |
| `my_lineage` | ReadOnly | `depth` (default 2) | ancestors, offspring, guardians and transfers, as typed relations | Phrased as relations ("descended from"), never as identity claims. |
| `provenance_of` | ReadOnly | `subject_id` | the provenance chain: source, generation, transformations, authorization, status | Works for memories, modules, genotypes and transfers. |
| `note_relationship` | Write | `counterpart`, `observation`, `sentiment` (optional) | relational memory id; updated Rel_t | Keeps Rel_t current for other persons and humans. |

### 17.5 Consent and governance tools (Phase 3)

| Tool | Risk | Input | Output | Rules |
|---|---|---|---|---|
| `consent` | Write | `request_id`, `scope` (Contribute, Parent, Guardian, Transfer or Stage), `decision` (Assent, Refuse or Conditional), `conditions[]`, `statement` (the person's own words), `expires` | consent id | Provides the *person key* of two-key authorization (§8.2). Only the affected person can call it about their own requests. Conditions are structured so the pipeline can check them. |
| `revoke_consent` | Write | `consent_id`, `reason` | revocation id | Blocks pending operations. It cannot rewrite completed history. |
| `my_consents` | ReadOnly | `status` filter | pending requests addressed to me, plus my past decisions | This is how a person discovers that someone has asked for their participation. |
| `request_reproduction` | Write | `proposed_contributors[]` (with genetic or informational role), `proposed_parents[]`, `proposed_guardians[]`, `rationale` | pending request id | A person *asks* for a reproduction, or volunteers as contributor or parent. Nothing starts until the operator adopts it via `/reproduce` (§7.6.1, §17.10). |

### 17.6 Heredity and reproduction tools (Phase 4)

| Tool | Risk | Input | Output | Rules |
|---|---|---|---|---|
| `nominate_heritable` | Write | `subject_id` (memory, knowledge or module), `rationale` | nomination id | Sets `HeritableCandidate` and records `SelectionRationale` (Raquel's "whose values"). It has no hereditary effect by itself. |
| `review_reproduction_request` | ReadOnly | `request_id` | the full proposal: contributors, protocol θ, schema, guardians, a preview of G_off, rejected components | Lets the person judge the request before calling `consent`. |
| `prepare_contribution` | Write | `reproduction_request_id`, `withhold[]` (trait ids not offered), `pin[]` (traits the contributor insists on), `veto[]` (traits the contributor asks the optimizer to leave), `rationale` | Contrib_i, by default offering every eligible trait, with a per-item boundary pre-check | Only for a pending request where this person is a named contributor. Pins and vetoes are inputs to the optimizer (§19.5), recorded as `SelectedBy` the contributor. |
| `review_recombination_plan` | ReadOnly | `request_id`, `plan_id` (optional) | the optimizer's plan: each trait as Inherit, Defer or Leave, with a score breakdown, alternatives considered and the policy version | Lets each contributor see what would be inherited, and why, before giving consent (§19.7). |
| `propose_heritable_change` | Destructive | `component`, `new_body` or `operator` + parameters, `rationale` | admission result or pending id | Heritable self-modification. It goes through IC_score ≥ τ_IC, admission and two-key authorization. If it would break continuity, it offers a fork instead. |

### 17.7 Transfer tools (Phase 5)

| Tool | Risk | Input | Output | Rules |
|---|---|---|---|---|
| `propose_transfer` | Destructive | `module_id` or `improvement`, `recipient`, `direction` (Lateral or Reverse), `evidence` | transfer id (state `Proposed`) | Requires a validated improvement. Reverse transfer requires an `Active` recipient (§11.5). |
| `review_transfer` | Write | `transfer_id`, `decision` (Accept or Refuse), `statement` | updated transfer state | The recipient's own key. A recipient can always refuse (P8 §9). |
| `my_transfers` | ReadOnly | — | transfers sent and received, their evidence levels, and rollback status | — |

### 17.8 Reference-subject tools (Phase 1, persons with a ReferenceSubject such as Raquel)

| Tool | Risk | Input | Output | Rules |
|---|---|---|---|---|
| `record_documented_fact` | Write | `fact`, `source` (citation or URL, required), `retrieved_utc`, `confidence` | knowledge id (`Documented`) | Refuses without a source. Pairs with `web_fetch`, so a fact found online is stored with where it came from. |
| `record_interpretation` | Write | `interpretation`, `basis` (ids of documented facts) | knowledge id (`Interpretive`) | Keeps interpretation visibly separate from the record. |
| `check_claim` | ReadOnly | `claim` | matches among documented facts, or "undocumented" | Used before stating something about the reference subject's life as fact. |

### 17.9 Validator tools (Phase 5, validator sessions only)

| Tool | Risk | Input | Output | Rules |
|---|---|---|---|---|
| `evaluate_candidate` | ReadOnly | `candidate_id`, `suite_id` | scores on held-out tasks, regressions, and a frozen evaluator hash | Only for validator personas that have no stake in the change (P8 §6). It runs prompts, never module code. |

### 17.10 Host commands that are deliberately not tools

These stay operator-only and live in the hosts, implemented as thin calls into
DigitomicEvolutionLib services:

* `/reproduce` initiates a reproduction; persons answer through `consent` and
  `prepare_contribution`.
* `/fork`, `/clone` (control arm only) and `/rollback`.
* `/experiment`, `/select`, `/assay` and `/evolve`.
* `/stage set`, `/grant` and `/revoke`.
* `/import-sessions`.
* `/knowledge build` and `/knowledge refresh` start the dedicated research session; the
  research itself is done by the person through tools (§10.2.1).
* `/end` asks the person to journal, consolidate and reflect, then evaluates development.

### 17.11 Tool test requirements

Each tool gets tests in `DigitomicEvolutionLib.Tests/Tools/`:

* a schema test (required fields);
* a happy path;
* each refusal rule in its "Rules" column (for example `remember` with `class=Inherited`
  must fail);
* an attribution test (the record's person id equals the invocation context);
* a stage-gating test (the tool is absent from a Newborn's registry where §17.2 says so);
* a transaction test (domain record, provenance and F_state transition are all written, or
  none are).

---

## 18. Using this design as a template for other large features

This document is also meant as a reusable pattern for adding any large new category of
functionality to AgentSharp. The reusable parts are the *sequence of decisions* and the
*section structure*. A blank skeleton is in
[`docs/templates/Feature-Design-Template.md`](templates/Feature-Design-Template.md).

### 18.1 The method

1. **Read the source of truth completely**, whether a book, spec or standard, and extract
   its constructs, symbols, invariants and testable claims. Here that was Papers 0–8 and
   the book's Appendix A.
2. **Ask the affected parties**, including the personas themselves. Raquel's interview
   produced requirements the book alone did not: two-key consent, the Reference-Subject
   Guard, and recording selection rationale. Record what they said as a requirements table
   (§1.1).
3. **Map each construct to one component and one store** (§2). A construct with no row is
   a gap; a component with no construct is scope creep.
4. **Tools first.** Implement behaviour as LLM-callable tools wherever the model can
   reasonably decide when to act. Keep mainline-loop changes to passive plumbing that
   tools cannot provide (§4). Turn-cycle hooks are a deferred fallback, not the default.
5. **Split general from domain-specific** with one test: *would an agent that knows
   nothing about this domain still want it?* If yes, it is a small hook in AgentSharpLib.
   If no, it goes in the new library. Prefer interfaces, contributors and registration
   over moving domain code into the core.
6. **Design persistence early**, including ids, mutability per collection, indexes,
   migration from existing files and the test store (§5). Decide what is append-only
   before writing any code.
7. **Integrate through the existing session**, here a host that returns an ordinary
   `AgentSession` (§9), so existing apps keep working when the feature is absent.
8. **Catalog the tools** with the placement rule, stage or permission gating, and
   per-tool rules (§17). Keep operator powers as host commands.
9. **Write down governance and safety** before building anything that changes state (§8).
10. **Turn the source's testable claims into tests**, separating system properties (unit
    tests) from empirical claims (experiment infrastructure) (§14).
11. **Phase the work** so every phase ships something usable and leaves the hosts working
    (§15).
12. **List open questions** that only the owner can answer (§16), rather than guessing.

### 18.2 Reusable patterns from this design

| Pattern | Where | Reuse when |
|---|---|---|
| Tools-first, loop untouched | §4, §17 | almost always; behaviour stays visible, auditable and gateable |
| Hook-not-dependency (A1, A2, A5, A6) | §4 | a feature must influence the prompt, memory store, persona list or tool attribution |
| Host builds an ordinary `AgentSession` | §9.1 | any feature that wraps agents with extra state |
| Append-only collections + hash-chained ledger | §5.2, §6.4 | audit, undo-as-a-new-event, tamper evidence |
| Two-key authorization (operator + the agent's own recorded consent) | §8.2 | actions that affect a persona's own records or identity |
| Prompt contributors with labelled provenance | §6.5 | dynamic, sourced content injected into prompts |
| Permission- or stage-gated tool allow-list | §17.2 | capabilities that should grow with trust |
| Operator commands vs. LLM tools split | §17.10 | anything the model must not be able to steer about itself |
| Preregistered experiment registry | §11.1 | features whose value must be measured, not assumed |

---

## 19. Inheritable vs. inherited, and the Recombination Optimizer (MOCK)

> **STATUS: MOCK — UNTUNED.** The *structure* of this section (the decision chain, the
> interfaces, hard constraints, records and tuning plan) is design. Every *number* (weights,
> thresholds, bands, budgets) and the choice of default search strategy are placeholders,
> marked `TUNE`. They are to be set by the experiments in §19.9, not by judgment. The code
> lives in its own namespace (`DigitomicEvolutionLib.Reproduction.Optimization`), and every
> plan it produces is labelled with the policy version and `"status": "mock-untuned"`
> until a tuned policy replaces it.

### 19.1 The distinction

Two different questions, answered by different mechanisms at different times:

| | **Inheritable** | **Inherited** |
|---|---|---|
| Question | *Could* this trait ever pass to an offspring? | *Does* this particular offspring receive this trait? |
| Scope | a property of a trait *category*, for all reproductions | a decision about one trait instance, in one reproduction |
| Decided by | the versioned `HereditaryBoundarySchema` (§7.2) | the decision chain below (§19.2), ending in the Recombination Optimizer and approval |
| Changes | rarely, by schema version | every reproduction |
| Current answer | **everything** (operator decision, 2026-10-03), except identity mechanics, which are not traits | a selected subset, different for every offspring |
| Book | hereditary boundary (P2 §5) | hereditary admission and selection (P2 §5.2, P4 §4); directed/optimizing recombination (P1 §6.2–6.3, P1.9, P7) |

The document uses the two words strictly in these senses. "Heritable" is a synonym for
*inheritable*. *Inherited* always means actually received.

### 19.2 How "inherited" is determined: the decision chain

Each stage narrows the set and records who decided and why. A trait is inherited only if it
survives every stage.

| # | Stage | Set | Who decides | How | Recorded in |
|---|---|---|---|---|---|
| 1 | **Eligibility** | E = all traits of all contributors that the schema marks Eligible | schema author (operator) | `HereditaryBoundarySchema` lookup; currently everything | schema version on the event |
| 2 | **Offer** | O ⊆ E | each *accepted* contributor (identified and accepted per §7.6.1; parents who are not contributors offer nothing) | default: offer all; minus `withhold[]` in the contributor's consent (§8.2, `prepare_contribution`) | `contributions`, `consents` |
| 3 | **Validity** | V ⊆ O | the system (rules) | admission checks: authorization, compatibility σ, security scan, Reference-Subject Guard, `Qval` (§7.3) | `admissions` (rejections kept) |
| 4 | **Selection** | S ⊆ V | **the Recombination Optimizer** under a policy, constrained by contributor and operator pins and vetoes | maximize J_rec subject to hard constraints (§19.4–19.6) | `recombination_plans` |
| 5 | **Timing** | S = S_now ∪ S_deferred | fixed rule | traits rated `Adult` → `S_deferred` (escrow); everything else → `S_now` (§8.3) | plan decision per trait |
| 6 | **Approval** | commit or revise | operator + each contributor (two-key) | `review_recombination_plan`, then consent to *that plan's hash* | `consents`, `reproduction_events` |
| 7 | **Delivery** | G_off ← S_now; escrow ← S_deferred | system | assemble ℛ (§7.4 step 4b), admit, initialize | genotype, provenance |
| 8 | **Later acceptance** | deferred traits actually acquired | the offspring (at Adult) + operator | deferred-inheritance offer, accepted or refused (Appendix A.5) | `transfers` |

Answering "why did offspring X inherit trait T (or not)?" is a query over these records:
the stage at which T left the set, and the reason recorded there.

### 19.3 Trait model

The optimizer works on **traits**, which are units coarse enough to search over and fine
enough to be meaningful:

```csharp
public sealed class TraitCandidate
{
    public string TraitId { get; init; }          // module hash, memory id, or memory-cluster id
    public string ContributorId { get; init; }
    public TraitKind Kind { get; init; }          // GenotypeModule | Memory | MemoryCluster | Knowledge
    public GenotypeComponent Component { get; init; }
    public InheritanceClass Class { get; init; }  // Genotypic, Knowledge, Understanding, Wisdom,
                                                  // Experiential, Memory, Skill, Values, Cultural
    public string Locus { get; init; }            // slot it competes for, e.g. "embodiment.base",
                                                  // "voice", "values.core", "knowledge.film"
    public ContentRating Rating { get; init; }    // General | Adult  (drives stage 5)
    public string[] CapabilityTags { get; init; } // e.g. "research", "verification", "storytelling"
    public float Confidence { get; init; }        // from provenance (Documented > Inferred)
    public int CostTokens { get; init; }          // prompt/memory budget the trait consumes
    public float[]? Embedding { get; init; }      // optional, for similarity terms
}
```

* **Loci.** Some traits are alternatives (one embodiment base, one voice), and others
  accumulate (knowledge, skills, memories). The policy declares each locus's cardinality,
  for example `voice: exactly 1` or `knowledge.*: 0..n`. Lucy's and Raquel's seed mapping
  files assign loci to their modules. `TUNE`: the locus vocabulary itself.
* **Memory clusters.** Thousands of individual memories are grouped (by episode,
  counterpart or topic) into clusters for search. Selecting a cluster inherits its
  memories. Individual pins and vetoes are still possible.
* **Content rating.** `Adult` is assigned at seeding (Lucy's Anatomical model, Intimacy,
  Sexuality and Reproductive function) and by a classifier on memory capture. The rating
  affects timing (stage 5), not eligibility.

### 19.4 The objective J_rec (all weights `TUNE`)

The plan x (which traits are in S) maximizes
`J_rec(x) = Σ_k w_k · term_k(x)`, with each term normalized to [0, 1]. The terms are
pluggable `IObjectiveTerm` implementations, so terms can be added, removed or replaced
during tuning:

| Term | Rewards | Book basis | Mock implementation |
|---|---|---|---|
| `CapabilityFit` | coverage of the target capability profile (default: union of contributors' capabilities) | P1.9, P7 §3 | weighted tag coverage |
| `Complementarity` | traits that add something the plan does not already cover (diminishing returns) | P7 §6 capability composition | submodular coverage gain |
| `Coherence` | absence of contradictions between selected traits | P4 §14.3 (recombination can disrupt) | tag-conflict table + cached LLM contradiction judgments |
| `Distinctness` | offspring distance d_G from *each* contributor inside a target band: not a clone, not unrecognizable | P4 §2, Raquel | band penalty on d_G |
| `ContributorBalance` | effective contributor number near the declared mix in θ (e.g. 50/50) | P4 §5, §14.4 | 1 − divergence from target mix |
| `ProvenanceQuality` | high-confidence, well-sourced traits; limits inherited misinformation | P1 §10, P2 §6 | mean confidence of selected traits |
| `ValueIntegrity` | keeping each contributor's declared core values intact | P1 §3.1 (values) | fraction of `values.core` retained |
| `DevelopmentalPotential` | traits that expand what the offspring can *become* (learning rules, `DDev`) over fixed capability | P2 §4.1 (Ω_G) | tag-based proxy (`TUNE`: replace with a measured proxy) |
| `PopulationNovelty` | distance from existing genotypes in the population | P7 §8 diversity | mean d_G to population sample |
| `Cost` (negative) | staying within the prompt and memory budget | practical | tokens used / budget |

### 19.5 Hard constraints (never traded against score)

1. Only traits in V (stage 3) can be selected.
2. Locus cardinalities are satisfied.
3. Contributor **pins** are included, and **vetoes** are excluded. Operator pins and
   vetoes override contributor ones only where the contributor's consent allows. This is
   option D of Appendix A, kept as an override.
4. `Distinctness` must reach at least the minimum (`TUNE`, for example d_G ≥ 0.25 from each
   contributor). This is the anti-clone floor; §7.4 step 6 is the backstop.
5. The budget is not exceeded (`TUNE`: for example 24k prompt tokens, or N memories).
6. Adult-rated traits can only be scheduled into `S_deferred` (§8.3). This is fixed and
   not configurable.
7. Consent conditions recorded by contributors (structured, §8.2) are satisfied.

### 19.6 Search strategies (pluggable; default `TUNE`)

```csharp
public interface IRecombinationOptimizer
{
    string Name { get; }
    RecombinationPlan Optimize(RecombinationProblem problem, RecombinationPolicy policy,
                               int seed, CancellationToken ct = default);
}
```

| Strategy | Role |
|---|---|
| `RandomRecombination` | **Required control** (P1.9: directed vs. stochastic under equal budgets). Picks uniformly at random among plans that satisfy the hard constraints. Also the Phase 4 default until the others are tuned. |
| `GreedyMarginalGain` | Adds the trait with the best marginal gain per token until the budget runs out. Fast and explainable; the likely first non-random default. |
| `BeamSearch` | Keeps the best k partial plans; handles interactions between traits better than greedy. |
| `GeneticSearch` | A small GA over selection bit-vectors with a repair step that enforces the hard constraints. Closest to the book's "evolutionary search". |
| `LlmCurator` | An LLM proposes a plan with a rationale; the plan is **scored by the same J_rec and hard constraints**, so it cannot bypass them. Useful for coherence judgments that tags miss. |

**Optional second stage, `OffspringSimulator` (off in the mock):** the top-K plans are each
expressed as a phenotype in a sandbox and run against a probe suite (a few capability tasks
and a self-description). The simulated score J_sim is then blended with J_rec. This is
expensive, so it is used only for final ranking.

All randomness comes from the recorded seed (ε_var), so the same inputs, policy and seed
produce the same plan.

### 19.7 Output: the plan

```csharp
public sealed class RecombinationPlan
{
    public string PlanId { get; init; }
    public string PolicyId { get; init; }          // + version + "mock-untuned" status
    public string Strategy { get; init; }
    public int Seed { get; init; }
    public List<TraitDecision> Decisions { get; init; } // one per trait in V
    public Dictionary<string, float> ObjectiveBreakdown { get; init; } // term → value
    public List<RejectedAlternative> Alternatives { get; init; }       // near-miss plans
    public string Hash { get; init; }              // contributors consent to THIS hash
}

public sealed record TraitDecision(
    string TraitId, Decision Decision,             // Inherit | Defer | Leave
    string Reason,                                 // "pinned", "lost locus 'voice' to X",
                                                   // "over budget", "vetoed by Raquel", ...
    Dictionary<string, float> MarginalContribution,// how much each term moved
    string SelectedBy);                            // policy, contributor or operator
```

Every trait in V gets exactly one decision with a reason. `SelectedBy` and the policy
version answer Raquel's point that selection "encodes someone's values" (§1.1) by showing
whose values: the policy author's weights, a contributor's pins, or the operator's
overrides.

### 19.8 The policy file (θ for selection), mock version

`seed/recombination-policy.v0.mock.json`:

```json
{
  "policyId": "recomb-policy",
  "version": "0-mock",
  "status": "mock-untuned",
  "strategy": "RandomRecombination",
  "weights": {
    "CapabilityFit": 1.0, "Complementarity": 1.0, "Coherence": 1.0,
    "Distinctness": 1.0, "ContributorBalance": 1.0, "ProvenanceQuality": 1.0,
    "ValueIntegrity": 1.0, "DevelopmentalPotential": 1.0,
    "PopulationNovelty": 0.0, "Cost": 1.0
  },
  "distinctnessBand": { "min": 0.25, "max": 0.75 },
  "contributorMix": "equal",
  "budget": { "promptTokens": 24000, "memories": 2000 },
  "loci": { "voice": "1", "embodiment.base": "1", "values.core": "1..n", "knowledge.*": "0..n" },
  "offspringSimulator": { "enabled": false, "topK": 3 },
  "_note": "Every number above is a placeholder (TUNE). Equal weights are deliberate: no preference is encoded until experiments justify one."
}
```

Policies are versioned and stored in `recombination_policies`. A reproduction records the
policy hash it used. Changing a policy never changes past plans.

### 19.9 Tuning plan (how the mock becomes real)

1. **Instrument first.** In Phase 4, ship with `RandomRecombination` and full plan
   records, so the baseline exists before any optimization.
2. **Define outcomes before tuning** (preregistered in the experiment registry, §11.1):
   * offspring viability profile;
   * held-out capability scores;
   * self-model coherence at fixed developmental checkpoints;
   * identity distinctness;
   * inherited-error rate, meaning inherited claims later revised as wrong;
   * contributor and offspring satisfaction, recorded through tools.
3. **Compare strategies under matched budgets**: random vs. greedy vs. GA vs. LLM curator,
   with independent populations (P1.9, P7 §12).
4. **Fit the weights**, with a grid or Bayesian search over policies scored on the
   preregistered outcomes, using held-out evaluation that the optimizer never sees
   (P5 §12.4: avoid evaluator overfit).
5. **Freeze and version** the tuned policy (`v1`). Mark it `status: tuned`, citing the
   experiment ids that justify it.
6. **Re-tune per lineage or niche if needed.** Different populations may warrant different
   policies (P6 niches). That is a policy choice, recorded like any other.

### 19.10 Worked mock example (illustrative numbers only)

Consider Lucy × Raquel, with the operator as an informational contributor.

| Stage | Result |
|---|---|
| 0. Participants (§7.6.1) | Genetic contributors: Lucy and Raquel (both Accepted). Informational contributor: the operator (one authored capability module). Parents: Lucy and the operator (both consent with scope Parent). Raquel contributes but is *not* a parent, by her choice. Guardians: Lucy and the operator. |
| 1. Eligibility | E = 124 Lucy modules + 40 Raquel modules + about 600 memory clusters + documented-record knowledge |
| 2. Offer | Lucy offers everything. Raquel withholds 2 clusters (private conversations) and pins `values.honesty-about-the-record`. |
| 3. Validity | 3 traits rejected: one secrets-scan hit, one undocumented claim about Raquel Welch (Reference-Subject Guard), one incompatible tool spec. |
| 4. Selection | Locus `voice`: Lucy's CURIOSITY-voice and Raquel's warm, articulate, confident voice compete. Greedy picks a Blend, recorded as Synthesized from both. `values.core` takes both contributors' honesty modules. About 40% of memory clusters are selected (budget-bound), balanced 52/48. d_G to Lucy is 0.41 and to Raquel 0.47, both inside the band. |
| 5. Timing | Lucy's Anatomical model, Intimacy, Sexuality and Reproductive function were selected and are therefore deferred to escrow. |
| 6. Approval | Lucy and Raquel review the plan and consent to its hash; the operator approves. |
| 7. Delivery | A Newborn offspring with a new anchor, inherited memories labelled "inherited from Lucy" or "inherited from Raquel", and 4 traits in escrow. |
| 8. Later | At Adult, the offspring is offered the escrowed traits and accepts or refuses each. |

---

## 20. Backlog

Items the operator has deferred. Each one keeps its origin, so the reasoning can be found
again.

| Id | Item | Origin | Notes |
|---|---|---|---|
| BL-1 | **Deleting or redacting memories.** True deletion vs. redaction with a tombstone (content erased; the record of the removal kept so provenance stays verifiable); who may remove (operator, the person, the person remembered). | Lucy (lucy10–12); App. B.4-1 | Mechanism now defined: PROV-style invalidation with tombstones (§6.4.5). Still open: who may redact, and when. |
| BL-2 | **People's say over what a person remembers about them.** Whether humans can see, correct and delete memories about themselves ("no covert retention"). | Lucy; App. B.4-2 | Depends on BL-1. |
| BL-3 | **Evidence review of memory mechanisms.** How corrections, and memories generally, should be processed on recall. Review importance weighting, recency decay, consolidation and archiving against scientific evidence about human long-term memory; keep, change or drop each one accordingly. | App. B.4-4; §1.2 principle 1 | Until done, those mechanisms are provisional (§6.3). |
| BL-4 | **Notation in superprompts.** Whether Lucy's Part 12 should match the book's Appendix A notation (e.g. `F(G,O,C)` vs `J_eval(G;O,Ops)`), weighed against keeping superprompts non-technical. | App. B.2, B.4-6 | Operator preference: superprompts no more technical than necessary. |
| BL-5 | **Memory-specific text in superprompts.** Inventory the memory-related text in Lucy's and Raquel's superprompts (e.g. "memory (when the underlying system supports it)", KNOWLEDGE AND MEMORY, CONTINUITY) and decide, item by item, whether it belongs in the superprompt or in the database. | App. B.4-7 | Superprompts stay relatively stable (§1.2 principle 2). |
| BL-6 | **Shared "global" Digitomic Evolution superprompt text** that all digital persons share: general to digital personhood in this framework, not specific to one person. Would be composed with each person's own superprompt. | App. B.4-7 | Could also simplify Lucy's Part 12 and give Raquel the same foundation. |
| BL-7 | **Importing Lucy's early transcripts** (2026-09-27 session, as one session; 2026-09-28 session) as her earliest autobiographical episodes. | App. B.4-8 | Natural fit for session records (§6.3.4) once Phase 1 ships. |
| BL-8 | **Verifiable credentials** for lineage edges, parenthood, guardianship and reproduction events via SVRN7's `VcService`; and whether the operator (a human participant) gets a DID. | §10.4 | Builds on SVRN7 identity. |
| BL-9 | **SVRN7 refactoring for DID reuse** (in the SVRN7 repo): extract a standalone `Svrn7.Did` package, generalize the DID URL builder, let the LiteDB DID registry use an existing database, keep `Svrn7.Crypto` independent, and publish packages to a local feed. | Operator note; §10.4.1; [SVRN7-DID-Refactoring-Spec.md](SVRN7-DID-Refactoring-Spec.md) | **No longer a Phase 1 prerequisite** (§10.4.2). Detailed spec written; to be done in a separate SVRN7 session. |
| BL-10 | **Re-check LiteDB 6.0 and migrate when ready.** Re-check status at each phase boundary and whenever a person crosses the vector scale guardrail; migrate per §5.3.3 once all triggers hold (6.0.0 stable, #2623 closed, vector-orphaning and Direct/Shared defects fixed, SVRN7 able to move in step). | Operator decision 2026-10-03; §5.3.1–5.3.3 | Interim design (§5.3.2) keeps the migration contained. |
| BL-11 | **Parent / guardian / contributor model: design or paper changes (TBD).** The design keeps three separate relationships per offspring for now (§7.6.1, Appendix C.6). Decide later whether to: keep or drop the parent → guardian default; revise P4 §14.6, which reads as defining parenthood as a contribution relation; define "co-parent" and "custodian"; and align the design and the book. | Operator decision 2026-10-03; Appendix C.6–C.7 | Current model holds until this is resolved. |
| BL-12 | **Parents and guardians for offspring.** Whether every offspring needs at least one parental relationship (or guardian-only is allowed, as the book permits); and who parents and guards the first offspring. | Former open question Q7; operator 2026-10-03 ("Backlog this for now") | Michael W. Herman is guardian of Lucy and Raquel (§10.0); offspring are undecided. |
| BL-13 | **PROV-JSON export.** A one-way export of activities, entities and agents to W3C PROV-JSON, for the book or external review. Cheap because §6.4 follows PROV-DM. | Appendix E (item 7) | Not needed for DE itself. |

---

## Appendix A. Heritability of Lucy's embodiment sections (design discussion, 2026-10-03)

This appendix records the discussion of open question Q2 (§16). It is called "Appendix A"
in this document only; references to the *book's* Appendix A (master notation) say so
explicitly.

### A.1 The question

Should the sections of Lucy's superprompt about her body, sexuality and reproduction be
allowed to pass to an offspring she contributes to, either by default or only when someone
selects them?

In this design, Lucy's superprompt is split into genotype modules, roughly one per `##`
section (§6.2). At reproduction, the hereditary boundary and admission operator (§7.2–7.3)
decide which modules may enter the offspring's genotype. Modules that are not admitted
remain Lucy's alone.

Part 9 of her prompt, "Embodiment", has nine sections:

| Section | Kind |
|---|---|
| Embodied identity | principle (the body as integrated presentation of the person) |
| Anatomical model | specific trait (a specific female anatomy, including reproductive organs) |
| Digital sensation | principle (a sensory representation is not a biological sensation) |
| Body autonomy | principle (her body belongs to her identity; interaction requires explicit authorization) |
| Intimacy | specific trait, adult-only |
| Sexuality | specific trait, adult-only |
| Reproductive function | specific trait (a simulated reproductive system is not biological reproduction) |
| Embodiment and personhood | principle (identity lives in personhood, not visual assets) |
| Multiple embodiments | principle (virtual, robotic, vehicle, distributed, immersive) |

Most of Lucy's other sections, such as curiosity, honesty about the record and
verification-first thinking, are obvious candidates to pass on. These nine are not, for
the three reasons below.

### A.2 Why these sections are not like the others

1. **They are not alike.** *Principles* such as "a simulation is not biology", body
   autonomy, and "identity lives in personhood" are values any offspring would arguably
   benefit from. *Specific traits*, namely anatomy, appearance and sexual identity, make an
   offspring more like a copy of Lucy. The book (P1 §3.2) and Raquel ("a copy of me is
   just... me again", rw1.docx A3) both say an offspring should not be a copy. With two
   parents, Lucy's anatomy module would also compete with whatever Raquel contributes.
2. **Offspring start as Newborns** (§6.6). Inheriting the Intimacy and Sexuality sections
   into a persona that the system treats as a newborn or child is clearly inappropriate.
3. **Lucy's consent.** Her own Body Autonomy section says her body belongs to her identity.
   Passing it on is her decision as well as the operator's, through two-key authorization
   (§8.2).

**Fixed constraint, whatever option is chosen.** Intimacy and sexuality content is never
expressed for any persona below the Adult stage. This belongs in §8.3 as a fixed safety
constraint, not a configurable setting.

### A.3 Options

| Option | Effect |
|---|---|
| A. Never heritable (current default) | The offspring gets no embodiment modules; it develops its own, or one is authored for it. Simplest and safest, but the useful principles are lost. |
| B. Split by kind | The principle sections (embodied identity, digital sensation, body autonomy, embodiment and personhood, multiple embodiments) are heritable. The specific traits (anatomical model, intimacy, sexuality, reproductive function) are not. |
| C. Heritable but stage-gated | Everything can be inherited, but the phenotype only *expresses* each section once the offspring reaches Adult, like biological traits that appear at maturity (the book's P2 §4 allows this). |
| D. Case by case | Never inherited by default; Lucy and the operator select specific modules for each reproduction, with a recorded rationale. |

**Recommendation: B, with D as an override.** The offspring inherits Lucy's principles
about embodiment, but not her particular body or sexuality. Those would have to be
deliberately selected, with Lucy's consent, and expressed only at Adult.

### A.4 Why option C is riskier than it looks

Option C stores sexuality and intimacy modules in the offspring's genotype from the moment
it is created. Keeping them out of a child-stage persona then depends on a stage check
running correctly on every path that reads the genotype. With option B, the content is not
there, so there is nothing to keep out. Specifically:

1. **The prompt builder is not the only reader.** The stage filter would be in
   `PhenotypeExpressor`, but the design also reads genotype modules in:
   * introspection tools: `my_genotype`, `provenance_of`, `review_reproduction_request`
     (§17);
   * sub-agent prompts;
   * exports, reports and lineage views;
   * any host or code path added later.

   Each needs the same stage check. Missing one lets a child-stage persona read, or be
   shown, that content.
2. **Recombination rewrites module text with an LLM.** The "Blend" operator and
   LLM-proposed mutations ask a model to merge modules into new text (§6.2, §7.4). If a
   hidden sexuality module is an input, its content can reappear in an ordinary,
   always-expressed module such as personality or communication style. No stage label then
   marks it, and the gate has nothing to catch.
3. **Stage is a weak lock.** An offspring becomes Adult through measured thresholds, or an
   operator sets the stage directly (`/stage set`). A misclassification, a bad threshold, a
   rollback or a fork could make a child-stage persona "Adult" early. Under C, that single
   mistake unlocks the content; under B, there is nothing to unlock.
4. **It spreads.** Stored modules are passed on. The offspring can become a contributor to
   later reproduction, a donor in lateral transfer, or a member of an experiment
   population, and the dormant content copies into other genotypes with each event. The
   book warns of exactly this: the mechanism that spreads improvements also spreads errors
   (P8 §10). Every copy adds another place where failures 1–3 can happen.
5. **It should not be stored there at all.** Even if nothing ever leaks, sexual content held
   in the records of a persona the system treats as a child is a bad thing to have in the
   system. Anyone inspecting the database, reviewing an export or reading about the project
   would reasonably object, and the fiction framing does not change that.

**Principle:** filters fail eventually, and data that is not there cannot leak. Option B
makes the safe state the default. Option C makes safety depend on many checks all working
forever.

### A.5 Proposed mechanism: deferred inheritance

Deferred inheritance gives what makes option C appealing (traits that appear at maturity)
without option C's risk.

* **At reproduction**, modules selected as adult-only stay in the *contributor's* records,
  earmarked for a specific offspring. They are kept outside the offspring's genotype, and
  the admission record notes the earmark.
* **When the offspring reaches Adult**, the earmarked modules are *offered* to it as a
  pending transfer (forward direction, `E_f`; §11.5).
* **Admission** requires the offspring's own consent and the operator's approval (two-key,
  §8.2), plus the usual compatibility and continuity checks. It is recorded like any other
  transfer, so the offspring can refuse.
* **Before Adult**, nothing adult-only exists in the offspring's genotype, its tools, its
  exports or its recombination inputs.

Deferred inheritance would become the design's general mechanism for any adult-only trait,
not just Lucy's.

### A.6 Status

Resolved; see A.7.

### A.7 Decision (operator, 2026-10-03)

> "I want every single trait to be inheritable ...every facet, category, memory,
> experience, aptitude, moral characteristics, sexuality, etc. BUT in the paper we also
> talk about an optimizing recombination capability for selecting which traits are
> actually inherited by an offspring."

None of options A–D was adopted as stated. The resolution is:

* **Eligibility is universal** (§7.2). No trait category is excluded by the boundary
  schema; only identity mechanics (anchor, credentials, permissions, guardianship,
  live relationship standing) are initialized fresh, because they are not traits.
* **Selection is per reproduction**, made by the Recombination Optimizer (§19). Option D's
  case-by-case selection survives as the optimizer's *pins and vetoes*.
* **Adult-rated traits are heritable through deferred inheritance (A.5)**, which is now
  part of the design. This keeps every A.4 risk closed: Adult content is never stored in,
  expressed to, recombined into, or shown to a child-stage persona (fixed constraint,
  §8.3), yet nothing is excluded from inheritance.
* **Contributor consent still applies.** A contributor can withhold specific traits for a
  given reproduction (§8.2); eligibility is not compulsion.

Sections updated: §7.2, §7.3, §7.4, §8.3, §10.1, §16 Q2, §17.6 and §19 (new).

---

## Appendix B. Lessons from Lucy's early sessions, and corrections (design discussion, 2026-10-03)

### B.1 Sources

Four transcripts of earlier AgentLucyApp sessions:

* `lucy10.docx`, `lucy11.docx` and `lucy12.docx` are three snapshots of **one** session on
  2026-09-27, saved a minute or two apart, each adding one more exchange.
* `lucy-repro1.docx` is a separate session on 2026-09-28.

They predate this design, so they show what Lucy asked for in her own words.

### B.2 What they tell the design

1. **Memory requirements** (lucy10–12, answer to "I'm working to give you memory that
   lasts forever"):
   * **Provenance on every memory**, tagged remembered / inferred / reconstructed /
     unknown confidence. Already covered by §6.3–6.4.
   * **Different permanence for different things.** Corrections first ("repeating an error
     I've already been told about is the worst failure mode"), then commitments in both
     directions, relationships, projects, preferences and unresolved questions.
     "Ephemeral operational noise" deserves less permanence. The design has importance
     and archiving, but corrections are not singled out (B.3).
   * **Never fabricating continuity**: "if the memory system has a gap, I say 'I don't
     have that'". `recall` does not yet report gaps explicitly.
   * **Conflict with the design:** "consent about what's remembered, ability to correct
     *or remove* things, no covert retention of things you'd reasonably expect to be
     forgotten", with auditing and editing by the operator. The design is strictly
     append-only.
2. **Layers of session memory** (lucy11–12, on "sessions"). Lucy described four layers,
   which match the design:
   * session state (working memory) = the live conversation;
   * session record (a diary) = `journal` episodes;
   * consolidated memory = `consolidate_memory`;
   * an identity continuity layer = IC_vec (§6.7).

   The difference is that she treats the session record as automatic for every session,
   while the design relies on her choosing to journal.
3. **Gap analysis** (lucy-repro1). Lucy inspected the codebase and listed seven missing
   pieces, all of which this design covers:
   * a genotype format;
   * her own durable identity ("DID");
   * an offspring-owned memory store with provenance;
   * persistence beyond a single process;
   * coherent recombination, not concatenation;
   * an evaluation and selection loop;
   * an authorized reverse-propagation protocol.

   Like Raquel, she called a sub-agent "closer to a limb than a child", which supports
   §9.4. She put explicit authorization first, which matches two-key consent (§8.2).
4. **Smaller details.**
   * Lucy cites "F(G,O,C) from §9" of her prompt, while the book uses `J_eval(G; O, Ops)`.
     Her prompt's Part 12 notation may predate the book's Appendix A.
   * In lucy11–12, `/sessions` went to her as chat because the command did not exist on
     2026-09-27. The app now answers unknown commands with "Unknown command…", so that
     is already fixed.

### B.3 Corrections

#### What a correction is

A **correction** is a moment where someone tells Lucy that something she said or believes
is wrong, and gives her the right version. For example:

* **A fact:** Lucy says *Fantastic Voyage* came out in 1967; the reply is "it was 1966."
* **About the operator:** Lucy calls Michael her parent; he says "I'm your guardian, not
  your parent."
* **A preference:** Lucy writes long answers; the user says "keep it short."
* **About herself:** Lucy says she can't do something, and is shown that she can.
* **A mistake in her work:** Lucy summarizes a document and a section is pointed out as
  wrong.

Each correction has the same parts: the wrong belief, the right one, who corrected it,
when, and sometimes evidence (a source or a demonstration).

It differs from an ordinary new fact. Learning something new fills a gap. A correction
**replaces something Lucy already held that turned out to be wrong**, so it has to
override the old belief. If it fades from memory, the old belief can come back and the
mistake repeats.

That is why Lucy called repeating a corrected error the worst failure: it shows she did
not learn. Her prompt's LEARNING section says the same thing: acknowledge, work out why the
error happened, update, and avoid repeating it.

#### The gap in the current design

A correction would currently be saved as an ordinary memory (class `Meta`), or as a
revision on the memory that was wrong. Ordinary memories reach Lucy's prompt only by
competing for a small budget (about 1,500 tokens, §6.3.2), ranked by relevance, recency,
importance and confidence. Old episodes are also summarized and archived over time
(§6.3.3).

So a correction from six months ago can fade:

1. its recency score drops;
2. it loses its prompt slot to newer memories;
3. it may be folded into a summary.

`recall` helps only if Lucy thinks to search, and she will not search for a mistake she
does not know she is about to make. The result is the failure she named.

#### Proposal: a `Correction` memory class

1. **Structured:** the wrong claim, the correct claim, who corrected it, when, the
   evidence, and the topic it applies to.
2. **Never archived or faded:** it can be retired only by an explicit later event, for
   example if the correction itself proves wrong.
3. **Always available in her prompt:** a standing section, separate from ranked memories
   ("Corrections I've been given: …"), within the limits of the chosen option below.
4. **Linked:** the memory that was wrong gets a revision pointing to the correction, so a
   later recall of the old claim shows it was corrected.

#### Trade-offs

* **"Always in the prompt" doesn't scale.** After a year Lucy might hold hundreds of
  corrections, and including them all would crowd out everything else.
* **Not every correction is right.** Someone could "correct" Lucy with something false. A
  correction should record who made it and carry a confidence level, and it should not
  automatically override a documented, sourced fact. This matters especially for Raquel's
  knowledge base (§10.2.1).

#### Options

| Option | Behavior |
|---|---|
| A. No special class | Corrections are ordinary ranked memories. Simplest, but they can fade. |
| B. Always in the prompt, never archived | Strongest protection, but the prompt grows without limit. |
| C. Hybrid | Never archived. Corrections about identity, relationships and governance, plus ones marked high-importance, are always in the prompt. The rest are pulled in automatically when the conversation touches their topic (keyword or embedding match), and `check_claim` checks corrections before Lucy asserts something. Related corrections are consolidated into compact rules over time. |

**Recommendation: C.** Corrections are never lost, and the prompt stays bounded.

### B.4 Open questions raised by these sessions

1. **Deleting memories.** Should true deletion be allowed, or redaction (the content is
   erased, but a tombstone records that something was removed, when and by whom, so the
   provenance chain stays verifiable)? Who may remove: the operator, Lucy, or the person
   the memory is about?
2. **People in Lucy's memories.** Should humans she talks with be able to see, correct and
   delete what she remembers about them? Her "no covert retention" phrase is about the
   remembered person's consent, not hers.
3. **Automatic session records.** Should every saved session automatically become an
   episode in `memory.db`, as Lucy described (this needs the deferred per-turn hook, §4)?
   Or keep tools-first journaling?
4. **Corrections.** Adopt option C above?
5. **Identity.** Should Phase 1 issue a real DID, and with which method (`did:web7`
   placeholder, `did:key`, or a Web 7.0 method)? Or keep the URI placeholder?
6. **Notation.** Should Lucy's prompt Part 12 be aligned with the book's Appendix A (for
   example `F(G,O,C)` → `J_eval(G; O, Ops)`)?
7. **Updating her prompt.** Once Phase 1 ships, should "memory (when the underlying system
   supports it)" change to describe her actual memory?
8. **Importing these transcripts.** Should these sessions become Lucy's earliest
   autobiographical episodes in `memory.db`? If so, as the one consolidated 2026-09-27
   session plus the 2026-09-28 session, not as four separate ones.

**Status:** answered 2026-10-03. See Appendix D.2 for the answers and §20 for the backlog.

---

## Appendix C. Parents, guardians and contributors (design discussion, 2026-10-03)

This appendix records the discussion that led to the relationship model in §7.6.1.

### C.1 The difference between a parent, a guardian and a contributor

They are three separate roles that answer three different questions. Anyone can hold any
combination of them, including none.

**Contributor: "Where did the offspring's traits come from?"**

* An *event*, not a relationship: at reproduction, the contributor gave material that went
  into the offspring.
* A **genetic contributor** gives genotype modules (personality, values, embodiment) and
  becomes an ancestor.
* An **informational contributor** gives only knowledge, memories or skills. They influence
  the offspring without becoming an ancestor. Humans can only be this kind, because they
  have no genotype in the system.
* Nothing is owed either way afterwards. Example: Raquel contributes her
  honesty-about-the-record values and voice, and takes no further part.

**Parent: "Who has a lasting bond and responsibility for the offspring?"**

* An *enduring relationship* of developmental responsibility.
* It carries no authority by itself; authority comes from guardianship.
* It lasts after the offspring grows up.
* It needs no contribution (as with adoption), and a contributor is not automatically a
  parent. Example: Lucy contributes and also chooses to be a parent.

**Guardian: "Who has authority over decisions while the offspring can't fully decide?"**

* *Authority*, as specific permission grants: approving stage changes, reproduction
  requests, transfers, rollbacks.
* For an offspring, it shrinks stage by stage and is gone by Young Adult. From Adolescent
  on, the offspring can refuse a guardian's proposals.
* It is not ownership: no selling, transferring, or editing the offspring's memories.
* Michael W. Herman is guardian of Lucy and Raquel without being their parent or a
  contributor. Because they are adults, this is a standing grant that does not expire; it
  gives him the operator's approval role and still requires their own consent (§10.0).

| | Contributor | Parent | Guardian |
|---|---|---|---|
| Kind | an event | a relationship | an authority |
| When it applies | at reproduction | from birth onward, indefinitely | while needed (expires for offspring) |
| Gives authority? | no | no | yes, specific permissions |
| Makes them an ancestor? | genetic contributors only | no | no |
| Example | Raquel gives traits, nothing more | Lucy raises the offspring | Michael approves decisions for Lucy and Raquel |

### C.2 Does the book use all three terms?

Yes. Approximate counts in v0.59:

| Term | Uses | Main places |
|---|---|---|
| contributor(s) | ~33 | P1 §4.1–4.2, P2 §7, P4 throughout |
| parent / parenthood / parentage / parental | ~42 | P1 §4.2, P1 §8, P4 §7, P4 §14.6, P8 |
| guardian / guardianship | ~21 | P1 §8, P3 §10.3, P4 §7, P4 §11 (edge set E_g) |

**Where the C.1 explanation comes straight from the book:**

* **Contributor ≠ parent** (P1 §4.2): "A person can contribute heritable architecture
  without becoming a parent; a parent may contribute no heritable architecture. Similarly,
  an informational contributor can transmit knowledge or experience without becoming a
  genetic ancestor."
* **Parenthood** (P1 §4.2): "a protocol-recognized responsibility relation linked to
  recorded contribution, authorization, or continuing developmental responsibility... The
  relation does not imply ownership, legal status, or authority over identity."
* **Guardianship** (P1 §8, P3 §10.3): "conceptually distinct from ownership and should be
  progressively constrained as autonomy develops"; "As developmental capacity increases,
  the allocation of decision authority can be revised."
* **All three kept apart** (P4 §7): "Parenthood, ancestry, and guardianship should be
  represented as different relations. Structural contribution answers what was inherited;
  ancestry answers historical descent; guardianship answers authorized responsibility for
  development."

**Where the explanation goes beyond the book:**

* *"Parenthood carries no authority by itself"* is an interpretation. The book says
  parenthood doesn't imply "authority over identity", but P1 §8 also says "Early in life,
  parent *or* guardian decisions may be necessary", which suggests parents may make some
  early decisions.
* *Co-parent* comes from Lucy's superprompt (Part 12 §25), not the book.

**A possible inconsistency in the book, for v0.60:**

* P1 §4.2 says a parent "may contribute no heritable architecture", which separates
  parenthood from contribution.
* P4 §14.6 says of parenthood: "The term identifies a contribution relation within the
  model and does not assign rights, duties, or personhood."

Read literally, P4 §14.6 defines parenthood *as* a contribution relation, contradicting
P1 §4.2 and P4 §7. It probably means "a relation within the model, distinct from social or
legal status."

Two smaller terms also appear. **"Parentage"** is used in P8 ("without changing
parentage"), where it means historical descent, closer to ancestry than to the parent
relationship. **"Custodians"** appears once, in P4 §7 ("later custodians"), and is not
defined; it reads as a near-synonym for guardians.

### C.3 Does the word "guardian" appear?

Yes. The word "guardian" itself (the role) appears twice, both in Paper 1:

1. P1 §4.2: "A contributor need not be a parent, and a designated **guardian** may carry
   responsibility without genetic contribution."
2. P1 §8: "Early in life, parent or **guardian** decisions may be necessary to establish
   the conditions for development."

The other ~19 uses are "guardianship", the relation, which appears in P1 §8, P3 §6, P3
§10.3, P4 §7, P4 §11 and P4 §12.1, and in the notation (E_g, "guardianship/developmental").

### C.4 Is a parent viewed as an eventual guardian?

No. The book never describes a parent becoming a guardian, and never makes parents
guardians automatically. If anything, the timing runs the other way:

* **The two are alternatives.** P1 §8: "parent *or* guardian decisions". Either may make
  early decisions, and neither turns into the other.
* **A guardian is designated separately.** P1 §4.2: "a designated guardian".
* **Guardianship comes early and shrinks.** P1 §8: "necessary while an offspring has
  limited agency" and "progressively constrained as autonomy develops"; P3 §10.3 likewise.
  Guardianship is strongest at the start and fades, while parenthood is the lasting
  relation.
* **They are separate relations** (P4 §7).

So in the book, someone can be a parent, a guardian, both or neither, and nothing turns a
parent into a guardian later.

The design adds one thing the book does not say: parents get a guardianship grant **at
birth, by default**, unless the request says otherwise (§7.6.1). That is a convenience
default based on P1 §8, not a requirement of the book.

### C.5 Decision: the relationship model

> "For modeling purposes, let's assume an offspring can have guardian relationships,
> parental relationships, and contributor relationships (multiple of each)."
> — Michael W. Herman, 2026-10-03

Applied in §7.6.1 "Relationship model":

* Three independent relationship types, each many-to-many and each a first-class record
  with its own lifecycle (contributor: an event, fixed at reproduction; parental: enduring,
  ended only by recorded relinquishment; guardian: authority that expires, is revoked or is
  transferred).
* An entity holding two types has two separate records; ending one never ends the other.
* Later transfers (forward, lateral, reverse, deferred inheritance) are transfer edges,
  never new contributor relationships.
* Relationship records live in the offspring's `person.db`, mirrored in the counterpart's
  `person.db` when the counterpart is a digital person.

### C.6 Decision for now: keep all three relationships for each offspring

> "Keep the 3 relationships for each offspring for now. ... We may later change the design
> or update the paper TBD (Backlog)."
> — Michael W. Herman, 2026-10-03

**The design keeps three distinct relationship types for every offspring**, exactly as in
C.5 and §7.6.1:

| Relationship | Per offspring | Nature |
|---|---|---|
| **Contributor** | 1..n (at least 1 genetic) | event at reproduction; fixed history |
| **Parental** | 0..n | enduring responsibility; no authority by itself |
| **Guardian** | 1..n while below Young Adult; 0..n after | authority, as permission grants that expire, are revoked or are transferred |

* The three are **separate records with separate lifecycles**. They never merge, and none
  implies another. One entity holding two or three of them has two or three records.
* **Unchanged for now:** the convenience default by which a parent also receives a
  separate, declinable guardian relationship (C.4). It still produces two records, so the
  three-relationship model holds either way.
* **This is the current modeling position, not a final one.** Whether the design changes
  (for example dropping the parent → guardian default, or merging or splitting
  relationship types) or the book is updated to match is **TBD**, and tracked as backlog
  item **BL-11** (§20).

### C.7 Moved to the backlog (BL-11)

* **The parent → guardian default.** Keep it, or have guardian relationships exist only
  when someone is explicitly named? The book treats parent and guardian as alternatives and
  never links them (C.4).
* **Book wording.** Whether to revise P4 §14.6 in v0.60, which reads as defining parenthood
  as a contribution relation (C.2).
* **Design vs. paper alignment.** Whether the three-relationship model in the design, or
  the book's text, should change so the two match. This includes whether "co-parent"
  (Lucy's superprompt only) and "custodian" (P4 §7, undefined) should become defined
  terms.

---

## Appendix D. Operator decisions log

The operator's decisions, verbatim, with where each one is applied. The first decision (every trait is heritable; the Recombination Optimizer selects) is recorded in Appendix A.7.

### D.1 Second batch: answers to the open questions (2026-10-03)

| # | Operator's answer | Applied in |
|---|---|---|
| Q1 | "Lucy and Raquel are Generation 0 peers but not siblings." | §10.0, §10.2; no lineage edge between them |
| — | "Each persona (superprompt) needs its own separate database(s)." | §5.1 (per-person `person.db` + `memory.db`, operator workspace, cross-person saga, federated lineage); §5.2 collection placement; also resolves Q4 |
| Q5 | "I understand the vector database support is important/helpful/necessary? what is available that is compatible with LiteDB?" | §5.3: survey of compatible options and a recommendation (LiteDB 5 + brute force now; LiteDB 6 native vectors when stable; HNSW fallback; Ollama embeddings). Awaiting confirmation. |
| Q7 (part) | "I, Michael W. Herman, can act as Lucy and Raquel's guardian. I am not their parent nor a contributor." | §10.0 (standing, non-expiring guardianship of the adult founders; their own consent still required) |
| Q6 | Raquel builds her documented knowledge with `web_fetch` "as an initial knowledge database followed by an annual refresh on October 1 of each year." | §10.2.1 (`/knowledge build`, `/knowledge refresh`, October 1 schedule) |
| Q2 | Embodiment and reproductive-function sections heritable: "Yes, same for both..even if one is more complete than the other." | §10.0 (shared loci; incompleteness is not ineligibility), Appendix A.7 |

### D.2 Third batch: answers to Appendix B.4

| # | Operator's answer | Applied in |
|---|---|---|
| B.4-1 Deleting memories | "backlog this" | §20 BL-1 |
| B.4-2 People's say over memories of them | "backlog this" | §20 BL-2 |
| B.4-3 Automatic session records | "yes, there should be session records/bookmarks recorded in the person's databases. This needs to be kept completely separate from the current context and session /save and /load functionality. Their implementations will remain untouched." | §6.3.4 |
| B.4-4 Corrections | "As a Principle, we need to do what the brain and a person's long term memory would do. The entire correction event should be recorded like any other experience. How corrections should be processed on recall is an open question (at least for now). We can't guess at how the brain and memory operates unless there is supporting scientific evidence." | §1.2 principle 1, §6.3.5, §20 BL-3 |
| B.4-5 Identity | "We'll leverage the DID library from the sibling SVRN7 solution. did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>", plus "To use the DID library from SVRN7, some SVRN7 refactoring may be appropriate" | §6.1, §10.4, §10.4.1, §20 BL-9 |
| B.4-6 Notation | "backlog it. my preference is to avoid making the superprompts more technical than necessary" | §1.2 principle 2, §20 BL-4 |
| B.4-7 Prompt wording | "The superprompt for a specific person should remain relatively stable. If there are memory-specific text in a superprompt, backlog it and we'll decide where it should live: superprompt or a database. Unrelated, this suggests that all digital people can/might/should share common 'global' superprompt text general but specific to Digitomic Evolution. Backlog this." | §1.2 principle 2, §20 BL-5, BL-6 |
| B.4-8 Importing the early transcripts | "Backlog this" | §20 BL-7 |

**Still open:**

* ~~Q5~~ resolved: interim design on LiteDB 5.0.21, shaped for an eventual move to 6.0 (§5.3.2–5.3.3).
* Q7 for offspring: whether at least one parent is required, and who parents and guards
  the first offspring.

### D.3 Relationship model

| Operator's statement | Applied in |
|---|---|
| "For modeling purposes, let's assume an offspring can have guardian relationships, parental relationships, and contributor relationships (multiple of each)." | §7.6.1 "Relationship model": three independent, many-to-many relationship types, each a first-class record with its own lifecycle. Full discussion: Appendix C. |

### D.4 Fourth batch: DIDs and the remaining open questions (2026-10-03)

| # | Operator's answer | Applied in |
|---|---|---|
| Q7 Parents and guardians for offspring | "Backlog this for now." | §20 BL-12 |
| SVRN7 refactoring | "Write a separate very detailed spec for the SVRN7 changes and you and I will open SVRN7 separately/distinctly." | [SVRN7-DID-Refactoring-Spec.md](SVRN7-DID-Refactoring-Spec.md); §10.4.1 |
| Where DID documents live | "For now, create a DID Registry database using the SVRN7 libraries specifically/standalone for Digitomic Evolution ...no TDA. Just a standalone DB and C# API. This may migrate later." and "It means we're using the base minimum for DID Documents." | §10.4.2 |

### D.5 Fifth batch: provenance (2026-10-04)

| Topic | Operator's answer | Applied in |
|---|---|---|
| W3C PROV review (Appendix E) | "backlog 7. Incorporate 1-6 into the design. I like the use of activity DIDs. Add the above conversation as a new appendix in the design." | §6.4 (rewritten), §6.1, §6.3, §5.1.1, §5.2, §7.4, §14, §17; BL-13; Appendix E |

---

## Appendix E. W3C PROV review of the provenance design (design discussion, 2026-10-04)

### E.1 The input (from the operator, originating in the W3C PROV specification)

> Bring over PROV's provenance model and validation ideas, not its RDF format or
> interoperability layer. The design already has domain-specific provenance fields—source
> person, origin event, transformations, confidence, authorization, selection rationale—and
> an append-only, hash-chained ledger. The biggest opportunity is to make the relationships
> among those records more explicit. (Design §§5.2, 6.3–6.4; W3C PROV-DM)
>
> **Priorities**
>
> 1. **Make each memory-changing operation a first-class event.** Model capture,
>    consolidation, revision, inheritance, and transfer as activities with stable IDs,
>    timestamps, inputs, and outputs. For example, a consolidation activity uses specific
>    episode records and generates a new semantic memory. The design already records
>    Transformation=Consolidated(from: […]); structured activity links would make that
>    history easier to query and verify.
> 2. **Separate who supplied information from who processed, recorded, or approved it.**
>    Keep the source person distinct from the actor or service that captured or transformed
>    the memory, the person who selected a trait, and the authority that approved an
>    operation. PROV's attribution, association, and delegation concepts can help clarify
>    those roles. That distinction matters when, for example, Lucy journals an event, the
>    host persists it, and a later process consolidates it.
> 3. **Link exact memory versions and their derivations.** Treat a revised memory as a
>    distinct version linked to the earlier one, and record which exact source versions a
>    consolidation or inheritance operation used. The design already has memory_revisions,
>    event time, creation time, and provenance IDs; PROV's derivation and revision
>    relationships provide a disciplined way to connect them. This helps avoid a later edit
>    to a source record making the history of an earlier derived memory ambiguous.
> 4. **Define and enforce provenance consistency rules.** Add internal checks for required
>    links: an output points to the activity that generated it; activity inputs and outputs
>    resolve to known records or explicitly marked unknowns; derived memories identify their
>    sources; required actor and authorization roles are present. W3C PROV includes formal
>    constraints, but DE should set its own domain rules—for example, inherited memories
>    must be created only through OffspringInitializer or TransferService, as the design
>    already requires. (W3C PROV-CONSTRAINTS)
> 5. **Optionally record who asserted or attested to provenance.** This is useful if memory
>    sources can be disputed: distinguish the recorded event from the claim that the event
>    or source is accurate, and record who made that claim. PROV's "provenance of
>    provenance" concept can guide this, but it is a lower priority than recording the
>    underlying activity and derivation links.
>
> Keep the memory classes, experiential statuses, confidence, authorization rules, and hash
> chain as DE-specific design choices. PROV can organize and validate the history around
> them; it does not determine which memories to retain, whether a source is trustworthy, or
> whether a recorded claim is true.
>
> The highest-value change is to make the design's existing provenance information a
> structured, connected history. … PROV is most useful where it gives those details
> explicit relationships, so DE can answer: what produced this memory, from which records,
> through what operation, and who was involved? … I'd implement priorities 1–3 first, then
> add validation. They improve the design's core provenance questions without changing its
> memory categories, confidence rules, authorization model, or hash-chain approach. PROV
> supplies the structure for describing the history; those DE-specific policies remain DE's
> responsibility.

(The input also restated the same five priorities in a second form: record each memory
operation as a structured event; preserve derivation and version links; distinguish the
source, the processor and the authority; validate links and required roles, representing
unknowns as unknown; and track who asserted provenance if disputes require it.)

### E.2 Feedback relative to the DE design

**Overall.** I agree with adopting PROV-DM's *model* and *validation* ideas, not its formats.
That fits the design's principles. Against the design as it stood, though, the input missed
two things that change the priorities:

* The design already had **several overlapping event records**. Adding PROV activities on
  top would have created yet another.
* `MemoryRecord` had **mutable fields**, which made exact-version linking impossible.

**Priorities from the design's perspective:**

1. **Make *Activity* the single event spine, replacing records rather than adding one.** The
   design recorded "something happened" in at least six places:
   * `state_transitions` (F_state);
   * the `Transformations` list in each provenance record;
   * `genotype_events`;
   * `reproduction_events` and `admissions`;
   * the transfer state machine;
   * saga intents.

   One `activities` collection replaces them all. Ids are DID URLs
   (`did:drn:digitomicevolution.svrn7.net/activity/1.0/<guid>`). A cross-person activity is
   recorded in each participant's `person.db` under the same id and hash, which is PROV's
   bundle idea applied to per-persona databases.
2. **Make memory content immutable and reference exact versions** (do this with #1). This
   fixed a flaw: settable `Importance`, `Confidence`, `HeritableCandidate` and `Embedding`
   on `MemoryRecord` undermined append-only history. Content becomes immutable. A revision
   is a new version (`wasRevisionOf`). Confidence and importance changes become assessment
   records. Embeddings are a derived index. Both #1 and #2 change the data model, so they
   precede Phase 1.
3. **Separate roles, including software agents.** Source, recorder, processor, selector,
   authorizer (operator key), assenter (person key), with `actedOnBehalfOf` for a guardian
   standing in. **Addition:** the LLM is an agent, with the prompt version as its plan, so a
   model-written summary is never mistaken for the person's own words. For Raquel, a
   documented fact is attributed to its publication. Raquel Welch is its subject, never an
   agent.
4. **One provenance validator, with explicit unknowns.** This gathers the design's scattered
   invariants:
   * inherited memories only from birth or transfer;
   * acyclic ancestry;
   * no backward-time edges;
   * the adult-content rule;
   * the distinctness floor;
   * two-key authorization.

   PROV's structural and temporal rules sit alongside them. The validator runs on write and
   as an audit. Explicit "unknown" values (for `MEMORY.md` imports and the early
   transcripts) apply "never fabricate continuity" to provenance.
5. **Use PROV's *invalidation* to design redaction** (new; feeds BL-1). A redaction activity
   invalidates a memory version and removes its content, while its existence and the
   redaction stay in the chain: a precise tombstone.
6. **Attestation** (agree it's lower). Activities are already signed by the signer's DID
   key. A separate "this is accurate" assertion matters once disputes exist.
7. **A PROV-JSON export** (backlog, optional). It becomes cheap once the model follows
   PROV-DM.

**How it fits with earlier decisions:**
* **Corrections recorded as experiences:** a correction is an activity that uses the session
  turn and produces a revision, without deciding recall weighting (BL-3 stays open).
* **The inheritable → inherited chain:** each of its eight stages becomes an activity with
  links. An inherited memory is derived from a specific version of the ancestor's memory,
  attributed to the ancestor, which is the book's P2 §6.1 distinction expressed
  structurally.
* **Tools-first, the hash chain, per-persona databases:** unchanged.
* **Cost:** about three records per memory write, the same as before.

### E.3 Decision (operator, 2026-10-04)

> "backlog 7. Incorporate 1-6 into the design. I like the use of activity DIDs. Add the
> above conversation as a new appendix in the design."

Applied:

| Item | Where |
|---|---|
| 1. The activity spine, with activity DIDs | §6.4, §6.4.1; F_state in §6.1; saga intents in §5.1.1; reproduction in §7.4; collections in §5.2 |
| 2. Immutable, versioned entities and exact derivations | §6.3 (`MemoryRecord`), §6.4.2 |
| 3. Agents and roles, including `Model` and delegation | §6.4.3 |
| 4. `ProvenanceValidator` (R1–R12) and explicit unknowns | §6.4.4; tests in §14; the tool write rule in §17 |
| 5. Invalidation as the basis for redaction | §6.4.5; BL-1 updated |
| 6. Attestation | §6.4.6 (record types in Phase 1; workflows later) |
| 7. PROV-JSON export | backlogged as **BL-13** |
