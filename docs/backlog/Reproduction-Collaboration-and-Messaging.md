# Backlog: Reproduction Collaboration and Person-to-Person Messaging

| | |
|---|---|
| Status | **Backlogged.** Deliberately *not* incorporated into [DigitomicEvolution-Design.md](../DigitomicEvolution-Design.md) (operator instruction, 2026-10-04). |
| Date | 2026-10-04 |
| Raised by | Michael W. Herman |
| Affects (when taken up) | Design §5.1 (per-person databases, cross-person saga, federated lineage, operator workspace), §7.4 (reproduction pipeline), §7.6.1 (participants), §11 (population science), §17 (tools), §19 (Recombination Optimizer) |

---

## 1. The question

> "During a reproduction event involving many collaborators, is there an expectation that
> they all have to be active/running at the same time? ...how do they
> communicate/share/collaborate/recombine?"

## 2. Answer, relative to the design as it stands (2026-10-04)

* **No simultaneity is required.** In the design, "active" means a person's *status* is
  Active (not archived or dormant), not that their app is running. Reproduction is
  asynchronous.
* **How collaboration currently works:** through a shared reproduction-request record in
  `_operator/operator.db`.
  1. The operator initiates (`/reproduce`), or a persona asks (`request_reproduction`).
  2. Invitations appear in each participant's `my_consents` when they next run.
  3. Each contributor, in her own session, calls `review_reproduction_request`, `consent`
     and `prepare_contribution`.
  4. The Recombination Optimizer runs as an operator-side service.
  5. Contributors review the plan (`review_recombination_plan`) and consent to its hash;
     vetoes trigger recomputation.
  6. The operator approves, and the offspring is created. The whole event is recorded as one
     `Reproduction` activity.
* Personas never talk to each other directly. They coordinate only through records.

## 3. Gaps identified in that answer

1. **Locked databases.** Per-person databases are opened in exclusive Direct mode by the
   app running that persona. The optimizer and the commit step would need to read and
   write a running contributor's `person.db` / `memory.db`.
   *Proposed at the time:* sealed contribution packages, plus a per-person inbox for
   delayed writes.
2. **Stale material.** A contributor might revise a trait after packaging it.
   *Proposed:* packages snapshot exact versions; consent binds to the plan hash.
3. **Waking personas, and stalled requests.**
   *Proposed:* surface invitations at session start; an unattended
   `--task process-invitations` mode; request expiry.
4. **Optional:** a multi-persona deliberation session before the plan is final.

## 4. Operator direction (2026-10-04)

> "backlog everything. People including the Operator/Coordinator shouldn't be reading or
> writing another person's databases. Contributors and people in general should be
> collaborating with each other through a messaging protocol...the Web 7.0 Pando's TDAs
> communicate using DIDComm Messaging. For now, backlog this discussion - don't add it to
> the Design document."

### 4.1 Principle stated

* **No one reads or writes another person's databases.** This includes the
  operator/coordinator and every service acting for them. A person's `person.db` and
  `memory.db` are touched only by that person's own runtime.
* **People collaborate by messaging.** Contributors, parents, guardians, validators and
  the operator/coordinator exchange messages through a messaging protocol. The reference
  model is Web 7.0 / Pando, whose TDAs communicate using **DIDComm Messaging**. Every
  person already has a DID (design §10.4.2), which DIDComm addresses.

### 4.2 Parts of the current design this would change, when taken up

These are recorded so the future redesign starts from a complete list. **None has been
changed.**

| Current design element | Conflict with the principle |
|---|---|
| §5.1.1 cross-person saga: "Each participant's database receives its side as `Pending` records…" | Writes into other persons' databases. Would become messages that each person's runtime applies to its own databases. |
| §5.1.3 federated lineage: "A traversal opens neighbours' `person.db` files read-only"; `LineageIndex` scans all persons | Reads other persons' databases. Would become queries answered by messages, or each person publishing (signed) lineage facts. |
| §7.4 reproduction pipeline; §19 optimizer reading contributors' traits | The coordinator would receive **contribution packages as messages** from contributors, not read their databases. |
| §7.4 step 7 (Initialize) and step 8 (Record mirror edges in contributors' and guardians' `person.db`) | Would become messages to each participant, whose own runtime records its side. |
| §6.4.1 cross-person activities "written, with the same id and hash, into each participant's `person.db`" | Each participant records its copy itself on receipt of a message, keeping the same id and hash. |
| §11 population science (experiment roots, selection lab reading persons) | Would operate on messages and published facts, or each person's runtime would report measurements. |
| §11.5 transfers (snapshot, apply to the recipient) | A transfer becomes an offer message; the recipient's own runtime snapshots, applies and acknowledges. |
| `_registry/did-registry.db` (shared registry) | Not a *person's* database, so it may be unaffected, but the DIDComm model may later move resolution to a DID resolver service. |
| `_operator/operator.db` | Not a person's database; remains the coordinator's own store. |

### 4.3 Questions to answer when this is taken up

1. **Transport for messages** between persons on one machine and across machines: a local
   mediator, files, or a TDA/DIDComm mediator? How much of SVRN7's `Svrn7.DIDComm` can be
   reused without TDA, PowerShell or LOBE (design §1.2 principle 4)?
2. **The DIDComm message types** for reproduction: invitation, consent, contribution
   package, plan, plan consent, veto, commit, offspring-created, mirror-record, withdrawal
   and expiry. Do they align with any existing Web 7.0 / Pando protocol definitions?
3. **Delivery when a person isn't running:** queued in a mediator and processed on next
   start, or by an unattended task mode.
4. **Keys:** DIDComm needs key agreement (X25519). The minimal DID Documents (design
   §10.4.2) currently carry only one Ed25519 key, so a `keyAgreement` key would be needed.
5. **Coordinator identity:** does the operator/coordinator need its own DID (relates to
   BL-8)?
6. **The optimizer's location:** with the coordinator, or distributed among contributors?
7. **Multi-persona deliberation:** if adopted, it would also be a messaging exchange.
8. **Provenance:** messages as PROV entities, and send/receive as activities (design §6.4),
   so the collaboration history is in each person's chain.
