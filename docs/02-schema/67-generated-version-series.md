---
weight: 67
title: Generated version series
description: Group marked records into a versioned contract and let the generator write the wiring, migrations included.
targetFramework: net8.0
---

# Generated version series: freezing and migrating

The generator can group `[<DeriveSchema>]` records into a version series and write the `Contract` wiring for you. It
does this by examining the names and attribute arguments of the marked records in the project. You still write each
migration by hand; the generator only chains the frozen schemas and your migrations in the right order.

See [Handwritten Versioned Contracts](/schema/versioned-contracts.html) for the `Contract` model this generates, and
[Build Generation](/schema/derivation/msbuild.html) for setup.

## How records are grouped

By default, a marked record whose name ends in `V` followed by a number (`ProfileV1`, `ProfileV2`) belongs to the
contract named by the rest of its name, **but only when a marked record named `Profile` exists in the same namespace
or module**. The bare record is the current version. Its version number is never written down; it is always one more
than the highest frozen version.

```fsharp
open Reified.DerivedSchema

[<DeriveSchema>]
type ProfileV1 = { Name: string }            // Profile v1 (frozen)

[<DeriveSchema>]
type Profile = { Name: string; Email: string } // Profile v2 (current: highest Vn + 1)
```

`ProfileV1` on its own, with no marked `Profile`, is not a series. It is an ordinary record whose schema happens to
have a `V1` in its name. The full rules, including explicit `Contract`/`Version`, are in
[Schema Inference](/schema/derivation/inference.html#version-series-inference).

## What is generated

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

The generated `contract` is just the hand-written chain from the handwritten page, with the versions filled in:

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

## Adding a third version

The bare name always means "current", so adding a version means freezing the current record under a `Vn` name and
writing the new shape under the bare name. Existing version numbers never change.

1. Rename the current `Profile` to `ProfileV2`, and leave its fields exactly as they were. It is now frozen.
2. Declare the new shape as `Profile`. It becomes v3, because the highest frozen version is now 2.
3. Keep the records in version order: `ProfileV1`, `ProfileV2`, then `Profile`. They can share a file, or the frozen
   versions can move to an earlier file (see [Keep frozen versions elsewhere](#keep-frozen-versions-elsewhere)).

```fsharp isolated
open Reified.DerivedSchema

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

## Names that do not follow the convention

`Contract` names the contract a record belongs to, and `Version` pins a frozen version number. Leave `Version` off the
current version:

```fsharp
[<DeriveSchema(Contract = "Profile", Version = 1)>]
type LegacyProfile = { Name: string }

[<DeriveSchema(Contract = "Profile")>]
type ProfileWire = { Name: string; Email: string }   // current: v2
```

The generated `ProfileWire.contract` takes `LegacyProfile -> Result<ProfileWire, MigrationError>` and builds a
`Contract<ProfileWire>` named `"Profile"`. A contract has exactly one current version; a second record without a
version is reported as a generation error.

## Keep frozen versions elsewhere

A contract's versions do not have to share a file, namespace, or module. The only requirement is compile order: the
current version's `contract` builder refers to every older schema, so older versions must be declared first.

A bare `Contract` name is relative to the record's own namespace or module. A dotted name is fully qualified, which
lets frozen versions live in a separate container:

```fsharp no-check reason="Declares its own namespace or module, which cannot follow the site's F# prelude opens."
// Profile.History.fs: compiled first, never edited again
module MyApp.Wire.History

open Reified.DerivedSchema

[<DeriveSchema(Contract = "MyApp.Wire.Profile", Version = 1)>]
type ProfileV1 = { Name: string }
```

```fsharp no-check reason="Declares its own namespace or module, which cannot follow the site's F# prelude opens."
// Profile.fs
namespace MyApp.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Profile = { Name: string; Email: string }   // current: v2
```

```xml
<Compile Include="Profile.History.fs" />
<Compile Include="Profile.fs" />
```

The generated `Profile.contract` refers to the frozen version by its full name,
`MyApp.Wire.History.ProfileV1`. Declaring `Profile.fs` first is reported as a compile-order error. `ProfileV1` without
the `Contract` argument would not join here, because the `Vn` convention only looks in its own namespace or module.
When both files use the same namespace, the convention works across files without any arguments.

See [Build Generation](/schema/derivation/msbuild.html) for setup.
