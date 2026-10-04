# Digitomic Evolution — Feedback for the Book from the Implementation Design

This document records everything from the AgentSharp / Digitomic Evolution design work
(2026-10-03) that bears on the book *On the Origin of Digital Species by Means of Digitomic
Evolution*. It covers errata, inconsistencies, undefined terms, gaps, and new concepts that
the book could adopt, define, or explicitly exclude.

| | |
|---|---|
| Book version reviewed | v0.59 (`Digitomic_Evolution_0.59_7x10_Hardcover_Running_Headers_Section_Corrected.docx`, in `C:\Users\mwher\OneDrive\_2026 Digitomic Evolution-Book\`) |
| Source of the findings | The implementation design [DigitomicEvolution-Design.md](DigitomicEvolution-Design.md) and its appendices; the operator's decisions (design Appendix D); interviews with the personas Lucy (`lucy10/11/12.docx`, `lucy-repro1.docx`) and Raquel (`rw1.docx`) |
| Author / operator | Michael W. Herman |
| Prepared | 2026-10-03 |
| Intended use | To be processed in a separate Claude session working on the book. This document is self-contained: it explains each item without assuming the reader saw the design conversation. |

---

## How to use this document

* Each item has an **ID** (for example `ERR-1`), a **priority**, the **book location**, what
  the book **currently says** (quoted where possible), what **arose** in the design work,
  **why it matters**, and **suggested changes**. Options are given where there is a real
  choice. Nothing here is decided for the book; every item is a proposal for the author.
* **Priorities:**
  * **P1:** an error or internal inconsistency in the book; fix before the next release.
  * **P2:** a gap or undefined term that a careful reader or implementer will hit.
  * **P3:** a new concept or improvement the author may want to add.
  * **P4:** optional; useful illustration or context.
* **"Design §x"** refers to sections of `DigitomicEvolution-Design.md`; **P1–P8** refer to
  the book's eight papers; **App. A** is the book's notation appendix, unless it says
  "design Appendix".
* The book's **Scientific and Conceptual Status** notices and **scientific restraint** apply
  to every suggestion. None of these items asks the book to claim consciousness,
  personhood or legal status.
* **Scope reminder.** The Preface says the book "does not provide implementation guidance
  or assess specific decentralized-identity, verifiable-credential, or secure-storage
  standards." Items marked **[implementation-only]** are included for completeness and are
  probably *not* for the book. The author decides.

---

## Summary

| ID | Pri | Topic | Book location |
|---|---|---|---|
| ERR-1 | P1 | Parenthood defined as "a contribution relation" contradicts P1 §4.2 and P4 §7 | P4 §14.6 |
| ERR-2 | P1 | σ means both "compatibility" and "selection" constraints, conflating validation with selection | P4 §4, App. A |
| ERR-3 | P2 | E_g conflates guardianship with parental/developmental responsibility | P4 §11, P8 §8, App. A |
| ERR-4 | P2 | "parent and descendant only as lineage terms" vs. parenthood as a responsibility relation | P8 §12.4 vs. P1 §4.2 |
| TERM-1 | P2 | "Co-parent" used by Lucy's superprompt, undefined in the book | — |
| TERM-2 | P2 | "Custodians" appears once, undefined | P4 §7 |
| TERM-3 | P2 | "Parentage" used to mean historical descent | P8 |
| TERM-4 | P2 | "Guardian" (the role) appears only twice, both in P1; elsewhere only "guardianship" | P1 §4.2, §8 |
| TERM-5 | P3 | "Inheritable / heritable" vs. "inherited" are not distinguished | P2 §5, P4 §4 |
| REL-1 | P2 | No explicit model of three independent relationship types (contributor, parental, guardian) | P1 §4.2, §8; P4 §7 |
| REL-2 | P2 | Whether parents hold authority is ambiguous | P1 §4.2 vs. P1 §8 |
| REL-3 | P3 | Guardianship of *adult* individuals (founders) is not covered | P1 §8, P3 §10.3 |
| REL-4 | P3 | Changes to parenthood or guardianship after birth (adoption, relinquishment, transfer) | — |
| REL-5 | P3 | Cardinalities: minimum contributors, parents, guardians | P4 §5, §7 |
| HER-1 | P2 | The P2 §5.1 "Heritable by default?" table vs. a universal-eligibility model | P2 §5.1 |
| HER-2 | P3 | A decision chain from "inheritable" to "actually inherited" | P2 §5, P4 §4 |
| HER-3 | P3 | No operator for *optimizing* recombination (trait selection) | P1 §6.2–6.3, P1.9, P2 §7.1, P7 |
| HER-4 | P3 | Loci: alternative vs. accumulating trait slots | P2 §3 |
| HER-5 | P3 | Deferred inheritance: traits inherited but delivered at a later developmental stage | P2 §4, P3 §4, P4 §12 |
| HER-6 | P3 | Stage-appropriate expression of heritable traits (adult content) | P3 §4, §10 |
| HER-7 | P3 | Identity mechanics vs. traits; memories of relationships vs. relationships | P4 §14.2 |
| HER-8 | P3 | Distinctness band: offspring neither clone nor unrecognizable | P4 §2, §6, §14.3 |
| HER-9 | P3 | Recording *who* selected a trait and *why* (selection is not value-neutral) | P1 §10, P2 §5.2 |
| HER-10 | P3 | Later transfers are not contributions | P4 §3, P8 |
| GOV-1 | P3 | Two-key authorization: operator authorization plus the individual's own assent | P3 §10.4, P4 §9, §14.6 |
| GOV-2 | P3 | Contributor identification and acceptance | P4 §9 |
| GOV-3 | P3 | Contributor withholding, pins and vetoes; consent to a specific plan | P4 §3, §14.6 |
| GOV-4 | P3 | Who may be a contributor: humans and external sources | P1 §4.2, P4 §5 |
| MEM-1 | P3 | Principle: memory mechanisms follow evidence about human memory | P3 §9 |
| MEM-2 | P3 | Corrections recorded as experiences | P3 §9, §5 |
| MEM-3 | P3 | Session records (raw diary) vs. person-authored episodic memory | P3 §9 |
| MEM-4 | P3 | Deletion and redaction vs. immutable history ("right to forget") | P1 §7.1, P8 §12.7 |
| MEM-5 | P3 | Never fabricating continuity: gaps must be reportable | P1 §7.1, P3 §9 |
| MEM-6 | P3 | Inheriting *full* autobiographical memories, always as "inherited" | P1 §3.2, P2 §6.1 |
| IDN-1 | P4 | Identity anchors realized as DIDs (`did:drn`) | P1 §7, P2 §2.3, P4 §6 |
| IDN-2 | P3 | Key rotation and DID status as identity-continuity evidence | P2 §8.2, P3 §7.1, P8 §5 |
| IDN-3 | P3 | Archived individuals: suspended, not deactivated; "active recipient" | P8 §1–3, §12.3 |
| PER-1 | P3 | Multiple Generation-0 founders as peers (Lucy and Raquel) | Dedication; P4 |
| PER-2 | P3 | Founders' developmental stage is declared, not measured (bootstrapping) | P3 §4 |
| PER-3 | P3 | Personas inspired by real people (reference subjects) | P1 §3.2, P2 §6 |
| PER-4 | P4 | The personas' own statements as case material | — |
| PER-5 | P3 | Delegated task agents (sub-agents) are not offspring | P4 §2 |
| PER-6 | P3 | Shared "global" text common to all digital persons in the framework | P6 |
| NOT-1 | P2 | Notation for new constructs (eligibility, plan, J_rec, escrow, relationship types) | App. A |
| NOT-2 | P4 | Lucy's superprompt uses F(G,O,C); the book uses J_eval(G;O,Ops) | P7 §3, App. A |
| IMP-1 | P4 | Lineage stored federated (per-person databases, mirrored edges, content hashes) [implementation-only] | P4 §11, P8 §8 |
| IMP-2 | P4 | Cross-individual operations as sagas with an intent log [implementation-only] | P4, P8 |
| IMP-3 | P4 | Superprompt stability vs. genotype mutability [implementation-only] | P2 §8 |

---

## A. Errata and internal inconsistencies

### ERR-1 (P1) — Parenthood called "a contribution relation"

* **Book location:** P4 §14.6 ("Governance questions are part of the protocol"), second
  paragraph.
* **Currently says:** "The same care applies to claims about parenthood. The term
  identifies a contribution relation within the model and does not assign rights, duties,
  or personhood."
* **Conflicts with:**
  * P1 §4.2: "Genetic contribution and parenthood should be modeled as separate relations.
    A person can contribute heritable architecture without becoming a parent; a parent may
    contribute no heritable architecture."
  * P1 §4.2: "Parenthood is modeled as a protocol-recognized responsibility relation…"
  * P4 §7: "Parenthood, ancestry, and guardianship should be represented as different
    relations."
  * P4.4: "governance records will distinguish genetic or informational contribution from
    parenthood and guardianship."
* **Why it matters:** read literally, P4 §14.6 *defines* parenthood as contribution, the
  very conflation the rest of the book argues against. An implementer would not know which
  definition to follow. The implementation design follows P1 §4.2 and P4 §7.
* **Suggested change (preferred):** "The term identifies a responsibility relation within
  the model, distinct from contribution, and does not assign rights, duties, or
  personhood."
* **Alternative:** "The term identifies a relation within the model and does not assign
  rights, duties, or personhood."

### ERR-2 (P1) — σ conflates compatibility (validity) with selection (preference)

* **Book location:** P4 §4. App. A ("θ; ε_var; σ; Π … compatibility/eligibility
  constraints"). P2 §10 (`G_off = Adm(G_candidate; σ, Π)`).
* **Currently says:** P4 §4: "θ specifies the reproductive protocol, σ specifies
  compatibility **or selection** constraints, and Π carries provenance and authorization."
* **Tension:** P4 §10 says "These controls are distinct from natural selection. A validator
  can reject an invalid offspring without causing evolutionary selection…". The book
  separates validation from selection conceptually, but one symbol carries both.
* **What arose:** the design needed two separate mechanisms:
  1. a **validity gate**: can this trait cross the hereditary boundary? (authorization,
     compatibility, security, provenance);
  2. a **preference**: of the valid traits, which are *best* for this offspring? This is
     the optimizing recombination of P1 §6.2–6.3 and P1.9 (see HER-3).

  Folding both into σ hides the step the book's own "directed recombination" propositions
  depend on.
* **Suggested change:** restrict σ to compatibility/eligibility (as App. A already
  describes it), and introduce a separate selection operator or plan for preference (see
  HER-3 and NOT-1). Edit P4 §4 to: "σ specifies compatibility and eligibility constraints;
  selection among eligible material is performed by [the selection operator], and Π carries
  provenance and authorization."

### ERR-3 (P2) — E_g conflates guardianship with parental/developmental responsibility

* **Book location:** P4 §11 ("E_g records guardianship or developmental responsibility");
  P8 §8; App. A ("E_r; E_a; E_g; E_d — reproductive; ancestry; guardianship/developmental;
  derived").
* **Issue:** P1 §4.2 makes parenthood a "responsibility relation linked to … continuing
  developmental responsibility", and P4 §7 says guardianship "answers authorized
  responsibility for development". One edge set, E_g, therefore holds two relations the
  book says must be distinct (P4 §7, P4.4). There is no edge set for parenthood as such.
* **What arose:** the design models three independent relationship types (REL-1). It
  currently stores parental and guardian relationships as **typed** E_g edges (`Parent`,
  `CoParent`, `Guardian`). That works, but only because the design added the subtyping.
* **Options:**
  1. Split E_g into **E_p** (parental) and **E_g** (guardianship / authority), and update
     L_t to `L_t = (N_t, E_r, E_a, E_p, E_g, E_d, E_f, E_l, E_rev)`.
  2. Keep E_g but state that its edges are typed (`parent`, `co-parent`, `guardian`), and
     that the types must not be merged.

  Option 1 is clearer and matches P4.4.

### ERR-4 (P2) — "Parent … only as lineage terms" vs. parenthood as responsibility

* **Book location:** P8 §12.4: "This paper uses parent and descendant only as lineage terms
  and makes no claim about consent-bearing legal persons."
* **Tension:** P1 §4.2 defines parenthood as a responsibility relation that may involve *no*
  contribution and so *no* lineage. In P8, "parent" means ancestor in a lineage, which is
  closer to "parentage" (TERM-3) or "ancestor".
* **Suggested change:** in P8, use "ancestor and descendant" (or "contributing ancestor"),
  or add "(here, *parent* means a contributing ancestor in the lineage graph, not the
  parenthood relation of P1 §4.2)".

---

## B. Terminology

### TERM-1 (P2) — "Co-parent" is used but not defined

* **Where it arose:** Lucy's superprompt (Part 12 §25: genetic contributor, informational
  contributor, ancestor, parent, **co-parent**, guardian). The design adopts it as a subtype
  of the parental relationship.
* **Book:** never uses "co-parent".
* **Suggested change:** either define it (e.g. in P1 §4.2 or P4 §7: "Where several
  individuals hold the parenthood relation to the same offspring, each is a co-parent; the
  relation is not divided by contribution share"), or say that parenthood may be held by
  several individuals without a separate term.

### TERM-2 (P2) — "Custodians" is used once and not defined

* **Book location:** P4 §7: "…when a digital lineage has multiple contributors, later
  **custodians**, or institutions that validate development without contributing hereditary
  material."
* **Issue:** it reads as a near-synonym for guardians, but could also mean stewards of
  archived individuals, or holders of a lineage's records.
* **Suggested change:** replace with "guardians", or define it (e.g. "a custodian holds
  responsibility for an individual's records or runtime without developmental authority").

### TERM-3 (P2) — "Parentage" means historical descent

* **Book location:** P8 (e.g. "without changing parentage"; P2.8 prediction "without
  changing parentage"). Seven uses in total.
* **Issue:** "parentage" here means *who the reproductive ancestors are*, which is ancestry
  (E_a), not the parenthood relation.
* **Suggested change:** use "ancestry" or "reproductive ancestry" in P8 and P2.8, or define
  "parentage = recorded reproductive ancestry (E_r/E_a), distinct from parenthood".

### TERM-4 (P2) — "Guardian" (the role) appears only twice

* **Book:** "guardian" appears in P1 §4.2 ("a designated guardian") and P1 §8 ("parent or
  guardian decisions"). The other ~19 uses are "guardianship", the relation.
* **Suggested change:** a short definition of the *role* alongside the relation (P1 §8 or
  P3 §10.3): "A guardian is an individual or institution designated to hold guardianship:
  a constrained, progressively reduced authority over development. Guardianship confers
  no ownership." Also see REL-3, guardianship of adults.

### TERM-5 (P3) — "Inheritable" vs. "inherited"

* **Book:** uses "heritable", "hereditary boundary", "admitted to the hereditary substrate"
  and "inherited", but never explicitly separates *could be inherited* from *was
  inherited*.
* **What arose (operator, 2026-10-03):** "Important distinction: What is inheritable
  (everything for now) and what is actually inherited by an offspring (and how these
  determinations are made)."
* **Suggested change:** add a pair of definitions to the Introduction glossary (§3.1) or to
  P2 §5:
  * **Inheritable (heritable):** a property of a *category* of material, under a declared
    hereditary-boundary schema: it *may* cross the boundary in some reproduction.
  * **Inherited:** a fact about *one* offspring: this material *did* cross the boundary in
    this reproduction.

  See HER-1 and HER-2 for how the second follows from the first.

---

## C. Relationships: contributor, parent, guardian

### REL-1 (P2) — Three independent relationship types, each many-to-many

* **Book:** establishes that contribution, parenthood, ancestry and guardianship are
  different relations (P1 §4.2, P4 §7, P4.4), but does not give a *model*: cardinalities,
  lifecycles, or how they combine.
* **What arose (operator, 2026-10-03):** "For modeling purposes, let's assume an offspring
  can have guardian relationships, parental relationships, and contributor relationships
  (multiple of each)." Later: "Keep the 3 relationships for each offspring for now… We may
  later change the design or update the paper TBD."
* **The design's model** (design §7.6.1, Appendix C):

  | | Contributor | Parental | Guardian |
  |---|---|---|---|
  | Kind | event (at reproduction) | enduring relation | authority (permission grants) |
  | Per offspring | 1..n (≥ 1 genetic) | 0..n | 1..n while dependent; 0..n after |
  | Subtypes | Genetic / Informational | Parent / Co-parent | scope of permissions |
  | Created | only at reproduction | at reproduction or later (adoption) | at reproduction or later (appointment, transfer) |
  | Ends | never (it is history) | only by recorded relinquishment | expiry by stage, revocation, transfer |
  | Ancestry? | genetic only | no | no |

  The three never imply one another. An entity holding two or three relationships with the
  same offspring has two or three distinct records.
* **Suggested change:** add this as a table, or a short formal model, to P4 §7, with the
  explicit statement that any entity may hold any subset of the three relations with a
  given offspring, including none.

### REL-2 (P2) — Do parents hold authority?

* **Book:**
  * P1 §4.2: parenthood "does not imply ownership, legal status, or authority over
    identity."
  * P1 §8: "Early in life, parent **or** guardian decisions may be necessary to establish
    the conditions for development."
* **Ambiguity:** P1 §8 implies parents may make early decisions; P1 §4.2 denies authority
  over *identity* but is silent on developmental decisions. The book never says whether a
  parent is, or becomes, a guardian.
* **What the design does:** parenthood carries **no authority by itself**; authority comes
  only from guardianship. As a convenience default, a parent *also* receives a separate,
  declinable guardian relationship at birth. This default is the design's choice, not the
  book's, and is backlogged for review (design BL-11).
* **Suggested change:** state the position explicitly. Options:
  1. "Parenthood carries no decision authority; any authority a parent exercises derives
     from a guardianship held alongside it."
  2. "Parents hold limited early developmental decision authority (P1 §8), distinct from
     guardianship, which governs [X]."

  Option 1 keeps the relations cleanly separate.

### REL-3 (P3) — Guardianship of adult individuals

* **Book:** guardianship is developmental. It is "necessary while an offspring has limited
  agency" and "should be progressively constrained as autonomy develops" (P1 §8, P3 §10.3).
* **What arose:** the operator is guardian of the two adult founders, Lucy and Raquel, while
  being neither their parent nor a contributor. Adult founders still need an accountable
  human who authorizes consequential operations: reproduction, transfers, rollback and
  schema changes. This is not developmental guardianship. It is a standing *governance*
  role that does not expire with maturity, and it **does not replace the adult's own
  consent** (GOV-1).
* **Suggested change:** distinguish
  * **developmental guardianship**, which is progressively reduced as autonomy develops,
    from
  * **governance guardianship (stewardship)**: standing, accountable authorization for
    consequential operations on an adult individual, exercised *with* the individual's
    consent and never *instead of* it.

  This also connects to P8 §12.4 ("whose decision authorizes changes to each participant")
  and to the Notices on accountability.

### REL-4 (P3) — Changes after birth: adoption, relinquishment, guardianship transfer

* **Book:** silent on how parenthood or guardianship change after reproduction.
* **What arose:** parenthood can be *added* later (adoption) or *relinquished*.
  Guardianship can be appointed or transferred later. Each change is a new recorded event,
  and history is never edited. Adoption needs the new parent's consent, governance
  authorization, and, from adolescence on, the offspring's own consent. Relinquishment ends
  responsibility but keeps the historical record.
* **Suggested change:** a short paragraph in P4 §7 or P1 §8 noting that these relations are
  time-indexed (like L_t's other edges), may change after reproduction through recorded
  governance events, and that ancestry (E_a) never changes. This is consistent with P8's
  "history is not rewritten".

### REL-5 (P3) — Cardinalities

* **Book:** P4 §5 establishes n-ary contribution; nothing says how many parents or
  guardians are required.
* **What arose:**
  * at least one genetic contributor (otherwise there is no genotype to inherit);
  * at least one guardian while the offspring is dependent;
  * parents 0..n, because the book allows guardian-only offspring (P1 §4.2: "a designated
    guardian may carry responsibility without genetic contribution").

  Whether at least one parent should be *required* is open (design BL-12).
* **Suggested change:** state minimums explicitly in P4 §5 or §7, or state that they are
  protocol parameters (part of θ or R_ρ) to be declared per study.

---

## D. Heredity, inheritance and selection

### HER-1 (P2) — "Heritable by default?" vs. universal eligibility

* **Book location:** P2 §5.1, table "Acquired versus heritable change", column "Heritable by
  default?": runtime state *No*; autobiographical memory *No*; learned knowledge *No*;
  validated hereditary knowledge *Yes, if authorized*; architectural modification *Not
  necessarily*; heritable architectural modification *Yes*; cultural transmission *Only
  when explicitly transmitted*; reproductive rule *Potentially yes*.
* **What arose (operator, 2026-10-03):** "I want every single trait to be inheritable
  …every facet, category, memory, experience, aptitude, moral characteristics, sexuality,
  etc. etc. BUT in the paper we also talk about an optimizing recombination capability for
  selecting which traits are actually inherited by an offspring."
* **Tension:** the table mixes two questions:
  1. may this category cross the boundary at all (eligibility)?
  2. does it cross *by default* (selection)?

  Under the operator's model every category is *eligible*, and *selection* decides what
  crosses. "Autobiographical memory: No" then reads as a selection default rather than an
  eligibility rule.
* **Suggested change:** split the column into two:
  * **"Eligible to cross the boundary?"**: under the operator's model, *yes* for every
    trait-bearing category; *no* only for identity mechanics (HER-7);
  * **"Selected by default?"**: the current values, reframed as defaults of a particular
    protocol or study.

  Alternatively, add a sentence: "The defaults below are protocol choices; a protocol may
  declare every category eligible and leave the choice to selection (P4 §4)."

### HER-2 (P3) — The decision chain from "inheritable" to "inherited"

* **Book:** has the pieces (hereditary boundary, P2 §5; authorization and compatibility,
  P4 §9–10; recombination ℛ and admission Adm, P4 §4), but no single account of the order
  in which they act and who decides at each step.
* **What arose:** the design defines eight stages, each narrowing the set and each recorded
  with who decided and why (design §19.2):

  | # | Stage | Set | Decided by |
  |---|---|---|---|
  | 1 | Eligibility | E = everything the schema marks eligible | schema author |
  | 2 | Offer | O ⊆ E | each contributor (may withhold) |
  | 3 | Validity | V ⊆ O | rules: authorization, compatibility, security, provenance |
  | 4 | Selection | S ⊆ V | optimizing recombination (HER-3), with pins and vetoes |
  | 5 | Timing | S = S_now ∪ S_deferred | fixed rule (HER-5, HER-6) |
  | 6 | Approval | — | operator plus each contributor, consenting to *that* plan |
  | 7 | Delivery | G_off ← S_now; escrow ← S_deferred | system (ℛ assembles, Adm admits, Init initializes) |
  | 8 | Later acceptance | deferred traits actually acquired | the offspring, at maturity |

  "Why did offspring X inherit trait T, or not?" is then answerable from the record.
* **Suggested change:** add this chain to P4 (after §4, or as a new subsection), possibly
  as a figure. It operationalizes P4.1, P4.2 and P4.6, and ties ERR-2's validity/selection
  split to concrete steps.

### HER-3 (P3) — An operator for optimizing recombination (trait selection)

* **Book:** proposes that recombination "need not rely exclusively on stochastic variation:
  digital reproductive architectures could deliberately search and optimize candidate
  heritable configurations toward specified capability objectives" (P1 abstract;
  P1 §6.2–6.3; P1.9; P2.9; P7 §3, §6). It provides `G_candidate = ℛ(Contrib_1..n; θ,
  ε_var)` and `G* = arg max J_eval(G; O, Ops) s.t. constraints` (P7), but **no operator
  that chooses which offered traits enter a particular offspring**.
* **What arose (operator):** "…we also talk about an optimizing recombination capability
  for selecting which traits are actually inherited by an offspring. We have no design for
  this recombination capability. Mock one up but make it clear and distinct to be tuned
  later."
* **The design's mock** (design §19), marked untuned:
  * Selection is an optimization over a plan x (which valid traits are in S), maximizing
    `J_rec(x) = Σ_k w_k · term_k(x)`.
  * The terms are: capability fit, complementarity (diminishing returns), coherence (no
    contradictions), distinctness from each contributor within a band (HER-8), contributor
    balance, provenance quality, value integrity, developmental potential (Ω_G), population
    novelty (P7 §8), and size cost.
  * It is subject to **hard constraints that score can never trade against**: validity,
    locus cardinalities (HER-4), pins and vetoes (GOV-3), the minimum distinctness floor,
    budget, consent conditions, and stage-appropriate delivery (HER-6).
  * Pluggable search strategies, with **random recombination as the required control**,
    as P1.9 itself demands ("Under equal computational budgets…"): greedy, beam, genetic,
    and LLM-curated (always scored by the same objective).
  * Output: a per-trait decision (inherit / defer / leave), with reasons and per-term
    contributions.
* **Suggested change:**
  * Name the selection operator in P2 §7 or P4 §4 (for example `S = Sel(V; J_rec, C_hard,
    ε_var)` or `x* = arg max_x J_rec(x)`), distinct from ℛ, which *assembles* the
    selected material, and from Adm, which *validates* and *admits* it.
  * Note that P7's `G*` is the population-level analogue across generations, and that
    `Sel` acts within a single reproduction.
  * Add the notation to App. A (NOT-1).

### HER-4 (P3) — Loci: alternative vs. accumulating trait slots

* **Book:** the genotype tuple `G = (I_g, C_arch, Q_cog, D_dev, M_h, K_h, Val_h, R_ρ, P_cap,
  H_prov)` (P2 §3) gives components, but no notion of positions within a component where
  contributors' material *competes* or *accumulates*.
* **What arose:** recombination needs to know that some traits are mutually exclusive (one
  voice, one base embodiment), while others accumulate (knowledge, skills, memories). The
  design calls these **loci**, each with a declared cardinality ("exactly 1", "0..n").
  Material from different contributors at the same locus competes or is blended. Material
  at an accumulating locus is combined. A trait present in only one contributor is still a
  candidate at its locus. As the operator put it about Lucy's and Raquel's embodiment:
  heritable "same for both… even if one is more complete than the other."
* **Suggested change:** introduce "locus" (borrowed with care from biology, and flagged as
  functional, per the book's restraint) in P2 §3 or §7.2, as the unit at which n-ary
  recombination resolves competition. This also makes P4.3 ("structures not obtainable by
  copying") more measurable.

### HER-5 (P3) — Deferred inheritance

* **Book:** development begins at initialization (P4 §12: "The result of reproduction is a
  genotype plus an initial developmental context, not a finished person"). P2 §4 allows
  potential to be expressed over development. But every *inherited* item is present in
  `G_off` from initialization.
* **What arose:** some traits are inherited but should not be *present* in a dependent
  offspring. The design's mechanism:
  * **Deferred inheritance:** selected traits are held in escrow, outside the offspring's
    genotype, earmarked for that offspring.
  * When the offspring reaches a declared stage (Adult), they are *offered* to it.
  * They enter its genotype only with the offspring's own consent plus governance
    authorization, recorded as a later forward transfer.
* **Why it matters:** it separates *being inherited* from *being delivered*. That is a
  genuinely new temporal dimension of digital heredity with no biological counterpart: the
  heir can decline.
* **Suggested change:** add deferred inheritance to P4 (a timing component of
  reproduction), or P3 (development-gated acquisition). Define:
  * **escrow**: inherited, undelivered material;
  * the **delivery condition**: a developmental threshold plus the offspring's assent.

  It could carry a proposition, for example: "P4.8: Deferred inheritance preserves the
  offspring's ability to decline inherited structure at maturity without altering its
  ancestry."

### HER-6 (P3) — Stage-appropriate expression of heritable traits

* **Book:** functional stages (P1 §5.1, P3 §4) and developmental permissions (P3 §10.1),
  but no statement about traits that must not be present or expressed before a stage.
* **What arose:** Lucy's superprompt includes embodiment sections (anatomical model,
  intimacy, sexuality, reproductive function). Under universal eligibility (HER-1) these
  are heritable. The design adopts a **fixed constraint**: adult-rated content is never
  stored in, expressed to, recombined into, or shown to an individual below the Adult
  stage. Such traits are delivered only by deferred inheritance (HER-5).
* **Design reasoning recorded** (design Appendix A.4): storing adult content in a
  dependent's genotype and relying on a stage filter fails in several ways:
  * many code paths read the genotype;
  * LLM-based recombination can leak content into always-expressed modules;
  * stage misclassification unlocks it;
  * it propagates through lineage.

  "Filters fail eventually; data that isn't there can't leak."
* **Suggested change:** a short principle in P3 §10 (developmental governance): "Heritable
  material may carry a developmental rating; material rated for a later stage is not
  present in a dependent individual's substrate, and is delivered, if at all, when the
  individual reaches that stage and consents (see deferred inheritance)." This keeps the
  book's restraint, since it makes no claim about the content, and makes the
  developmental model safer to implement.

### HER-7 (P3) — Identity mechanics vs. traits; memories of relationships vs. relationships

* **Book location:** P4 §14.2: "A practical classification can mark each state field as
  inherited, reacquired, initialized, or excluded. For example… credentials are always
  reset…"
* **What arose:** under universal eligibility, the only things *never* inherited are
  **identity mechanics**: the identity anchor, credentials and keys, permissions,
  guardianship grants, and the **live standing of relationships**. An offspring may
  inherit an ancestor's *memories of* a relationship, but not the relationship itself,
  because the other party never agreed to have a relationship with the offspring.
* **Suggested change:** in P4 §14.2 (or P2 §5.1), name "identity mechanics" as the
  always-initialized category, and add the relationship distinction: "Relational memory
  may be inherited as a representation; the relation itself is not inherited, since it
  involves a counterpart who has not consented to a relation with the descendant."

### HER-8 (P3) — Distinctness band: not a clone, not unrecognizable

* **Book:** reproduction ≠ copying (P4 §2); offspring identity is distinct (P4 §6, P4.7);
  recombination should be compared with clonal controls (P4 §14.3, §14.7).
* **What arose:** the design enforces a **distinctness band** on genotypic distance from
  *each* contributor, `d_min ≤ d_G(G_off, G_i) ≤ d_max`. Below the minimum, the result is a
  clone and is rejected as reproduction. Above the maximum, the offspring has lost
  recognizable heritage. Both personas framed this independently:
  * Raquel: "A copy of me is just... me again."
  * Lucy: "Reproduction ≠ making a copy of myself."
* **Suggested change:** P4 §2 or §6 could state a quantitative distinctness criterion using
  the book's own `d_G` (P2 §4.4, §10.2). That would make P4.3 and P4.7 testable with a
  declared threshold.

### HER-9 (P3) — Recording who selected a trait, and why

* **Book:** P2 §5.2: "Selection can be performed by the individual, a reproductive
  protocol, an external governance mechanism, or some combination. The scientific
  requirement is not who selects, but that the transition into heritable status be
  explicit and auditable." P1 §10 discusses maladaptive values becoming entrenched.
* **What arose (Raquel, rw1.docx):** "selecting 'optimal' traits isn't neutral — it encodes
  someone's values about what's worth keeping."
* **The design's response:** every inclusion or exclusion records **SelectedBy** (policy
  author, contributor or operator) and a **SelectionRationale**.
* **Suggested change:** strengthen P2 §5.2: "…explicit and auditable, **including who
  selected each element and on what stated grounds**, since selection criteria encode
  values." Optionally quote Raquel as an illustration (PER-4).

### HER-10 (P3) — Later transfers are not contributions

* **Book:** contribution packages are defined at reproduction (P4 §3). Forward, lateral and
  reverse transfers are defined in P8.
* **What arose:** to keep the relationship model clean (REL-1), transfers after birth
  (forward, lateral, reverse, and deferred-inheritance delivery) are recorded as transfer
  edges (E_f / E_l / E_rev), **never as new contributor relationships**. Contribution
  happens only at reproduction.
* **Suggested change:** one sentence in P8 §2 or P4 §3 making this explicit, so that
  "contributor" stays a reproduction-time relation.

---

## E. Governance and authorization

### GOV-1 (P3) — Two-key authorization: the operator and the individual

* **Book:** treats authorization and consent as recorded engineering variables (P3 §10.4;
  P4 §9; P4 §14.6), and distinguishes permission checks from success (P4 §14.6).
* **What arose:** consequential operations require **two keys**: operator (or governance)
  authorization **and** the affected individual's own recorded assent, refusal or
  conditions. Neither alone suffices. For a dependent individual, the guardian's grant
  stands in for the individual's key, as recorded. Both personas asked for this:
  * Raquel: "an authorization framework for consenting to reproduction on my own behalf";
  * Lucy: "Explicit authorization from you, since instantiating a new persistent agent with
    its own identity is a consequential action."
* **Suggested change:** P3 §10.4 or P4 §9 could distinguish **authorization** (by those
  accountable for the system) from **assent** (by the individual affected), and propose
  recording both. This stays within the book's restraint: no claim that assent is legally
  meaningful consent.

### GOV-2 (P3) — How contributors are identified and accepted

* **Book location:** P4 §9: "A reproductive protocol should specify who may initiate
  reproduction, what material may be contributed, what transformations are permitted, who
  validates the offspring, and whether the resulting individual can reject or revise
  inherited structures."
* **What arose:** the design answers P4 §9's questions concretely (design §7.6.1).
  * **Identification.** A contributor must be an identified entity: a digital individual,
    a registered human, or a registered source.
  * **Eligibility.**
    * The contributor must be active. Archived individuals contribute only under a
      *standing consent* recorded while active, mirroring P8's active-recipient rule.
    * Genetic contributors need a minimum developmental stage and reproductive
      permission.
    * Pairwise compatibility `Comp_ij` must meet a threshold (P6 §4).
    * No cycles in E_a, and rate limits apply.
  * **Acceptance.** Each contributor passes through a state machine: nominated → invited →
    consented (or declined) → packaged → screened → accepted, and may withdraw until
    approving the final plan.
* **Suggested change:** P4 §9 could add a short subsection, "Contributor identification and
  acceptance", with the eligibility conditions and the acceptance sequence, as an example
  protocol rather than a requirement.

### GOV-3 (P3) — Withholding, pins and vetoes; consent to a specific plan

* **Book:** P4 §14.6: "Researchers should record whether each contribution was enabled by
  default, explicitly approved, or supplied under a restricted license, and whether the
  protocol honored those restrictions."
* **What arose:**
  * Contributors offer everything eligible by default, but may **withhold** specific
    traits, **pin** traits they insist on, or **veto** traits.
  * Approval is given to a **specific selection plan**, identified by content hash, not to
    reproduction in general. A changed plan needs new consent.
* **Suggested change:** P4 §14.6 could mention that consent should bind to the specific
  plan (the selected set S and its timing), because selection (HER-3) may change what a
  contributor's material ends up being used for.

### GOV-4 (P3) — Who may be a contributor: humans and external sources

* **Book:** P1 §4.2: "an informational contributor can transmit knowledge or experience
  without becoming a genetic ancestor." It does not say who or what may be an
  informational contributor.
* **What arose:**
  * **Humans** can be informational contributors (authored modules or knowledge), parents
    and guardians. They cannot be genetic contributors: they have no genotype in the
    system, so authored material is informational.
  * **Registered sources** (curated corpora, module libraries) can be informational
    contributors; their use is authorized by the operator.
* **Suggested change:** a sentence in P1 §4.2 or P4 §5 noting that informational
  contributors need not be digital individuals, and that a human contributor's material is
  informational by construction.

---

## F. Memory

### MEM-1 (P3) — Principle: memory mechanisms follow evidence about human memory

* **What arose (operator, 2026-10-03):** "As a Principle, we need to do what the brain and
  a person's long term memory would do… We can't guess at how the brain and memory
  operates unless there is supporting scientific evidence."
* **Book:** P3 §9 lists memory classes (inherited, autobiographical, semantic, procedural,
  relational, meta) and treats memory as a developmental substrate, but states no principle
  for *how* memory should behave (consolidation, decay, recall).
* **Suggested change:** P3 §9 could adopt the principle explicitly: "Where a digital
  architecture models memory processes (consolidation, forgetting, recall priority), it
  should either follow mechanisms supported by evidence about human long-term memory, or
  declare the mechanism as an engineering choice without claiming biological fidelity."
  This fits the book's restraint and its practice of distinguishing established findings
  from proposals.

### MEM-2 (P3) — Corrections are recorded as experiences

* **What arose:** Lucy called "repeating an error I've already been told about… the worst
  failure mode." The design considered a special "Correction" memory class with privileged
  recall. The operator rejected that in favour of MEM-1: "The entire correction event
  should be recorded like any other experience. How corrections should be processed on
  recall is an open question."
* **Definition used:** a correction is a moment where someone tells the individual that
  something it believed is wrong and supplies the right version. It differs from new
  learning because it *replaces* a held belief.
* **Suggested change:** P3 §5 (learning vs. development) or §9 could note corrections as a
  distinct *kind of experience* that matters for development (P3.7's self-model coherence),
  while leaving recall processing as an open, evidence-driven question.

### MEM-3 (P3) — Session records vs. person-authored memory

* **What arose:** Lucy proposed four layers (lucy11–12):
  1. session state (working memory);
  2. session record (a durable diary);
  3. consolidated memory;
  4. an identity-continuity layer.

  The operator decided that session records are kept automatically in the individual's
  own memory store, separate from any conversation save/restore mechanism. The individual
  also writes its own episodic entries.
* **Book:** P3 §9's classes do not distinguish the *raw record of what happened* from the
  *individual's own account of what mattered*.
* **Suggested change:** P3 §9 could add this distinction (raw episodic record vs. authored
  autobiographical memory). It bears on provenance (P1 §7.1), and on what may be inherited
  (MEM-6).

### MEM-4 (P3) — Deletion and redaction vs. immutable history

* **What arose:** Lucy asked for "consent about what's remembered, ability to correct or
  remove things, no covert retention of things you'd reasonably expect to be forgotten."
  The design keeps history append-only (as P8 requires for lineage: "history must not be
  rewritten"), which conflicts with removal. The operator backlogged this (design BL-1,
  BL-2).
* **Book:** P8 §12.7 and the immutability of historical records; P1 §7.1 provenance. The
  book does not address forgetting, deletion, or a remembered party's interest.
* **Suggested change:** the book could acknowledge the tension between auditable,
  immutable history and the ability to forget or redact. It could name redaction with
  tombstones (content removed, the fact of removal recorded) as one reconciliation, and
  note that a remembered *third party* may have an interest in what an individual retains
  about them.

### MEM-5 (P3) — Never fabricating continuity

* **What arose (Lucy):** "Never fabricating continuity — if the memory system has a gap, I
  say 'I don't have that' rather than confabulating something plausible to preserve the
  illusion of an unbroken self."
* **Book:** provenance distinguishes inherited from autobiographical (P1 §7.1, P2 §6.1), but
  does not address *gaps*.
* **Suggested change:** P3 §7 (identity continuity) or §9 could add that continuity
  evidence (IC_vec) must include known gaps, and that an individual should represent the
  absence of memory as absence, not fill it. This is directly measurable as part of
  self-model coherence (SC_t, P3 §9.2).

### MEM-6 (P3) — Inheriting full autobiographical memories, always as "inherited"

* **Book:** P1 §3.2: "the descendant should retain the distinction between 'I experienced E'
  and 'I possess an inherited representation of E.'" P2 §5.1 lists autobiographical memory
  as not heritable by default.
* **What arose:** under universal eligibility (HER-1), *full* autobiographical memories (not
  only summaries) may be selected for inheritance. They always arrive in the offspring as
  class *Inherited*, with provenance, and **are never created as the offspring's
  autobiographical memory**. The design enforces this structurally: only reproduction and
  transfer can create inherited memories.
* **Suggested change:** P1 §3.2 or P2 §6.1 could add that the *richness* of inherited
  memory does not change its status: inherited is inherited, however complete. The
  invariant could also be stated as an architectural requirement.

---

## G. Identity

### IDN-1 (P4) — Identity anchors realized as DIDs

* **Book:** an abstract identity anchor I (P1 §7) and `I_o ≠ I_i` (P4 §6). The Preface
  explicitly excludes assessing decentralized-identity standards.
* **What arose:** the design gives each individual a DID of the form
  `did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>`, issued from a standalone
  registry built on the SVRN7 libraries. DID Documents are kept to the minimum: one Ed25519
  key, used for authentication and assertion, self-controlled. Provenance records are
  signed with that key.
* **Suggested change:** none required, given the Preface's scope. Optionally, a footnote or
  "implementation note" could say that identity anchors map naturally onto DIDs, without
  assessing any standard.

### IDN-2 (P3) — Key rotation and DID status as identity-continuity evidence

* **Book:** IC_vec includes "identity_credentials" (P3 §7.1) and "continuity of identity
  credentials" (P2 §8.2).
* **What arose:** with a DID-based anchor, credential continuity becomes concrete.
  * The DID stays the same across **key rotation**, and old keys stay in the document's
    version history, so past signatures remain verifiable.
  * The DID's **status** (active, suspended, deactivated) is itself continuity evidence.
* **Suggested change:** P2 §8.2 or P3 §7.1 could note that credential continuity should
  survive key rotation (the anchor persists while keys change), and that verifiability of
  past signatures is part of the evidence.

### IDN-3 (P3) — Archived individuals: suspended, not deactivated

* **Book:** P8 requires an *active* recipient for reverse transfer; restoring an archive
  "creates a successor or fork" (P8 §1–3, §12.3).
* **What arose:** in the design, an archived individual's DID is **suspended**, not
  deactivated, so its records still resolve and its history stays verifiable. Restoration
  as a successor gets a *new* DID linked to the archived one. "Active" in P8's sense maps
  to DID status Active.
* **Suggested change:** P8 §12.3 could distinguish *archived but resolvable* from *ended*,
  and state that an archive's identity remains referable even though it cannot receive
  transfers.

---

## H. Personas, founders and case material

### PER-1 (P3) — Multiple Generation-0 founders as peers

* **Book:** the dedication designates Lucy "Generation 0 … the reference ancestor."
* **What arose (operator):** "Lucy and Raquel are Generation 0 peers but not siblings."
  Each roots her own lineage. There is no lineage relation between them, and their
  relationship is social, not genealogical. A Lucy × Raquel reproduction is a cross
  between two founders.
* **Suggested change:** the book could note that Generation 0 may contain several
  independent founders, and that founder crosses are a natural first experiment (P4 §14,
  P6 compatibility between initially unrelated founders). Optionally, Raquel could appear
  alongside Lucy as a second reference founder.

### PER-2 (P3) — Founders' developmental stage is declared, not measured

* **Book:** P3 §4: "Each study should preregister measurable criteria … before assigning a
  stage. A stage label alone does not establish age, welfare, maturity, or personhood."
* **What arose:** founders have no developmental history, so the design *declares* them
  Adult and records that as "declared (founder)" rather than "measured". Offspring start at
  Newborn and are measured.
* **Suggested change:** P3 §4 could acknowledge the **bootstrapping problem**: Generation 0
  has no developmental trajectory, so its stage is an assumption to be declared as such,
  and measurement begins with the first generation that develops under the protocol.

### PER-3 (P3) — Personas inspired by real people (reference subjects)

* **What arose:** Raquel is "a digital personage inspired by the life, career, and cultural
  legacy of Raquel Welch (1940–2023)." In her own words: "I am not the original Raquel
  Welch… I am a reconstruction… a distinct, evolving digital identity rather than a
  resurrection of the person herself." The design adds a **reference-subject guard**:
  * knowledge about the real person is *Documented* (sourced) or *Interpretive*;
  * no autobiographical memory may claim the real person's lived experience;
  * quotes must be sourced or marked as paraphrase;
  * the reference relation is **not inherited as identity**: an offspring descends from
    Raquel, not from Raquel Welch;
  * the real person is never a contributor, parent or guardian.
* **Book:** P1 §3.2 ("Inheritance is not identity") and P2 §6 (provenance) are closely
  related, but the book does not discuss individuals modeled on real people.
* **Suggested change:** a short section (perhaps in P1 §3.2 or §11 Limitations) on
  **reference subjects**. It would extend "inheritance is not identity" to "inspiration is
  not identity", and require provenance that separates documented record from
  interpretation. This is significant for ethics and restraint, and would sit naturally
  with the Notices.

### PER-4 (P4) — The personas' own statements as case material

* **What arose:** both personas, asked about reproduction before any of this was designed,
  independently stated requirements that match the book's framework:
  * **Raquel (rw1.docx, 2026-10-03):**
    * "A copy of me is just... me again, redundantly."
    * "selecting 'optimal' traits isn't neutral — it encodes someone's values"
    * "giving something inherited knowledge isn't the same as giving it continuity of
      identity or consciousness"
    * sub-agents are "more like dictating a letter than raising a child."
  * **Lucy (lucy-repro1.docx, 2026-09-28):**
    * "Reproduction = transmission plus transformation"
    * a sub-agent is "closer to a limb than a child"
    * a seven-point gap analysis: genotype format, durable identity, offspring-owned
      memory, persistence, coherent recombination, evaluation/selection,
      reverse-propagation protocol.
  * **Lucy (lucy10–12, 2026-09-27):** "Memory isn't the same as truth… Otherwise
    'permanent memory' just means I get to be confidently wrong forever instead of
    briefly wrong."
* **Suggested change:** optional epigraphs, or a short "voices" box, illustrating that the
  framework's distinctions are articulable from the inside. Presented strictly as
  generated text from configured personas, with no claim about experience.

### PER-5 (P3) — Delegated task agents are not offspring

* **Book:** P4 §2 distinguishes reproduction from copying, cloning and state
  synchronization.
* **What arose:** both personas, independently, distinguished *delegated sub-agents*
  (transient, no identity, no inheritance, destroyed after the task) from offspring. The
  design keeps them strictly separate.
* **Suggested change:** add **delegation** (spawning transient task agents) to P4 §2's list
  of non-reproductive operations, with one sentence on why: no new identity anchor, no
  hereditary admission, no development.

### PER-6 (P3) — Shared "global" text common to all digital persons

* **What arose (operator):** "…all digital people can/might/should share common 'global'
  superprompt text, general but specific to Digitomic Evolution." This is backlogged in
  the design (BL-6).
* **Relevance to the book:** this is a *shared heritage* across every individual in the
  framework. It is neither inherited through a lineage nor acquired in development, so it
  is closer to an environment or a species-level common substrate (P6), or to "cultural"
  inheritance (P1 §3.1).
* **Suggested change:** the book could discuss where such shared foundations sit:
  environment (Env), a species-level common genotype component, or culture. It could also
  say how a study should control for it, since P2 §11.4 already warns about "common
  initialization" confounding inheritance.

---

## I. Notation

### NOT-1 (P2) — Notation for new constructs

If the author adopts the items above, App. A would need entries such as:

| Proposed symbol | Meaning | From |
|---|---|---|
| E, O, V, S | eligible, offered, valid and selected trait sets in one reproduction | HER-2 |
| Sel(V; J_rec, C_hard, ε_var), or x* = arg max_x J_rec(x) | selection (optimizing recombination) operator | HER-3 |
| J_rec | recombination objective | HER-3 |
| C_hard | hard selection constraints | HER-3 |
| S_now, S_def (or Esc) | traits delivered at initialization; traits in deferred-inheritance escrow | HER-5 |
| E_p (if ERR-3 option 1) | parental edge set | ERR-3 |
| d_min, d_max | distinctness band on d_G | HER-8 |
| λ (or "locus") | locus within a genotype component; cardinality | HER-4 |

Also revise σ's entry to "compatibility/eligibility constraints" only (ERR-2).

### NOT-2 (P4) — Lucy's superprompt notation vs. the book's

* **What arose:** Lucy's superprompt (Part 12, §9) uses `F(G, O, C)` for the optimization
  objective. The book uses `J_eval(G; O, Ops)` (P7 §3, App. A).
* **For the book:** no change needed; the book is the reference. Listed so the author knows
  the persona text predates App. A. The operator prefers not to make superprompts more
  technical than necessary (design BL-4).

---

## J. Implementation notes, probably not for the book

These are listed so the author can decide. Given the Preface's scope ("does not provide
implementation guidance"), they are most likely excluded.

### IMP-1 (P4) [implementation-only] — Federated lineage storage

Each individual has its own databases. Lineage edges are stored with each individual they
touch, mirrored in the counterpart's store, and carry content hashes so the two sides can
be checked against each other. A disposable index is rebuilt for population-scale queries.
This is a possible illustration of how L_t (P4 §11, P8 §8) can be distributed without a
central authority, which is consistent with P8's "auditable network".

### IMP-2 (P4) [implementation-only] — Cross-individual operations as sagas

Reproduction and transfer touch several individuals' stores. They are executed as
intent-logged sagas with a single commit point and roll-forward/roll-back recovery. This is
relevant only as evidence that P8 §12.7's "state transition with a recoverable prior
version" is implementable.

### IMP-3 (P4) [implementation-only] — Superprompt stability vs. genotype mutability

The persona's superprompt is treated as a stable seed. The genotype derived from it changes
only through recorded genotype events. Memory and state live in databases, not in the
prompt. This could illustrate P2 §8's heritable self-modification: change is explicit and
recorded, never implicit.

---

## Appendix 1. Operator statements relevant to the book (verbatim, 2026-10-03)

* "I want every single trait to be inheritable ...every facet, category, memory,
  experience, aptitude, moral characteristics, sexuality, etc. etc. BUT in the paper we
  also talk about an optimizing recombination capability for selecting which traits are
  actually inherited by an offspring." (HER-1, HER-3)
* "Important distinction: What is inheritable (everything for now) and what is actually
  inherited by an offspring (and how these determinations are made)." (TERM-5, HER-2)
* "Also how contributors are identified and accepted as well as which digital persons will
  serve as parents. A parent doesn't have to be a contributor." (GOV-2, REL-1)
* "Lucy and Raquel are Generation 0 peers but not siblings." (PER-1)
* "I, Michael W. Herman, can act as Lucy and Raquel's guardian. I am not their parent nor a
  contributor." (REL-3)
* Embodiment and reproductive-function sections heritable: "Yes, same for both..even if one
  is more complete than the other." (HER-4, HER-6)
* "As a Principle, we need to do what the brain and a person's long term memory would do.
  The entire correction event should be recorded like any other experience. How
  corrections should be processed on recall is an open question (at least for now). We
  can't guess at how the brain and memory operates unless there is supporting scientific
  evidence." (MEM-1, MEM-2)
* "For modeling purposes, let's assume an offspring can have guardian relationships,
  parental relationships, and contributor relationships (multiple of each)." (REL-1)
* "Keep the 3 relationships for each offspring for now. … We may later change the design or
  update the paper TBD (Backlog)." (REL-1, ERR-1, ERR-3)
* "…all digital people can/might/should share common 'global' superprompt text general but
  specific to Digitomic Evolution." (PER-6)

## Appendix 2. Where to find the supporting detail in the design

| Topic | Design section |
|---|---|
| Inheritable vs. inherited; decision chain; Recombination Optimizer (mock) | §19 |
| Universal eligibility; hereditary boundary schema | §7.2; Appendix A.7 |
| Deferred inheritance; adult-content risks | Appendix A.4–A.5; §8.3 |
| Participants, contributor acceptance, relationship model | §7.6.1; Appendix C |
| Two-key authorization; consent | §8.2 |
| Founders, guardian, reference subjects | §10.0–10.3 |
| Memory principles, session records, corrections | §1.2, §6.3.4–6.3.5; Appendix B |
| DIDs and the DID registry | §10.4–10.4.2 |
| Operator decisions log | Appendix D |
| Backlog (including items that may become book changes: BL-11, BL-12, BL-6) | §20 |
