---
weight: 90
title: "Walkthrough: Reference Apps"
description: The reference application tiers — plain results and refined values, then a wire tier generated from records.
---

# Walkthrough: Reference Apps

The reference apps exercise the schema group the way an application does — values crossing CLI, form, JSON, and
storage boundaries — so that API friction invisible in a snippet has nowhere to hide. Two tiers are runnable here:

1. [`Reified.ReferenceApp.Intro`]({{% relref "/values/reference-app.md" %}}) — plain `Result`, checks,
   refined values, and accumulated validation, with no schemas at all. Start there if you are new; this page
   covers the schema tier.
2. `examples/Reified.ReferenceApp.Wire` — boundary discipline with the wire tier **generated** from
   `[<DeriveSchema>]` records.

The integration reference application — the workspace tracker that adds effectful use cases and two HTTP hosts on
one declaration — lives in the [Axial repository](https://github.com/adz/Axial), because serving a Reified
contract is what its adapters are for. Nothing on this page requires it.

## The generated-wire slice

```bash
dotnet run --project examples/Reified.ReferenceApp.Wire/Reified.ReferenceApp.Wire.fsproj --nologo
```

The wire slice answers the question the hand-written tier leaves open: what does the day-to-day authoring
experience look like once wire schemas are generated? You own an ordinary record with constraint attributes:

```fsharp
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

`schemagen` writes the sibling `workspace.g.fs`: the schema pipeline you would have written by hand, `parse` and
`validate`, typed `Fields` references, and — because `WorkspaceCardV1`/`WorkspaceCard` follow the version-chain
naming convention — a `WorkspaceCard.contract` builder that takes your typed v1 → v2 migration. The hand-written
surface shrinks to exactly the parts that carry meaning: the migration, the strict domain mapping (`TrustedCard`
rejects an owner listed as a member — a rule the wire deliberately cannot express), and a head-version write
through a compiled codec. Generated schemas are ordinary schemas, so `JsonSchema.generate` and `Json.compile`
come along for free.

See [Versioned Contracts]({{% relref "/schema/contracts.md" %}}) for the full attribute vocabulary, the
`.contract` grammar alternative, and running generation in your build with `Reified.Schema.Contracts.Build`.

## Serving the same declaration over HTTP

`Reified.Schema.Http` owns structured data, RFC 9457 problem details, and OpenAPI assembly as values — it never
opens a socket. The host adapters that execute those contracts, and the twin ASP.NET Core and GenHTTP reference
APIs that prove the boundary is host-neutral, live in the
[Axial repository](https://github.com/adz/Axial). Routing and app wiring remain the host's
([HTTP Servers]({{% relref "/schema/http-servers.md" %}})).
