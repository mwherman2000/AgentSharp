# SVRN7 DID Refactoring — Specification

Changes to the **SVRN7** solution (`C:\Web 7.0\repos\SVRN7`) so that its DID capabilities
can be reused by other Web 7.0 solutions, AgentSharp / Digitomic Evolution first, as small,
self-contained .NET libraries. Consumers should not need SVRN7's society, monetary,
federation, TDA, PowerShell or LOBE components.

| | |
|---|---|
| Status | Draft spec. Nothing in SVRN7 has been changed. |
| Date | 2026-10-03 |
| Target repo | SVRN7 (separate work session, separate commits) |
| Proposed version | 0.9.0 |
| Requested by | Michael W. Herman |
| First consumer | AgentSharp `DigitomicEvolutionLib` ([DigitomicEvolution-Design.md](DigitomicEvolution-Design.md) §10.4) |
| Baseline examined | SVRN7 working tree as of 2026-10-03 (Svrn7.* 0.8.0) |

---

## 1. Goals and non-goals

### 1.1 Goals

1. **A standalone DID package.** A consumer can create, store, resolve, version, suspend
   and deactivate W3C DID Documents for `did:drn` identifiers by referencing only small
   DID-focused assemblies.
2. **A standalone LiteDB DID registry.** The LiteDB-backed registry is usable on its own,
   either in its own database file or embedded in a database the consumer already has
   open.
3. **Crypto that stands on its own.** `Svrn7.Crypto` can be used without the society and
   monetary model types.
4. **Correctness fixes** in the existing DID registry and document construction, found
   during review (§3).
5. **No breaking changes for existing SVRN7 code** in 0.9.0. Existing projects compile
   unchanged, via type forwarding (§5).
6. **Published packages** that consumers reference as NuGet packages, not as
   cross-repository project paths.

### 1.2 Non-goals

* No change to the `did:drn` method, the DRN resource-addressing draft
  (`draft-herman-drn-resource-addressing-00`) or `draft-herman-did-w3c-drn-00`.
* No change to Society, Federation, Ledger, DIDComm, TDA, wallets, transfers or LOBE
  behaviour.
* **No PowerShell and no LOBE in any new or extracted package.** The extracted packages
  must have no dependency on `System.Management.Automation`, `Svrn7.TDA`, LOBE packages or
  cmdlets, directly or transitively.
* No DID resolution over the network (`drn.directory` DNS lookup stays in `Svrn7.TDA`).
* No change to VC behaviour beyond optional extraction (§4.6).

---

## 2. Current state (inventory)

| Item | Location | Notes |
|---|---|---|
| DID types: `DidDocument`, `DidVerificationMethod`, `DidServiceEndpoint`, `DidProof`, `DidResolutionResult`, `DidStatus` | `src/Svrn7.Core/Models.cs` (≈ lines 8, 213–285) | Live in the same file as wallets, transfers, overdrafts, citizens, societies and more. |
| `KeyAlgorithm`, `Svrn7KeyPair` | `src/Svrn7.Core/Models.cs` (line 7; ≈ 448–455) | Needed by crypto. |
| `Svrn7Role` enum (includes `LOBEPackageManager`, `LOBEMarketplace`) | `src/Svrn7.Core/Models.cs` line 15 | Referenced by `DidDocument.Role`. |
| `IDidDocumentRegistry`, `IDidDocumentResolver` | `src/Svrn7.Core/Interfaces.cs` (≈ 122–149) | Same file as wallet, nonce, federation, inbox and DIDComm interfaces. |
| `ICryptoService` | `src/Svrn7.Core/Interfaces.cs` (≈ 7–21) | Implemented in `Svrn7.Crypto`. |
| `Svrn7Exception`, `InvalidDidException`, `NotFoundException` | `src/Svrn7.Core/Exceptions.cs` | Used by the registry. |
| DID URL builder `TdaResourceId` (`Build(networkId, db, type, key)` → `did:drn:{networkId}/{db}/{type}/{key}`) | `src/Svrn7.Core/Svrn7Constants.cs` (≈ 155–190) | Named for TDA data stores; the form itself is generic. |
| `CesrPrefixEd25519 = "0D"` and other constants | `src/Svrn7.Core/Svrn7Constants.cs` (line 119) | Used by `CryptoService`. |
| `CryptoService` (secp256k1, Ed25519, X25519, AES-256-GCM, Blake3, Base58, CESR signatures) | `src/Svrn7.Crypto/CryptoService.cs` | Depends on `Svrn7.Core` for interface, key and constant types. Packages: NBitcoin, NSec.Cryptography, Blake3, Konscious Argon2. |
| `DidRegistryLiteContext`, `LiteDidDocumentRegistry` | `src/Svrn7.Store/LiteRegistries.cs` (≈ 12–245) | Same assembly as wallet stores, federation stores, inbox and resolvers (`LiteStores.cs`, `LiteFederationAndResolvers.cs`, `Svrn7LiteContext.cs`). LiteDB 5.0.21. |
| `VcRegistryLiteContext`, `LiteVcRegistry` | `src/Svrn7.Store/LiteRegistries.cs` (≈ 248+) | Optional extraction (§4.6). |
| `DIDDocumentService` (OpenTelemetry-traced wrapper over `IDidDocumentRegistry`) | `src/Svrn7.Identity/DIDDocumentService.cs` | Doc comments reference the `Get-DIDDocument` LOBE cmdlet. |
| `VcService` | `src/Svrn7.Identity/VcService.cs` | Same assembly. |
| **DID Document construction** (the only builder) | `src/Svrn7.Federation/Svrn7Driver.cs` (≈ 700–780) | Lives in Federation, so constructing a valid document means referencing Federation or copying code. |
| Published packages | `dist/` | Contains `Svrn7.Identity.0.8.0.nupkg` and others; no `Svrn7.Core`, `Svrn7.Crypto` or `Svrn7.Store` package was seen. |
| Tests | `tests/Svrn7.Tests/CoreTests.cs`; `tests/Svrn7.Society.Tests`; `tests/Svrn7.TDA.Tests` | No direct unit tests for `LiteDidDocumentRegistry` were found; it is exercised indirectly through Society and TDA tests. |

Verified 2026-10-03: `Svrn7.Core`, `Svrn7.Crypto`, `Svrn7.Identity` and `Svrn7.Store`
have **no code dependency** on PowerShell or LOBE. The only traces are XML doc comments
(`Svrn7RunspaceContext`, "LOBE cmdlet") and the two `Svrn7Role` enum values.

---

## 3. Problems found during review

| # | Problem | Where | Impact |
|---|---|---|---|
| P1 | DID types are mixed with society and monetary types (≈ 58 public types across `Models.cs` and `Interfaces.cs`). | `Svrn7.Core` | Consumers of DIDs take on the whole SVRN7 domain model. |
| P2 | `DidDocument` carries TDA-specific properties: `Role` (`Svrn7Role?`, including LOBE values) and `Svrn7Name`. | `Models.cs` `DidDocument` | A generic DID type is coupled to TDA roles and LOBE. |
| P3 | The only DID Document builder is in `Svrn7.Federation` (`Svrn7Driver`). | `Svrn7Driver.cs` ≈ 700–780 | Consumers must duplicate the JSON construction logic. |
| P4 | **The builder serializes `context`, not `@context`.** It uses an anonymous type with a property named `context`, so the canonical JSON lacks the W3C `@context` member. | `Svrn7Driver.cs` (`context = new[] { "https://www.w3.org/ns/did/v1" }`) | Stored `DocumentJson` is not a conformant W3C DID Document. |
| P5 | `DocumentJson` duplicates the first-class fields and is supplied by the caller; nothing keeps them consistent. | `DidDocument` | Fields and JSON can disagree. |
| P6 | `DidRegistryLiteContext` only accepts a connection string and opens its own `LiteDatabase`. | `LiteRegistries.cs` ≈ 21 | Cannot share a database the consumer already holds open (LiteDB Direct mode is exclusive). |
| P7 | Collection names are unprefixed (`Documents`, `History`, `VMIndex`). | `LiteRegistries.cs` ≈ 17–19 | Embedding the registry in another database risks name collisions. |
| P8 | **Registry writes are not atomic.** `CreateAsync` inserts into `Documents`, `History` and `VMIndex` separately, with no transaction. `UpdateAsync` is the same. | `LiteDidDocumentRegistry` | A crash mid-write leaves an inconsistent registry. |
| P9 | **Status changes leave no history.** `SuspendAsync`, `ReinstateAsync` and `DeactivateAsync` update `Documents` only. They append no `History` record and do not bump `Version`; `UpdatedAt` is not set by suspend or reinstate. | `LiteDidDocumentRegistry` | Version history is incomplete; status transitions cannot be audited or replayed. |
| P10 | **The verification-method index goes stale.** `VMIndex` is written only on `CreateAsync` (parsed from `DocumentJson`), never on `UpdateAsync`. | `IndexVerificationMethods` | After key rotation, `FindDidByPublicKeyHexAsync` misses new keys and still matches retired ones. |
| P11 | `VMIndex` is derived from `DocumentJson` rather than from the `VerificationMethod` list, and silently skips malformed JSON. | `IndexVerificationMethods` | Index depends on caller-supplied JSON (see P5). |
| P12 | `LiteDidDocumentRegistry` requires an `ILogger<T>`. | constructor | Minor; consumers must pass `NullLogger<T>.Instance`. |
| P13 | `Svrn7.Store` bundles the DID registry with wallet, federation, inbox and other stores. | `Svrn7.Store` | Referencing the DID registry pulls in every store. |
| P14 | `Svrn7.Crypto` depends on `Svrn7.Core` only for `ICryptoService`, `Svrn7KeyPair`, `KeyAlgorithm` and the CESR prefix constants. | `CryptoService.cs` | Crypto consumers also take on the full domain model. |
| P15 | The DID URL builder is named `TdaResourceId`. | `Svrn7Constants.cs` | Name implies TDA-only use; non-TDA consumers (e.g. `…/person/1.0/<guid>`) use it anyway. |
| P16 | No direct unit tests for the DID registry. | `tests/` | P8–P11 went unnoticed. |
| P17 | `QueryAsync` loads every document and filters in memory. | `LiteDidDocumentRegistry.QueryAsync` | Fine at current scale; note only. |

---

## 4. Proposed changes

### 4.1 New package `Svrn7.Did` (no outbound dependencies)

A new project, `src/Svrn7.Did/Svrn7.Did.csproj` (net8.0, no package references). It
contains:

| Type | Source | Change |
|---|---|---|
| `DidDocument` | `Svrn7.Core` | Moved. `Role` and `Svrn7Name` are **removed from the type** and replaced by `Dictionary<string, string> Extensions` (see 4.1.1). |
| `DidVerificationMethod`, `DidServiceEndpoint`, `DidProof`, `DidResolutionResult`, `DidStatus` | `Svrn7.Core` | Moved unchanged. |
| `IDidDocumentRegistry`, `IDidDocumentResolver` | `Svrn7.Core` | Moved unchanged. |
| `Svrn7Exception`, `InvalidDidException`, `NotFoundException` | `Svrn7.Core` | Moved. The other exception types stay in Core and derive from the moved base. |
| `DrnUrl` (new name for the DID URL builder and parser) | `TdaResourceId` | Moved and renamed. `TdaResourceId` stays in Core as a thin `[Obsolete]` wrapper that calls `DrnUrl`. |
| `DidDocumentBuilder` (new) | logic from `Svrn7Driver` | See 4.2. |
| `DidDocumentJson` (new) | — | Canonical serializer and deserializer (4.2). |

**Namespaces.** Moved types keep their current namespaces (`Svrn7.Core.Models`,
`Svrn7.Core.Interfaces`, `Svrn7.Core.Exceptions` or whatever they are today), so that type
forwarding (§5) works and no `using` changes are needed. New types use the namespace
`Svrn7.Did`.

#### 4.1.1 Removing TDA-specific properties from `DidDocument`

* `Role` and `Svrn7Name` become entries in `Extensions` (keys `svrn7:role`,
  `svrn7:name`).
* Source compatibility for existing SVRN7 code comes from extension methods in `Svrn7.Core`:

  ```csharp
  public static class DidDocumentSvrn7Extensions
  {
      public static Svrn7Role? GetRole(this DidDocument d) => …;   // parses Extensions["svrn7:role"]
      public static void SetRole(this DidDocument d, Svrn7Role? role) => …;
      public static string? GetSvrn7Name(this DidDocument d) => …;
      public static void SetSvrn7Name(this DidDocument d, string? name) => …;
  }
  ```

* Call sites change from `doc.Role` to `doc.GetRole()`; there are a handful, in
  `Svrn7Driver`, `DIDDocumentService.Summarize` and TDA code.
* **Data compatibility.** Existing LiteDB documents store `Role` and `Svrn7Name` as BSON
  fields. The registry's `BsonMapper` (4.3) maps the legacy `Role` / `Svrn7Name` fields
  into `Extensions` on read, and writes only `Extensions`. A one-time migration is not
  required, but `MigrateLegacyFields()` is provided for operators who want clean data.

**Alternative, if a breaking change is unacceptable even in 0.9.0:** keep `Role` and
`Svrn7Name` on `DidDocument`, but type `Role` as `string?`. That still removes the
dependency on the `Svrn7Role` enum, and its LOBE values, from the DID package. The
recommendation is the `Extensions` approach.

### 4.2 `DidDocumentBuilder` and canonical JSON (fixes P3, P4, P5, P11)

```csharp
namespace Svrn7.Did;

public sealed class DidDocumentBuilder
{
    public DidDocumentBuilder(string did, string methodName = "drn");

    public DidDocumentBuilder WithController(string controllerDid);             // default: self
    public DidDocumentBuilder AddVerificationMethod(string fragment, string type, string publicKeyHex,
                                                   string? controller = null);  // id = did#fragment
    public DidDocumentBuilder AddAuthentication(string fragment);
    public DidDocumentBuilder AddAssertionMethod(string fragment);
    public DidDocumentBuilder AddKeyAgreement(string fragment);
    public DidDocumentBuilder AddCapabilityInvocation(string fragment);
    public DidDocumentBuilder AddCapabilityDelegation(string fragment);
    public DidDocumentBuilder AddService(string fragment, string type, string endpoint);
    public DidDocumentBuilder AddAlsoKnownAs(string uri);
    public DidDocumentBuilder WithExtension(string key, string value);

    public DidDocument Build();   // validates; sets DocumentJson from the fields
}

public static class DidDocumentJson
{
    public static string Serialize(DidDocument doc);   // canonical W3C JSON, "@context" first
    public static DidDocument Parse(string json, string methodName);
}
```

Rules:

* **The fields are the single source of truth.** `DocumentJson` is always produced by
  `DidDocumentJson.Serialize` from the fields, never written by hand. The registry
  recomputes it on `Create` and `Update` and rejects a supplied `DocumentJson` that
  differs.
* **`@context` is emitted correctly** as `"@context": ["https://www.w3.org/ns/did/v1"]`,
  plus suite contexts when present (for example
  `https://w3id.org/security/suites/ed25519-2020/v1`). This uses an explicit
  `[JsonPropertyName("@context")]` or a hand-written `Utf8JsonWriter`, not an anonymous
  type.
* **Member order is deterministic**, so equal documents produce byte-identical JSON:
  `@context, id, controller, alsoKnownAs, verificationMethod, authentication,
  assertionMethod, keyAgreement, capabilityInvocation, capabilityDelegation, service`,
  then extensions. This makes `DocumentJson` hashable and signable.
* **Extensions** are serialized as top-level members with their prefixed names (for example
  `"svrn7:role": "Society"`). This is valid under W3C DID Core's extensibility rules.
* **Validation in `Build()`:**
  * `id` matches `^did:[a-z0-9]+:.+`;
  * every relationship fragment references an existing verification method;
  * verification-method ids are unique;
  * `publicKeyHex` is valid hex of the right length for the type (Ed25519: 32 bytes;
    X25519: 32 bytes; secp256k1: 33 or 65 bytes);
  * violations throw `InvalidDidException`.
* **`Svrn7Driver` is changed to use `DidDocumentBuilder`.** This fixes P4 for all SVRN7
  documents created from now on. Existing stored documents keep their old JSON until their
  next update. `DidDocumentJson.Parse` accepts both `context` and `@context` on read.

### 4.3 New package `Svrn7.Did.LiteDb` (fixes P6–P12, P16)

A new project, `src/Svrn7.Did.LiteDb/Svrn7.Did.LiteDb.csproj` (net8.0). It references
`Svrn7.Did`, `LiteDB` 5.0.21 and `Microsoft.Extensions.Logging.Abstractions` 8.0.2, and
contains `DidRegistryLiteContext` and `LiteDidDocumentRegistry`, moved from `Svrn7.Store`
(which forwards the types, §5).

**Context constructors (P6, P7):**

```csharp
public sealed class DidRegistryLiteContext : IDisposable
{
    public DidRegistryLiteContext(string connectionString);                 // existing: owns its database
    public DidRegistryLiteContext(ILiteDatabase database,
                                  string collectionPrefix = "",
                                  bool ownsDatabase = false);              // new: embedded use
    public string CollectionPrefix { get; }
    // Collections: {prefix}Documents, {prefix}History, {prefix}VMIndex
}
```

* With `ownsDatabase: false`, `Dispose()` does not dispose the shared database.
* The `BsonMapper` configuration (`DidDocument` id mapping, indexes, and the legacy-field
  mapping from 4.1.1) moves into a static `DidRegistryBsonMapping.Configure(BsonMapper)`.
  A consumer that brings its own database calls it on that database's mapper (or uses
  `BsonMapper.Global` documented as such).

**Registry behaviour (P8–P12):**

| Change | Detail |
|---|---|
| Atomic writes (P8) | `CreateAsync`, `UpdateAsync` and every status change wrap their inserts and updates in `BeginTrans()` / `Commit()`, with `Rollback()` on exception. In Shared connection mode, where explicit transactions are limited, the context documents this and the operations stay idempotent: History inserts are keyed by `(Did, Version)`, which is unique. |
| History for every change (P9) | `SuspendAsync`, `ReinstateAsync` and `DeactivateAsync` become versioned updates. They increment `Version`, set `UpdatedAt` (and `DeactivatedAt` where applicable), recompute `DocumentJson` (status is not a DID Core member, so the JSON is unchanged, but the record is versioned), and append to `History`. A new unique index on `History (Did, Version)` is added. |
| Index maintenance (P10, P11) | `VMIndex` is maintained from the `VerificationMethod` list, not from `DocumentJson`, on `Create` and on every `Update`. Rows carry `{ PublicKeyHex, Did, VerificationMethodId, FromVersion, ToVersion? }`. On update, removed keys get `ToVersion` set rather than being deleted. |
| Key lookup | `FindDidByPublicKeyHexAsync` returns only keys current in the latest version. A new `FindKeyHistoryAsync(publicKeyHex)` returns every `(Did, VerificationMethodId, FromVersion, ToVersion)`, so signatures made with since-rotated keys can still be attributed. |
| Optional logger (P12) | A constructor overload without `ILogger` uses `NullLogger<LiteDidDocumentRegistry>.Instance`. |
| Query (P17) | `QueryAsync` uses indexed queries (`MethodName`, `Status` are already indexed) instead of `FindAll()` plus LINQ. |
| Index rebuild | New `RebuildVerificationMethodIndexAsync()` regenerates `VMIndex` from `History`. It is used once after upgrading, because indexes written by 0.8.0 are stale (P10). |

### 4.4 `Svrn7.Crypto` independence (fixes P14)

* Move `ICryptoService`, `Svrn7KeyPair` and `KeyAlgorithm` into a new, dependency-free
  project, `src/Svrn7.Crypto.Abstractions`. Move the CESR prefix constants into it too,
  as `CesrPrefixes`.
* `Svrn7.Crypto` references `Svrn7.Crypto.Abstractions` instead of `Svrn7.Core`.
* `Svrn7.Core` references `Svrn7.Crypto.Abstractions` and forwards the moved types (§5).
  `Svrn7Constants.CesrPrefixEd25519` and its siblings remain as `const` aliases of
  `CesrPrefixes.*`.
* Optional, not required for 0.9.0: split secp256k1 and NBitcoin out of `Svrn7.Crypto`, so
  that Ed25519-only consumers don't pull in NBitcoin.

### 4.5 `Svrn7.Identity` clean-up

* `DIDDocumentService` references `Svrn7.Did` (it uses only DID types). Its doc comments
  drop the LOBE cmdlet reference ("Used by any C# caller that needs traced DID…").
* `Summarize` uses `GetRole()` (4.1.1).
* Optional: move `DIDDocumentService` into `Svrn7.Did` itself, since it depends only on
  `IDidDocumentRegistry` and `System.Diagnostics`. Recommended, so consumers get tracing
  without `Svrn7.Identity`.

### 4.6 Optional: VC registry extraction

The same treatment could be applied to `VcRecord`, `IVcRegistry`, `VcRegistryLiteContext`
and `LiteVcRegistry`: a `Svrn7.Vc` package plus `Svrn7.Vc.LiteDb`. This is **not
required** for the first consumer. It is listed so the package layout leaves room for it.

---

## 5. Compatibility strategy (no breaking changes for SVRN7 code)

* **Type forwarding.** Each assembly that loses types keeps
  `[assembly: TypeForwardedTo(typeof(X))]` entries for every moved type:
  * `Svrn7.Core` forwards to `Svrn7.Did` and `Svrn7.Crypto.Abstractions`;
  * `Svrn7.Store` forwards to `Svrn7.Did.LiteDb`.

  Binaries compiled against 0.8.0 keep loading, and source keeps compiling, because the
  namespaces are unchanged.
* **Project references.** `Svrn7.Core` references `Svrn7.Did` and
  `Svrn7.Crypto.Abstractions`. Core's description changes from "Zero outbound
  dependencies" to "Depends only on Svrn7.Did and Svrn7.Crypto.Abstractions (both
  dependency-free)." `Svrn7.Store` references `Svrn7.Did.LiteDb`.
* **One intentional source change:** `DidDocument.Role` / `Svrn7Name` become extension
  methods (4.1.1). This is mechanical; all call sites are in SVRN7. If that is
  unacceptable, use the `string? Role` alternative.
* **Data.** No migration is required. Legacy `Role` / `Svrn7Name` BSON fields are read via
  the mapper (4.1.1). Stale `VMIndex` rows are fixed by
  `RebuildVerificationMethodIndexAsync()` (4.3), which SVRN7's own startup should call
  once after upgrading.

---

## 6. Resulting package layout

```
Svrn7.Crypto.Abstractions   (no deps)                ICryptoService, Svrn7KeyPair, KeyAlgorithm, CesrPrefixes
Svrn7.Did                   (no deps)                DID types, registry interfaces, exceptions,
                                                     DrnUrl, DidDocumentBuilder, DidDocumentJson,
                                                     [DIDDocumentService if moved]
Svrn7.Did.LiteDb            → Svrn7.Did, LiteDB, Logging.Abstractions
Svrn7.Crypto                → Svrn7.Crypto.Abstractions, NSec, NBitcoin, Blake3, Argon2
Svrn7.Core                  → Svrn7.Did, Svrn7.Crypto.Abstractions   (forwards moved types)
Svrn7.Store                 → Svrn7.Core, Svrn7.Crypto, Svrn7.Did.LiteDb, LiteDB
Svrn7.Identity              → Svrn7.Core, Svrn7.Crypto, Svrn7.Did
(Federation, Society, Ledger, DIDComm, TDA … unchanged)
```

**What a DID-only consumer references:** `Svrn7.Did`, `Svrn7.Did.LiteDb` and `Svrn7.Crypto`.
The transitive closure is LiteDB, Logging.Abstractions, NSec, NBitcoin, Blake3, Argon2 and
`Svrn7.Crypto.Abstractions`. It contains **no** `Svrn7.Core`, Society, Federation, TDA,
PowerShell or LOBE assemblies.

---

## 7. Packaging and publishing

* Version **0.9.0** for every changed or new package, with assembly versions bumped
  accordingly.
* Every `src/Svrn7.*` library project gets `<IsPackable>true</IsPackable>` and package
  metadata (copy the existing Authors, Company, License, ProjectUrl and Copyright), plus
  `<Description>`.
* `dotnet pack -c Release -o dist` produces `Svrn7.Crypto.Abstractions`, `Svrn7.Did`,
  `Svrn7.Did.LiteDb`, `Svrn7.Crypto`, `Svrn7.Core`, `Svrn7.Store` and `Svrn7.Identity`
  0.9.0.
* **Local feed for consumers:** `dist/` is registered as a NuGet source on the developer
  machine, for example `dotnet nuget add source "C:\Web 7.0\repos\SVRN7\dist" -n svrn7-local`.
  Alternatively, a `nuget.config` in the consumer repo points at it. AgentSharp then uses
  `PackageReference`s.
* Include symbols (`.snupkg`) and SourceLink, if already configured in SVRN7.

---

## 8. Tests (new, in `tests/Svrn7.Did.Tests`)

`Svrn7.Did.Tests` runs the LiteDB registry against `new LiteDatabase(new MemoryStream())`.

| Area | Tests |
|---|---|
| Builder | Minimal document (one Ed25519 key, authentication + assertionMethod) builds and validates; dangling relationship fragment throws; duplicate key ids throw; bad hex length throws; `DocumentJson` starts with `"@context"`; serialization is byte-identical across runs; round-trip `Serialize → Parse` preserves every field and extension. |
| Legacy JSON | `Parse` accepts `context` (pre-fix) and `@context`. |
| DrnUrl | `Build` and `Parse` round-trip, including `did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>`; `TdaResourceId` wrapper returns identical strings. |
| Registry: create | Version 1 is stored; History has one row; VMIndex has one current row; duplicate DID throws. |
| Registry: update | Version must be current + 1; History gains a row; a rotated key appears in `FindDidByPublicKeyHexAsync` and the retired key no longer does; `FindKeyHistoryAsync` returns both with correct version ranges. |
| Registry: status | Suspend, reinstate and deactivate each bump `Version` and append History; deactivated documents reject update, suspend and reinstate; `IsActiveAsync` reflects status. |
| Atomicity | Inject a failure after the `Documents` write (test hook or fault-injecting mapper): no partial `History` or `VMIndex` rows remain. |
| Embedded mode | Two registries with different prefixes share one `LiteDatabase` without interfering; `Dispose` with `ownsDatabase: false` leaves the database usable. |
| Legacy data | A document stored with 0.8.0-style `Role` / `Svrn7Name` BSON fields reads back with matching `Extensions`. |
| Index rebuild | After seeding 0.8.0-style stale `VMIndex` rows, `RebuildVerificationMethodIndexAsync` yields the correct current and historical keys. |
| Dependency guard | Test that the closure of `Svrn7.Did` and `Svrn7.Did.LiteDb` contains no `System.Management.Automation`, `Svrn7.TDA`, `Svrn7.Society`, `Svrn7.Federation` or `Svrn7.Core` assembly. |

All existing test projects (`Svrn7.Tests`, `Svrn7.Society.Tests`, `Svrn7.TDA.Tests`) must
pass unchanged.

---

## 9. Acceptance criteria

1. A new console app referencing only `Svrn7.Did`, `Svrn7.Did.LiteDb` and `Svrn7.Crypto`
   0.9.0 can:
   * generate an Ed25519 key;
   * build a minimal DID Document for `did:drn:digitomicevolution.svrn7.net/person/1.0/<guid>`;
   * store it in a standalone LiteDB file;
   * resolve it, update it (rotate key), suspend, reinstate, and read its full history;
   * verify a signature made with the key.
2. The same app's dependency closure contains no Core, Society, Federation, TDA, PowerShell
   or LOBE assemblies (§6).
3. All SVRN7 solution projects build with no source changes other than the `Role` /
   `Svrn7Name` call sites (4.1.1), and all existing tests pass.
4. New tests (§8) pass.
5. Newly created SVRN7 DID Documents contain `"@context"` (P4 fixed).
6. Packages are in `dist/` at 0.9.0.

---

## 10. Suggested work order

1. Add `Svrn7.Crypto.Abstractions`; move types; forward; build.
2. Add `Svrn7.Did`; move DID types, interfaces and exceptions; forward; add `DrnUrl` and the
   `TdaResourceId` wrapper; build.
3. Add `DidDocumentBuilder` and `DidDocumentJson` with tests; switch `Svrn7Driver` to the
   builder (P4).
4. Replace `Role` / `Svrn7Name` with `Extensions` plus extension methods; fix call sites;
   add the legacy mapping.
5. Add `Svrn7.Did.LiteDb`; move the context and registry; forward from `Svrn7.Store`;
   apply P6–P12 fixes; add tests.
6. `Svrn7.Identity` clean-up; optionally move `DIDDocumentService` into `Svrn7.Did`.
7. Packaging (§7); acceptance app (§9).

Each step is a separate commit that builds and passes tests.

---

## 11. Open questions for the SVRN7 session

1. **`Extensions` vs. `string? Role`** for removing the TDA coupling from `DidDocument`
   (4.1.1).
2. **Move `DIDDocumentService` into `Svrn7.Did`?** (4.5)
3. **Split secp256k1 and NBitcoin out of `Svrn7.Crypto`** now or later? (4.4)
4. **Extract the VC registry too?** (4.6)
5. **Network id registration.** Does `digitomicevolution.svrn7.net` need to be registered
   anywhere (for example in `drn.directory`) for future network resolution? It is not
   needed for a local registry.
6. **Package naming.** Keep the `Svrn7.*` prefix for these generic packages, or use
   `Web7.Did*`?
