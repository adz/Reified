---
title: Schema Inference
linkTitle: Inference
description: How schemagen turns F# records, fields, unions, comments, and constructors into Schema DSL.
weight: 30
targetFramework: net8.0
---

# Schema Inference

`schemagen` parses F# source and lowers marked declarations to typed Schema DSL. It does not load your assembly or
inspect types at runtime.

## Record shape

A derivable record must be:

- public and declared directly in a namespace or file-level module;
- non-generic;
- in a file with one namespace or one file-level module for generated declarations; and
- marked with `[<DeriveSchema>]`.

Every record field becomes one schema field in declaration order. By default the generated `construct` expression
creates a record literal. A single `[<SchemaConstructor>]` static member replaces that literal; it must accept fields in
declaration order and return the record type.

The generated module uses the record's type name and exposes `schema`, `parse`, and `validate`.
Generated code refers to your fields by name. Adding, removing, renaming, or changing a field without regenerating makes
F# compilation fail rather than allowing the schema to drift.

## Field type inference

| F# field type | Inferred schema |
| --- | --- |
| `string` | `Schema.text` |
| `int` | `Schema.int` |
| `decimal` | `Schema.decimal` |
| `bool` | `Schema.bool` |
| `DateOnly` | `Schema.date` |
| `DateTimeOffset` | `Schema.dateTime` |
| `Guid` | `Schema.guid` |
| `'a option` | optional field using the inferred `'a` schema |
| `'a list` | `Schema.listWith` the inferred element schema |
| `Map<string, 'a>` | `Schema.mapWith` the inferred value schema |
| `Map<'key, 'a>` where `'key` is a single-case union over `string` | `Schema.mapWithKey` using the union case as the reversible property-name conversion |
| another marked record in the same file | that record's generated schema |
| a fully qualified marked record in another project source file | that record's generated schema |
| a nullary discriminated union | enum schema; case names become tags |
| a `[<DeriveUnion>]` union | internally tagged union using `type`, with fieldless, directly named, and marked-record payload cases |
| a `[<DeriveUnion "field">]` union | the same internally tagged union with a custom discriminator |

The wire vocabulary is intentionally closed. Arrays, tuples, generic records, nested options, floating-point types,
map keys other than strings and transparent single-case string unions, other integer widths, and unknown application
types produce generation diagnostics. Use `list`, `decimal`, `int`, or an explicitly mapped domain boundary instead
of silently changing wire semantics.

Cross-file record and union references must use their fully qualified F# type paths. `schemagen` builds a project-wide
catalogue before generating companions; an ambiguous short name is rejected rather than resolved by compile order.

## Names

The default naming policy is `camel`, so `MarketingOptIn` becomes `marketingOptIn`. Set
[`ReifiedSchemaNaming`](/schema/derivation/msbuild.html#msbuild-properties) to `snake` or `verbatim` for the whole project. Use
`[<SchemaName "marketing_opt_in">]` on one field or nullary union case to override the policy locally.

## Options, supplied fields, and defaults

An `option` field represents wire absence and parses an omitted key as `None`. There is no second nested-option absence
axis. `[<Supplied>]` is different: it requires the key to occur in the input even when the typed value could otherwise
be produced. `[<Default value>]` supplies an omitted non-optional field and cannot be used on an option field.

## Constraints and metadata

Field attributes are lowered in source order to the operations documented in the
[attribute table](/schema/derivation/attributes.html#field-attributes). Constraints remain executable and inspectable, so parsing,
`Schema.check`, JSON Schema, forms, and diagnostics all observe the same rule. `Format` is metadata only. XML `///`
comments become `Schema.describe` metadata and generated XML documentation.

## Union inference

A union whose cases have no payload can be used as a field type without marking the union:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
type Plan = Free | Team | Enterprise

[<DeriveSchema>]
type Signup = { Plan: Plan }
```


Tags follow the naming policy and can be overridden with `[<SchemaName>]` on a case.

Payload unions opt into generation with `[<DeriveUnion>]`. A fieldless case may be mixed with directly named case
fields or cases carrying a marked record. Derived unions may also appear below another union payload or inside a list
or map:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
[<DeriveSchema>]
type Card = { LastFour: string }

[<DeriveSchema>]
type Bank = { Iban: string }

[<DeriveUnion>]
type Payment =
    | Cash
    | Credit of limit: decimal
    | Card of Card
    | Bank of Bank

[<DeriveSchema>]
type Checkout = { Payment: Payment }
```


The generated schema follows the handwritten [union schema rules](/schema/union-schemas.html). Each fully qualified
union type gets one generated `schema` binding. Records, nested union payloads, lists, maps, and references from other
source files reuse that binding instead of generating field-named copies. A generated `try<CaseName>Case` function
selects each direct-field case. Its `case` block names that function with `tryExtract`, then uses the same `fieldAs`,
`withSchema`, and `construct` vocabulary as a record schema. Multi-field cases use one private payload record so every
getter remains named and total.

Enum and transparent-wrapper schemas live in the generated file beside their F# type. Other generated files reuse
bindings such as `VariableType.schema`, `LocaleTag.schema`, and `LocaleTag.map` instead of repeating conversions.

Every direct case field must be named. `Credit of decimal` is rejected instead of publishing the compiler-generated
wire name `item`.

## Version-series inference

The generator examines the names and `[<DeriveSchema>]` arguments of every marked record in the project. It assigns
each record a **contract** (a fully qualified name such as `My.Wire.Profile`) and a **version** number. Records that
share a contract form its version series.

Each record is classified by the first rule that applies:

| Record | Contract | Version |
| --- | --- | --- |
| `[<DeriveSchema(Contract = "Profile", Version = 2)>]` | `Profile` in the record's own namespace or module | 2 (frozen) |
| `[<DeriveSchema(Contract = "My.Wire.Profile", Version = 2)>]` | `My.Wire.Profile`, exactly as written | 2 (frozen) |
| `[<DeriveSchema(Contract = "Profile")>]` | `Profile` in the record's own namespace or module | current |
| `[<DeriveSchema(Version = 2)>]` on `Profile` | the record's own name | 2 (frozen) |
| `ProfileV2`, where contract `Profile` exists in the same namespace or module | `Profile` | 2 (frozen), from the suffix |
| `ProfileV2`, where no contract `Profile` exists there | the record's own name, `ProfileV2` | current (a standalone record, not a series) |
| any other name, such as `Profile` | the record's own name | current |

A `Contract` value containing a dot is fully qualified. A value without a dot is relative to the record's own namespace
or module. "Contract `Profile` exists" means that some marked record is named `Profile`, or some record declares
`Contract = "Profile"`, in that container anywhere in the project.

**Frozen** versions state their number, either as a `Vn` suffix or as `Version = n`. The **current** version never
does: its number is always one more than the highest frozen version of its contract, or 1 when there are none. So
`ProfileV1`, `ProfileV2`, and `Profile` give `Profile` version 3, and that number moves up when you freeze a
`ProfileV3`. Stored payloads keep their meaning because frozen numbers never move.

A contract's versions may be spread across files, namespaces, and modules, subject to these checks, which are reported
as generation diagnostics:

- a contract has **exactly one current version**;
- versions are declared **oldest to newest in compile order, with no gaps**. The current version's generated
  `contract` builder refers to every older version's schema, so the older versions must compile first;
- no version number appears twice.

A contract with more than one version gets a `contract` builder on its current version. It takes one migration
parameter per adjacent step, `migrateV1ToV2`, `migrateV2ToV3`, and so on, plus a `VersionSource`. The generator never
writes the migrations themselves. A contract with a single version gets no builder. See
[Versioned Contracts](/schema/versioned-contracts.html#generate-a-version-series-from-records) for the generated
signatures and how to add a version.
