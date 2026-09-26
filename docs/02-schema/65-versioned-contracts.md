---
weight: 65
title: Versioned Contracts
description: Keep frozen wire schemas readable through explicit, typed migrations.
targetFramework: net8.0
---

# Versioned Contracts

A `Contract<'model>` is a chain of frozen wire schemas and explicit migrations. Use it when stored configuration,
queued messages, or events must remain readable after their wire shape changes. Contract versioning belongs at the
wire boundary; map the current wire value into a strict domain type afterwards.

Schema derivation is documented separately. [Derived Schemas](/schema/derivation/index.html) explains how to generate schemas from
ordinary `[<DeriveSchema>]` F# records, configure MSBuild, and see every supported attribute.

## Wire and domain models

A wire model describes what the format can carry. Keep it public and permissive. A domain model describes what business
code may rely on and should protect its invariants with refined values or private construction.

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
// Wire DTO: shaped like persisted input.
type OrderWire = { Sku: string; Quantity: int }

// Domain constructor owns business invariants.
// Order.create : string -> int -> Result<Order, OrderError>
let toDomain (wire: OrderWire) =
    Order.create wire.Sku wire.Quantity
```


Parse or migrate to the current wire model first, then call `toDomain`. See
[Separate Wire and Domain Models](/schema/patterns/wire-and-domain-models.html).

## Declare a contract by hand

`Contract.create` starts with the current version and schema. Add each immediately preceding version with
`Contract.supersedes`, then choose how the input version is discovered with `Contract.build`.

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
open Reified

type ConfigV1 = { Host: string }
type Config = { Host: string; Port: int }

let migrateV1ToV2 (v1: ConfigV1) : Result<Config, MigrationError> =
    match v1.Host.Split ':' with
    | [| host; port |] ->
        match System.Int32.TryParse port with
        | true, parsed -> Ok { Host = host; Port = parsed }
        | false, _ -> Error(MigrationError.MigrationFailed $"unreadable port in '{v1.Host}'")
    | _ -> Ok { Host = v1.Host; Port = 5432 }

let configContract : Contract<Config> =
    Contract.create "Config" 2 configSchema
    |> Contract.supersedes 1 configV1Schema migrateV1ToV2
    |> Contract.build (VersionSource.Field "schemaVersion")
```


Migrations are hand-written, typed, contiguous, and may fail. Parsing selects the frozen schema for the input version,
parses it, migrates each adjacent step, and checks the result against the current schema. There is no automatic
structural migration.

## Version sources

| Source | Use |
| --- | --- |
| `VersionSource.Field "schemaVersion"` | Read a positive integer version from an input field. |
| `VersionSource.External` | The caller knows the version out of band and calls `Contract.parseVersion`. |
| `VersionSource.UnversionedMeans 1` | Treat input without a marker as one registered version. |

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
match Contract.parse configContract raw with
| Ok config -> printfn $"%s{config.Host}:%d{config.Port}"
| Error ContractError.VersionMissing -> eprintfn "no readable schema version"
| Error (ContractError.VersionUnrecognized version) -> eprintfn $"version %d{version} is not registered"
| Error (ContractError.VersionTooNew(found, supported)) ->
    eprintfn $"payload is v%d{found}; this build supports v%d{supported}"
| Error (ContractError.ParseFailed(version, diagnostics)) ->
    eprintfn $"v%d{version} payload is malformed: %A{diagnostics}"
| Error (ContractError.Migration failure) -> eprintfn $"migration failed: %A{failure}"
```


`ContractError.ParseFailed` and `MigrationError.RevalidationFailed` carry the same path-aware `SchemaErrors` used by
`Schema.parse`.

## Generate a version series from records

The generator can group `[<DeriveSchema>]` records into a version series and write the `Contract` wiring for you. It
does this by examining the names of the marked records in each source file.

### How records are grouped

By default, a marked record whose name ends in `V` followed by a number (`ProfileV1`, `ProfileV2`) belongs to the
series named by the rest of its name, **but only when a marked record with that bare name (`Profile`) exists in the
same file**. The bare record is the current version. Its version number is never written down; it is always one more
than the highest `Vn` in the series.

```fsharp
open Reified.DerivedSchema

[<DeriveSchema>]
type ProfileV1 = { Name: string }            // Profile v1 (frozen)

[<DeriveSchema>]
type Profile = { Name: string; Email: string } // Profile v2 (current: highest Vn + 1)
```

`ProfileV1` on its own, with no marked `Profile`, is not a series. It is an ordinary record whose schema happens to
have a `V1` in its name. The full rules, including explicit `Chain`/`Version`, are in
[Schema Inference](/schema/derivation/inference.html#version-series-inference).

### What is generated

Every version gets its own module with `schema`, `parse`, and `validate`, exactly as for any derived record (see
[Generated Code](/schema/derivation/generated-code.html)). The current version's module also gets one extra function,
`contract`:

| Generated binding | Type |
| --- | --- |
| `ProfileV1.schema` | `Schema<ProfileV1>`: the frozen v1 wire shape |
| `ProfileV1.parse` / `ProfileV1.validate` | as for any derived record |
| `Profile.schema` | `Schema<Profile>`: the current (v2) wire shape |
| `Profile.parse` / `Profile.validate` | parse or check **only** the current shape; no version handling |
| `Profile.contract` | `(ProfileV1 -> Result<Profile, MigrationError>) -> VersionSource -> Contract<Profile>` |

The generated `contract` is just the hand-written chain from the section above, with the versions filled in:

```fsharp no-check reason="Shape of emitted code."
    let contract
        (migrateV1ToV2: ProfileV1 -> Result<Profile, MigrationError>)
        (source: VersionSource)
        : Contract<Profile> =
        Contract.create "Profile" 2 schema
        |> Contract.supersedes 1 ProfileV1.schema migrateV1ToV2
        |> Contract.build source
```

**The generator does not write migrations.** It cannot know how a v1 value becomes a v2 value, so each migration is a
parameter that you supply. `contract` only chains the frozen schemas and your migrations in the right order:

```fsharp no-check reason="Depends on the generated Profile module."
let migrateV1ToV2 (v1: ProfileV1) : Result<Profile, MigrationError> =
    Ok { Name = v1.Name; Email = "" }

let profileContract : Contract<Profile> =
    Profile.contract migrateV1ToV2 (VersionSource.Field "schemaVersion")

// Reads v1 or v2 input and always returns the current Profile.
let load raw = Contract.parse profileContract raw
```

Use `Contract.parse profileContract` for stored or incoming data that may be any version. `Profile.parse` accepts only
the current shape.

### Adding a third version

The bare name always means "current", so adding a version means freezing the current record under a `Vn` name and
writing the new shape under the bare name. Existing version numbers never change.

1. Rename the current `Profile` to `ProfileV2`, and leave its fields exactly as they were. It is now frozen.
2. Declare the new shape as `Profile`. It becomes v3, because the highest `Vn` is now 2.
3. Keep the records in version order in one file: `ProfileV1`, `ProfileV2`, `Profile`.

```fsharp
[<DeriveSchema>]
type ProfileV1 = { Name: string }

[<DeriveSchema>]
type ProfileV2 = { Name: string; Email: string }                  // was Profile

[<DeriveSchema>]
type Profile = { Name: string; Email: string; Verified: bool }     // v3
```

After a build, the generated `contract` has one more parameter:

```fsharp no-check reason="Shape of emitted code."
    let contract
        (migrateV1ToV2: ProfileV1 -> Result<ProfileV2, MigrationError>)
        (migrateV2ToV3: ProfileV2 -> Result<Profile, MigrationError>)
        (source: VersionSource)
        : Contract<Profile> =
        Contract.create "Profile" 3 schema
        |> Contract.supersedes 2 ProfileV2.schema migrateV2ToV3
        |> Contract.supersedes 1 ProfileV1.schema migrateV1ToV2
        |> Contract.build source
```

The compiler now points at everything that needs attention:

- The existing `migrateV1ToV2` must return `ProfileV2` instead of `Profile`. Change its return type; the body usually
  stays the same.
- The `Profile.contract` call site needs the new `migrateV2ToV3`.
- Code that maps the current wire record into your domain type now sees the v3 `Profile`.

Parsing a v1 payload now runs `migrateV1ToV2` and then `migrateV2ToV3`. Payloads already stored as v1 or v2 are read by
their frozen schemas, so nothing has to be rewritten or renumbered.

`Contract` only reads. Whatever writes new payloads must stamp the new version number itself. Use
`Contract.currentVersion profileContract` rather than hard-coding the number, so the writer moves to 3 together with
the contract.

### Names that do not follow the convention

Set `Chain` and `Version` explicitly when a record's name should not carry a `Vn` suffix:

```fsharp
[<DeriveSchema(Chain = "Profile", Version = 1)>]
type LegacyProfile = { Name: string }

[<DeriveSchema>]
type Profile = { Name: string; Email: string }   // bare chain name: v2
```

The generated `contract` then takes `LegacyProfile -> Result<Profile, MigrationError>`. See
[Build Generation](/schema/derivation/msbuild.html) for setup.

## Design rules

- Keep every shipped wire schema frozen.
- Write and test each adjacent migration; do not infer renames or defaults automatically.
- Keep generated records at the wire tier and map the current value into the domain.
- Revalidation after migration ensures the result passes the current schema.
- Generated output is ordinary Schema DSL code; contract versioning does not introduce runtime reflection.
