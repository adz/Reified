---
weight: 65
title: Handwritten Versioned Contracts
description: Keep frozen wire schemas readable through explicit, typed migrations you write by hand.
targetFramework: net8.0
---

# Handwritten Versioned Contracts

A `Contract<'model>` is a chain of frozen wire schemas and explicit migrations. Use it when stored configuration,
queued messages, or events must remain readable after their wire shape changes. Contract versioning belongs at the
wire boundary; map the current wire value into a strict domain type afterwards.

This page declares a contract by hand. For generating the version series from `[<DeriveSchema>]` records, see
[Generated version series](/schema/generated-version-series.html). Schema derivation itself is documented separately
in [Derived Schemas](/schema/derivation/index.html).

## Wire and domain models

A wire model describes what the format can carry. Keep it public and permissive. A domain model describes what business
code may rely on and should protect its invariants with refined values or private construction.

```fsharp no-check reason="Order/OrderError are the application's own domain type and error case; not ones this page can construct standalone."
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

```fsharp isolated
open Reified
open Reified.SchemaDSL

type ConfigV1 = { Host: string }

let configV1Schema : Schema<ConfigV1> =
    schema<ConfigV1> {
        field _.Host
        construct (fun host -> { Host = host })
    }

type Config = { Host: string; Port: int }

let configSchema : Schema<Config> =
    schema<Config> {
        field _.Host
        field _.Port
        construct (fun host port -> { Host = host; Port = port })
    }

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
parses it, then runs each adjacent migration in order, so every intermediate version is visited and none are skipped.
When the input version was not already current, the migrated result is re-checked against the current schema and a
failure becomes `MigrationError.RevalidationFailed`. When the input version was already current, the initial parse is
the check, so no second pass runs. There is no automatic structural migration.

## Version sources

`Contract.build` chooses how the input version is discovered. The three cases are listed in the
[`VersionSource` API reference](/api/Reified.VersionSource.html); the example above reads a positive integer from a
`schemaVersion` input field. Use `VersionSource.External` when the caller knows the version out of band and calls
`Contract.parseVersion`, or `VersionSource.UnversionedMeans 1` to treat input without a marker as one registered
version.

```fsharp no-check reason="Continues the configContract illustration above; raw is the caller's own structured input, not one this fragment constructs standalone."
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
`Schema.parse`. A `MigrationError.MigrationFailed` written by your own migration carries only its message string, so
no paths are preserved through hand-written migration failures.

## Design rules

- Keep every shipped wire schema frozen.
- Write and test each adjacent migration; do not infer renames or defaults automatically.
- Keep generated records at the wire tier and map the current value into the domain.
- Revalidation after migration ensures the result passes the current schema.
- Generated output is ordinary Schema DSL code; contract versioning does not introduce runtime reflection.
