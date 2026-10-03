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

---

## 2. Concept-to-component map (every facet)

Each row is a construct from the book and the component that realizes it. Symbols follow the
book's Appendix A.

| Book construct (symbol, paper) | Component (all in `DigitomicEvolutionLib` unless noted) | Stored in |
|---|---|---|
| Digitomic unit, digital person (P0 §3) | `DigitalPerson` aggregate | `persons` |
| Identity anchor I; I_o ≠ I_i (P1 §7, P4 §6) | `IdentityAnchor` (immutable id + DID-style URI) | `persons` |
| Person-state S_t (P1 §7) | `PersonState` snapshot (I, G_t ref, K, M, Exp, Val, Rel, Agency, Cap) | `person_states` |
| F_state transition (P1 §7) | `StateTransitionEngine`: one recorded transition per state-changing tool call, with typed inputs (Input, Learn, Develop, Adapt, Govern) | `state_transitions` |
| Genotype G = (I_g, C_arch, Q_cog, D_dev, M_h, K_h, Val_h, R_ρ, P_cap, H_prov) (P2 §3) | `Genotype` + typed `GenotypeModule`s | `genotypes`, `genotype_modules` |
| Phenotype P_t = Φ_dev(G, Env, Hist, Δ_dev) (P2 §4, P3 §3) | `PhenotypeExpressor` → system-prompt sections + runtime config | `phenotype_observations` |
| Developmental potential Ω_G; P_cap (P2 §4.1) | `CapabilityGraph` (capabilities, prerequisites, ceilings) | `capability_defs` |
| Hereditary boundary; Adm(G_candidate; σ, Π) (P2 §5, §10) | `HereditaryBoundary` schema + `AdmissionService` | `boundary_schemas`, `admissions` |
| Mutation μ, μ_dev; G′ (P2 §7, P3) | `IGenotypeOperator` (stochastic, rule-based, search-guided, authored) | `genotype_events` |
| Recombination ℛ(Contrib_1..n; θ, ε_var) (P2 §7.1, P4 §4) | `IRecombinationOperator` + `RecombinationProtocol` (θ) | `reproduction_events` |
| Directed/optimizing recombination (P1 §6.2–6.3, P1.9, P7 §3, §6) | **Recombination Optimizer (§19, MOCK)**: `IRecombinationOptimizer`, `RecombinationPolicy`, `RecombinationPlan` | `recombination_policies`, `recombination_plans` |
| Contribution package Contrib_i (P4 §3) | `ContributionPackage` | `contributions` |
| Offspring initialization S_o(t_0) = Init(G_off, Env_0, Δ_dev,0) (P4 §12) | `OffspringInitializer` | `persons`, `person_states` |
| Parenthood, ancestry, guardianship (P1 §8, P4 §7) | Typed `LineageEdge`s plus `GuardianshipGrant` | `lineage_edges`, `guardianships` |
| Lineage graph L_t = (N_t, E_r, E_a, E_g, E_d, E_f, E_l, E_rev) (P4 §11, P8 §8) | `LineageGraph` (node = identity@version) | `lineage_nodes`, `lineage_edges` |
| Provenance H_prov, Π, Π_rev (P1 §7.1, P2 §6, P8 §4) | `ProvenanceRecord` + hash-chained `ProvenanceLedger` | `provenance` |
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
| `state_transitions` | append-only | `PersonId+T` | F_state input record per step |
| `genotypes` | append-only (versioned) | `PersonId+Version`, `GenotypeId` | G tuple header; modules by reference |
| `genotype_modules` | append-only, content-addressed | `Hash` (unique), `Component`, `InheritanceClass` | each module is one "gene" |
| `genotype_events` | append-only | `PersonId`, `Kind` | μ, μ_dev, heritable modifications |
| `memories` | append + revision records | `PersonId+Class`, `Keywords` (multikey), `ImportanceDecay` | long-term memory (§6.3) |
| `memory_revisions` | append-only | `MemoryId` | contrary evidence, confidence changes (P2 §6.2) |
| `knowledge` | append + revision | `PersonId+Kind`, `Keywords` | semantic facts, including documented-record items with sources |
| `relationships` | update-in-place, history kept | `PersonId+CounterpartId` | Rel_t |
| `self_models` | append-only | `PersonId+T` | self-description for SC_t |
| `development_status` | append-only | `PersonId+T` | stage, measures, threshold evidence |
| `stage_defs`, `capability_defs`, `boundary_schemas` | versioned config | `Version` | preregistered definitions |
| `permissions`, `guardianships`, `consents` | append-only (grants and revocations are events) | `PersonId`, `Kind` | governance |
| `contributions` | append-only | `ReproductionEventId`, `ContributorId` | Contrib_i |
| `admissions` | append-only | `ReproductionEventId` | per-component admit or reject with reason and selector |
| `reproduction_requests` | append-only (participant states as events) | `Status`, `ParticipantId` | participants, roles, per-role consents (§7.6.1) |
| `reproduction_events` | append-only | `OffspringId` | full ℛ/Adm/Init record |
| `lineage_nodes`, `lineage_edges` | append-only | `From`, `To`, `EdgeType`, `T` | L_t (§7.7) |
| `transfers` | append-only (state machine as events) | `RecipientId`, `Direction`, `Status` | T_rev, lateral, forward |
| `snapshots` | append-only | `PersonId+T` | signed pre-change state (P8 §12.7) |
| `continuity_assessments` | append-only | `PersonId` | IC_vec, IC_score, τ_IC |
| `provenance` | **append-only, hash-chained** | `SubjectId`, `Seq` | ledger (§6.4) |
| `populations`, `generations`, `fitness_evaluations` | append-only | `PopulationId+Gen` | P5 |
| `compat_trials`, `species_hypotheses` | append-only | `PopA+PopB` | P6 |
| `de_runs`, `interventions` | append-only | `RunId` | P7 |
| `experiments`, `measurements` | append-only (preregistration frozen by hash) | `ExperimentId`, `Metric` | all papers |
| `meta` | update | — | schema version, migration log, chain heads |

**Which database holds each collection** (§5.1):

* **`memory.db`** (per person): `memories`, `memory_revisions`, `knowledge`, and the
  vector data.
* **`person.db`** (per person): `persons` (the single self record, plus cached stubs of
  counterparts' anchors), `person_states`, `state_transitions`, `genotypes`,
  `genotype_modules`, `genotype_events`, `relationships`, `self_models`,
  `development_status`, `permissions`, `guardianships`, `consents`, `contributions`
  (outgoing), `admissions`, `reproduction_events` (in the offspring's file),
  `lineage_edges` (the edges touching this person), `transfers`, `snapshots`,
  `continuity_assessments`, `provenance`, `recombination_plans` (in the offspring's file),
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
public sealed record IdentityAnchor(string PersonId, string Uri, DateTime CreatedUtc);
// Uri e.g. "did:web7:digitomic:lucy" – resolvable later; today just a stable name

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
Every state-changing digitomic tool call appends one `StateTransition` record whose fields
are the book's arguments. `Input_t` is the triggering tool call and its episode. `Learn_t`
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
    public string ProvenanceId { get; init; }
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

public sealed class MemoryRecord
{
    public string Id { get; init; }
    public string PersonId { get; init; }
    public MemoryClass Class { get; init; }
    public ExperientialStatus Status { get; init; }
    public string Content { get; init; }          // written to read well out of context
    public string[] Keywords { get; init; }
    public float Importance { get; set; }         // 0..1, set at capture, adjusted on recall
    public float Confidence { get; set; }         // revisable (P2 §6.2)
    public DateTime EventUtc { get; init; }       // original event time
    public string? EpisodeId { get; init; }       // groups a conversation
    public string? CounterpartId { get; init; }   // relational memories
    public string ProvenanceId { get; init; }     // source, generation, transformation, auth
    public string? InheritedFromPersonId { get; init; } // set iff Class == Inherited
    public bool HeritableCandidate { get; set; }  // flagged for possible admission; NOT heritable
    public float[]? Embedding { get; set; }
}
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
semantic or procedural memories. The output links back to its sources through a provenance
`Transformation=Consolidated(from: […])`. Cold episodic memories are marked archived but
never deleted.

**Invariant.** `Inherited` memories are created only by `OffspringInitializer` or
`TransferService`. They are never created by tools or capture. This enforces
`Experienced_by(ancestor,E) ≠ Experienced_by(descendant,E)` (P2 §6.1) in code.

### 6.4 Provenance ledger

```csharp
public sealed class ProvenanceRecord           // H_prov / Π / Π_rev
{
    public string Id { get; init; }
    public long Seq { get; init; }               // global, gap-free
    public string SubjectId { get; init; }       // memory/module/genotype/transfer id
    public string SourcePersonId { get; init; }
    public int Generation { get; init; }
    public ContributionType Type { get; init; }
    public string? OriginEvent { get; init; }    // episode/turn/reproduction id
    public List<Transformation> Transformations { get; init; }
    public float Confidence { get; init; }
    public AuthorizationRef? Authorization { get; init; }
    public ProvenanceStatus Status { get; init; } // Proposed|Admitted|Rejected|Revoked|RolledBack
    public string? SelectedBy { get; init; }     // who chose it (Raquel's "whose values")
    public string? SelectionRationale { get; init; }
    public string PrevHash { get; init; }
    public string Hash { get; init; }            // SHA-256(PrevHash + canonical JSON)
    public string? Signature { get; init; }      // IRecordSigner; optional now (§10.4)
}
```

Every write that creates hereditary or inherited material goes through
`ProvenanceLedger.Append` in the same LiteDB transaction as the subject document.

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
| Within-state adaptation | runtime config changes during a session | `state_transitions.Adapt` |
| Developmental change | memory, knowledge, relationships, stage | memories, development_status |
| Heritable-developmental modification | admitted change to the person's own G (new genotype version) | genotype_events + admission |
| Generational evolution | reproduction | reproduction_events |

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

`ReproductionService.ReproduceAsync(ReproductionRequest)` runs in one LiteDB transaction:

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
* At commit, the roles become lineage edges (§7.4 step 8):
  * `E_r` for every accepted contributor, typed Genetic or Informational;
  * `E_a` for genetic contributors only;
  * `E_g` for each parent, co-parent and guardian, typed accordingly.
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

### 10.4 Identity URIs and signing (Web 7.0 hook)

`IdentityAnchor.Uri` uses a `did:web7:` style string so that future DID or verifiable
credential work can resolve it. `IRecordSigner` is the pluggable signer for provenance
records and snapshots. It ships with `NullSigner` (hash chain only) and an `EcdsaFileSigner`
that keeps keys in the user profile, enough to produce the "signed pre-transfer snapshot"
of P8 §12.7. The book explicitly leaves DID and VC standards out of scope; so does this
design, which only keeps the hook.

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
  Identity/         DigitalPerson.cs, IdentityAnchor.cs, IdentityContinuityService.cs,
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
  Provenance/       ProvenanceRecord.cs, ProvenanceLedger.cs
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
   (§10.0, Appendix B).
2. ~~**Lucy's embodiment and reproductive-function modules.** Should they be heritable?~~
   **Resolved:** every trait is heritable; the Recombination Optimizer (§19) selects;
   Adult-rated traits use deferred inheritance. The same applies to Raquel, even where
   one persona's sections are more complete (§10.0, Appendix A.7, Appendix B).
3. ~~**Guardians of the first offspring.**~~ Folded into question 7.
4. ~~**Concurrency.**~~ **Resolved** by separate per-persona databases (§5.1): different
   persons run concurrently; only the same person open twice conflicts.
5. **Embeddings / vector search.** *Answered with a recommendation; please confirm.*
   Use LiteDB 5.0.21 with stored vectors and brute-force cosine now, with Ollama
   `nomic-embed-text` as the default embedding provider. Move to LiteDB 6's native vector
   index when 6.0 is stable. Use HNSW only if a person exceeds about 100k vectors first
   (§5.3).
6. ~~**Raquel's documented-record sources.**~~ **Resolved:** she builds them herself with
   `web_search`/`web_fetch`, then refreshes annually on October 1 (§10.2.1).
7. **Parents and guardians of offspring.** *Partly resolved:* Michael W. Herman is
   guardian (not parent, not contributor) of Lucy and Raquel (§10.0). Still open for
   offspring:
   * Must every offspring have at least one parent, or is guardian-only allowed (the book
     permits it)?
   * Who are the first offspring's parents and guardians?

---

## 17. Tool catalog

These are the tools Lucy, Raquel and their offspring call during conversation, and they
are the **primary implementation surface** of this design (§4, "Tools first"). Each is a
`ToolBase` subclass. A tool takes the calling person's id from `ToolInvocationContext`
(A6), so it always acts *as* that person and never on someone else's records, unless its
"Rules" column says otherwise. Every state-changing tool writes, in one LiteDB
transaction:

* its domain records;
* a provenance record;
* an F_state transition (§6.1).

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
| `revise_memory` | Write | `memory_id`, `kind` (ContraryEvidence, ConfidenceChange or Reinterpretation), `note`, `new_confidence` | revision id | Appends to `memory_revisions` and never edits the original (P2 §6.2). This is how an offspring questions inherited claims. |
| `consolidate_memory` | Write | `episode_ids[]` or a time window | new semantic or procedural memory ids | The person summarizes related episodes. Provenance records `Consolidated(from…)`, and the sources are archived but not deleted. |

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

## Appendix B. Operator decisions, 2026-10-03 (second batch)

The operator's answers to the open questions, verbatim, with where each one is applied.

| # | Operator's answer | Applied in |
|---|---|---|
| Q1 | "Lucy and Raquel are Generation 0 peers but not siblings." | §10.0, §10.2; no lineage edge between them |
| — | "Each persona (superprompt) needs its own separate database(s)." | §5.1 (per-person `person.db` + `memory.db`, operator workspace, cross-person saga, federated lineage); §5.2 collection placement; also resolves Q4 |
| Q5 | "I understand the vector database support is important/helpful/necessary? what is available that is compatible with LiteDB?" | §5.3: survey of compatible options and a recommendation (LiteDB 5 + brute force now; LiteDB 6 native vectors when stable; HNSW fallback; Ollama embeddings). Awaiting confirmation. |
| Q7 (part) | "I, Michael W. Herman, can act as Lucy and Raquel's guardian. I am not their parent nor a contributor." | §10.0 (standing, non-expiring guardianship of the adult founders; their own consent still required) |
| Q6 | Raquel builds her documented knowledge with `web_fetch` "as an initial knowledge database followed by an annual refresh on October 1 of each year." | §10.2.1 (`/knowledge build`, `/knowledge refresh`, October 1 schedule) |
| Q2 | Embodiment and reproductive-function sections heritable: "Yes, same for both..even if one is more complete than the other." | §10.0 (shared loci; incompleteness is not ineligibility), Appendix A.7 |

**Still open:** Q5 confirmation; Q7 for offspring (whether at least one parent is
required, and who parents and guards the first offspring).
