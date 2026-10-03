# <Feature Name> — Design

> Template for designing a large new category of functionality in AgentSharp. It is
> derived from [DigitomicEvolution-Design.md](../DigitomicEvolution-Design.md), which is
> the worked example; see its §18 for the method behind each section. Replace every
> `<...>` and delete guidance lines (starting with `>`) when done.

| | |
|---|---|
| Status | Draft for review |
| Date | <yyyy-mm-dd> |
| Code home | `<FeatureLib>` (+ `<FeatureLib>.Tests`); general hooks in `AgentSharpLib` (§4) |
| Storage | <LiteDB / files / none> |
| Inputs | <source-of-truth documents; stakeholder and persona statements> |

---

## 1. Purpose, scope and restraint

> What the feature makes possible, for whom, and what it explicitly does NOT claim or do.

### 1.1 What the affected parties asked for

> Interview the stakeholders, including the personas who will use the feature. Record
> each statement next to the design response that satisfies it.

| Statement | Design response |
|---|---|
| "<quote>" | <component / section> |

## 2. Concept-to-component map

> One row per construct from the source of truth. A construct with no row is a gap; a
> component with no construct is scope creep.

| Construct (source §) | Component | Stored in |
|---|---|---|

## 3. Architecture overview

> Layers, the dependency rule (the feature library references AgentSharpLib, never the
> reverse), and where the LLM fits.

## 4. Changes to AgentSharpLib (general functionality only)

> **Tools first.** Implement behaviour as LLM-callable tools (§<tools>). Change the
> mainline loop only for passive plumbing that tools cannot provide. Test for anything
> proposed here: would an agent that knows nothing about this domain still want it?

| # | Change | Why it is general |
|---|---|---|

## 5. Persistence

> Database or file layout, connection mode, ids, and for each collection: mutability
> (append-only?), indexes, migration from existing files, and the test store.

## 6. Domain model

> Core types as C# sketches, with the invariants each one enforces.

## 7. Core operations and pipelines

> Multi-step operations: the steps, the transaction boundary, and what each step records.

## 8. Governance and safety

> Actors, authorization (who approves what, including the agent's own consent where
> relevant), safety constraints, and what is recorded when something is blocked.

## 9. Runtime integration

> How a host gets a working `AgentSession` with this feature, and what the hosts' slash
> commands do. No loop event subscriptions unless §4 justifies them.

## 10. Persona-specific design

> Per-persona seeding, special guards, and defaults.

## 11. <Additional domain sections as needed>

## 12. Mapping to existing prompts or specs

> Map existing persona prompt sections or spec clauses to components, so prose rules
> become executable policy.

## 13. Project structure

> Folder and file layout of `<FeatureLib>` and its package references.

## 14. Testing

> Turn each testable claim from the source into a unit test (system properties) or into
> experiment infrastructure (empirical claims).

| Claim | Test |
|---|---|

## 15. Implementation phases

> Every phase ships something usable and leaves the hosts working without the feature.

| Phase | Deliverable | User-visible result |
|---|---|---|

## 16. Open questions for the owner

## 17. Tool catalog

> Placement rule (feature library vs. AgentSharpLib), availability or gating matrix, and
> one table per tool group:

| Tool | Risk | Input | Output | Rules |
|---|---|---|---|---|

> End with: host commands that are deliberately not tools, and the tool test
> requirements (schema, happy path, each refusal rule, attribution, gating,
> transactionality).
