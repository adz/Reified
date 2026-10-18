---
weight: 90
title: "Walkthrough: Reference Apps"
description: The reference application tiers, plain results and refined values, then a wire tier generated from records.
targetFramework: net8.0
---

# Walkthrough: Reference Apps

The reference apps exercise CLI, form, JSON, and storage boundaries. Two tiers are runnable:

1. [`Reified.ReferenceApp.Intro`](/constraints/reference-app.html), plain `Result`, checks,
   refined values, and accumulated validation, with no schemas at all. Start there if you are new; this page
   covers the schema tier.
2. `examples/Reified.ReferenceApp.Wire`, boundary discipline with the wire tier **generated** from
   `[<DeriveSchema>]` records.

## The generated-wire slice

```bash
dotnet run --project examples/Reified.ReferenceApp.Wire/Reified.ReferenceApp.Wire.fsproj --nologo
```


The wire slice generates schemas from ordinary records with constraint attributes:

```fsharp
open Reified.DerivedSchema

type Visibility =
    | Private
    | Public

[<DeriveSchema>]
type WorkspaceCard =
    { [<Min 1; Max 60>] Name: string
      [<Email; SchemaName "owner_email">] OwnerEmail: string
      [<Default "private">] Visibility: Visibility
      [<Distinct>] Members: string list }

    // Called by the generated schema instead of a record literal.
    [<SchemaConstructor>]
    static member create name (ownerEmail: string) visibility members =
        { Name = name; OwnerEmail = ownerEmail.ToLowerInvariant(); Visibility = visibility; Members = members }
```


`schemagen` writes the sibling `workspace.g.fs`: the schema pipeline you would have written by hand — `schema`,
`parse`, and `validate` — and, because `WorkspaceCardV1`/`WorkspaceCard` follow the version-chain
naming convention, a `WorkspaceCard.contract` builder that takes your typed v1 → v2 migration. No `Fields` module is
generated; the wire records are yours, and the generated code refers to them. You write the migration, the strict
domain mapping (`TrustedCard` rejects an owner listed as a member, a rule the wire deliberately cannot express), and a
head-version write through a compiled codec.

The constraint attributes stay on the wire records, so input is admitted through `Contract.parse` — which runs
`Schema.parse` on the selected frozen schema and therefore enforces `Min`/`Max`/`Email`/`Distinct`. The compiled codec
is used only to *write* an already-admitted head-version value, not to decode untrusted input. That is the difference
from the permissive-DTO strategy in [Schema or Generated DTO](/schema/schema-or-generated-dto.html): this app wants the
wire rules enforced at the boundary, so it reads with the boundary parser rather than the trusted decoder. Generated
schemas work with `JsonSchema.generate` and `Json.compile`.

See [Versioned Contracts](/schema/versioned-contracts.html) for the version-series attribute vocabulary and
[Build Generation](/schema/derivation/msbuild.html) for running generation in your build with `Reified.SchemaGen.MSBuild`.
