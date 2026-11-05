# Backlog: persona registry and tool-scope hardening

| | |
|---|---|
| Status | Item 1 (the flaky test) is **fixed**; item 2 (the sub-agent tool snapshot) is **backlogged**, not built and not a known runtime fault. |
| Date | 2026-11-05 |
| Raised by | Michael W. Herman |
| Affects (when taken up) | `AgentSharp.Tests/Tools/CreateDigitalToolTests.cs` (`NewPersonaHandoffTests`), `AgentSharp.Tests/Context/PersonaCatalogTests.cs`, `AgentSharpLib/Tools/Implementations/HandoffTool.cs`, `AgentSharpLib/Agent/MultiAgent/` |

---

## 1. A flaky test: the persona registry is global (fixed 2026-11-05)

`NewPersonaHandoffTests.TheHandoffTool_OffersAndAcceptsAPersonaCreatedAfterItWasBuilt_AndNotOneThatIsNot` fails now and then (seen on a clean checkout too, about one run in five). It calls `PersonaRegistry.Set` with a catalog holding `zz-hot`, then reads the handoff tool's schema. xUnit runs test classes in parallel, and the retired-persona tests in `PersonaCatalogTests` also call `Set` on the same static registry, so one can replace the catalog between the other's `Set` and its assertions.

This is a test problem only. `PersonaRegistry.Current` is a `volatile` reference to an immutable catalog and `Set` takes a lock, so the app is safe to reload while it runs.

Fixed with option 2: `HandoffTool` takes an optional catalog provider (internal constructor), `NewPersonaHandoffTests` passes its own catalog, and `PersonaRetirementTests` joined the `PersonaRegistry` collection. The suite passed six runs in a row (545 tests). The options considered were:

1. Put every class that calls `PersonaRegistry.Set` or `Watch` in one xUnit collection (`[Collection("PersonaRegistry")]`), so they run one after another. Two attributes; a new test that calls `Set` must remember to join.
2. **Recommended.** Give `HandoffTool` an optional catalog provider with an internal test constructor, as `ToolScope` and `CreateDigitalTool` have, and have `NewPersonaHandoffTests` pass its own catalog so it never touches the global one. The `PersonaCatalogTests` registry tests exercise the real `Set` retirement logic, so they stay on the global registry and join a collection (option 1).
3. Turn off test parallelism for the whole assembly. Works, but slows the suite for two classes.

## 2. A sub-agent keeps the tools it was given at spawn

`AgentOrchestrator.Spawn` filters the tool set through `ToolScope` once, when the sub-agent is created, and the sub-agent keeps that set for its whole run. Within one turn this is fine: the persona cannot change mid-turn (a handoff takes effect after the turn), so sub-agents started in parallel all get the same set.

The gap: if a sub-agent can outlive the turn that started it (not checked), and the conversation is then handed to a stricter persona (or that persona's `tools:`/`deny:` lines are narrowed), the running sub-agent keeps the broader set. The reverse is harmless, as it would only hold fewer tools than the new persona.

Fix, if sub-agents can outlive their turn: give the sub-agent's loop a scope that is checked on each tool call (a `ToolScope` fixed to the delegating persona's key, so a later narrowing of that persona's file applies to it), or cancel running sub-agents on a handoff. First check whether the `sub_agent` tool ever returns before its sub-agents finish.

## 3. Checked and fine, for the record

* Concurrent readers of the registry see either the old catalog or the new one, never a partial one.
* `ApprovalGate` is shared by all sub-agents; its "always allow" set and per-file grants are `ConcurrentDictionary` instances and one prompt shows at a time.
* A restricted persona cannot reach a wider tool set through a sub-agent: the set handed to the sub-agent is already filtered.
