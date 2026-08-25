# Reified Plan

This file tracks current product and architecture direction.
High-level durable decisions live in `dev-docs/decisions/`.
Speculative sketches live in `dev-docs/current-ideas/`, but this file is the live direction.

## Release Strategy

The boundary stack — `Reified.Result`, `Reified.Constraint`, `Reified.Refinements`, and `Reified.Parse` as focused
packages, plus `Reified.Schema` and `Reified.Schema.Json` — is the 1.0 gate. The Acceptance Checks in
`dev-docs/TASKS.md` are the concrete, checkable form of that gate; there is no separate adoption-target project
tracked against it. As of 2026-08-14 all eight checks pass, including a docgen xref-rewriting fix
(`scripts/docgen/Program.fs`) that resolved six broken generated reference links (stale `SchemaCeBuilder`-nested
slugs, a `Field<'model,'value>` disambiguation-suffix mismatch, and `DateRange` — a type abbreviation with no
standalone page). The contract-declaration
thread originally sequenced versioning/migration machinery before the grammar; in practice the grammar and generator
shipped first (2026-07-12, single-version wire-tier scope), and the versioning/migration engine shipped on 2026-07-13.
Record-first `[<DeriveSchema>]` generation is now the primary generated path; `.contract` remains a secondary wire-tier
form with no planned LSP investment. The schema surface has been through heavy recent churn (direct `Result` returns
and contracts) and should be treated as settling rather than settled.

Documentation tooling moved from Hugo/Docsy to FsLiveDocs on 2026-08-17 (`dev-docs/decisions/README.md` has the
full entry). `dotnet livedocs audit`, `build`, and `capture` all pass for the current `./docs` tree; a real release
capsule, `artifacts/livedocs/Reified-0.7.0-livedocs.zip`, has been captured and inspected clean (222 entities, 1,497
members, SHA-256 `d761e028c2e5a39d93906fdd4ac4068c6cdcacaf6b9d9682d3c126aac238e66a`) against the published FsLiveDocs
0.3.3. Reaching a working `capture` took two FsLiveDocs fixes: the doubly-nested-module bug (0.3.2) and a
type-abbreviation member collision (`DateRange = Interval<DateTimeOffset>` re-reporting `Interval`'s own members,
fixed as 0.3.3 — see `dev-docs/decisions/README.md`). Both versions are on NuGet; `.config/dotnet-tools.json` pins
0.3.3 with no local pack or private feed involved.

The homepage and header logo were also broken right after the migration: `.livedocs/config.json` had no logo
configured at all, and the stylesheet was untouched Hugo/Docsy CSS that doesn't match FsLiveDocs' real
DaisyUI/Tailwind markup, so essentially none of it applied. Fixed 2026-08-17: logo config added, and
`docs/content/reified-theme.css` rewritten against the actual generated DOM (see Axial's equivalent stylesheet for
the working pattern this follows) with Reified's own light/dark identity.

## Current Direction

Reified describes; it does not run. Effects left with Flow when the repository split, so everything here is a
value: a rule, a shape, a diagnostic, a contract. The public surface has two identities:

- **Focused values and failures**: ordinary `Result` composition in `Reified.Result`; reusable constraints in
  `Reified.Constraint`; primitive parsing in `Reified.Parse`; and invariant-carrying types in
  `Reified.Refinements`. Each has its own documentation section.
- **Schema**: structured input, accumulated path-aware errors, model construction, codecs, contracts, and boundary
  interpreters.

Each package installs on its own. The `Reified` umbrella package installs all of them at once and adds no API: it
has no sources and no assembly, only dependencies, so a type never has a second place it could come from.
`Reified.ErrorHandling` is not coming back — a grouping that is not a capability does not earn a package.

Reified's data-boundary direction splits concerns like this:

- `Data` is the owned source-neutral structured-value model and fixture language. Its Phase 1 surface covers recursive
  literals, strict immutable edits, named variations, bounded matrices, paths and extraction, exact structural diffs,
  recursive partial patterns, selective produced-data proofs, concise human rendering, and deterministic JSON rendering. `Data` preserves number
  tokens, object order, and duplicate fields; operations state their equality and selection semantics explicitly.
- `Constraint<'value>` describes reusable, path-free, raw-input-free value rules; `Constraint.check` runs one
- `Schema<'value>` describes typed shape, construction, inspection, and portable constraint metadata; `Schema.parse`
  admits structured data and `Schema.check` rechecks an already assembled typed value through its field schemas and record
  constructor. Successful operations return the ordinary value rather than a universal trust wrapper.
- schema interpreters parse structured data, check existing values, produce diagnostics, and drive non-validation metadata
  consumers

Core schema declarations and their interpreters share the single `Reified.Schema` namespace and package (module names,
not namespaces, separate declaration from interpretation); the package stays independent of any execution model.

Constructor-level intrinsic errors are a second stage after field parsing and field constraints, not an error source that
runs alongside invalid fields. If any field or nested item has intrinsic diagnostics, interpreters must not apply the
model constructor; constructor errors are reported only when every constructor argument is already trusted. By default
constructor errors attach to the current object path, and input parsing may expose an option to attach them to a relative
field path when that gives better boundary feedback.

Schema work should prove the portable metadata model before growing broad interpreters. The metadata slice — field
ordering, primitive value schemas, schema constraints as inspectable metadata, erasing those constraints for execution,
and constructor/getter alignment — is proven. Constructor-last computation expressions are the sole public record
authoring surface:

```fsharp
schema<Customer> {
    field "id" _.Id
    field "name" _.Name
    construct ctor
}
```

`schema<'model>` anchors the model type. `field` resolves canonical schemas; optional field blocks apply `withSchema`,
`constrain`, type-directed `refine`, and executable `validate`. The typed field chain lets `construct` or
`constructResult` match the closing constructor by arity and position.
Build-time generation exists as wire-tier tooling: `[<DeriveSchema>]`-marked records are the
primary declaration (FCS syntax-only frontend in `src/Reified.Schema.Contracts`, run by `scripts/schemagen` or the
`Reified.Schema.Contracts.Build` MSBuild package), with `.contract` files as the parked secondary form. Generated contracts
remain wire-tier records; domain models stay hand-written F# rather than becoming a second generated authoring surface.

The public schema-authoring vocabulary keeps `field` plus the field-block operations.
`Schema.text`, `Schema.int`, `Schema.decimal`, `Schema.bool`,
`Schema.date`, `Schema.dateTime`, and `Schema.guid` are the primitive `Schema<'value>` values, and composites
(`Schema.list<'item>()`, `Schema.option`, `Schema.map<'item>()`, `Schema.union`, `Schema.unionWith`, `Schema.enum`, `Schema.defer`)
and refined/domain schemas fill `withSchema` inside a field block. Do not introduce competing primitive aliases such as `string`, `integer`,
`boolean`, `uuid`, `dateOnly`, or `Field.text`; the `Value` module is internal implementation, not public vocabulary.

Collection members are type-directed. `field` recursively resolves list item schemas, while standalone lists and
string-keyed maps use `Schema.list<'item>()` and `Schema.map<'item>()`. `listWith` and `mapWith` are the explicit escape
hatches for recursive or locally configured member schemas. `Syntax.constrainItems` and `Syntax.constrainValues` apply
typed constraints inside a collection; ordinary `Schema.constrain` applies to the collection itself. Non-string map
keys have no inferred wire representation.

Schema must also preserve a high-performance codec lowering path. The inspectable schema model may contain rich metadata,
but JSON codecs should not interpret that metadata tree directly on the hot path. A codec interpreter must be able to
compile schemas into direct record plans: ordered field descriptors, cached wire-name bytes, indexed field slots,
typed field decoders, and constructor application that does not require per-value reflection or `obj array` dispatch.
CodecMapper is the performance reference for this shape. This path now ships as `Reified.Schema.Json` (`Json.compile` over the
retained compiled record plan, benchmarked against `System.Text.Json` in `benchmarks/Reified.Schema.Benchmarks/CodecSuites.fs`);
remaining codec work is optimization and format breadth, not proving the shape.

The built `Schema<'model>` value itself must retain typed constructor and field information sufficient for that codec
compilation: type erasure at authoring time must not force interpreters onto boxed `obj array` dispatch or require
callers to re-supply the constructor and typed fields alongside the schema. Codec compilers walk the retained typed
shape to emit constructor-specialized record plans (CodecMapper's `MappingDefinition` / `Specialize` dual-view pattern).

Runtime reflection must not be the foundation for schema construction, constructor binding, validation, or codec
execution. Reflection can be an optional import/tooling path on .NET, but the core authored schema path must remain AOT-
and trimming-safe and must have a Fable-compatible fallback. If ergonomic boilerplate becomes painful, prefer build-time
generation layered over explicit schemas rather than reflection-heavy runtime discovery.

## Boundary With Axial

`Axial` runs what Reified describes: its optional server adapters execute `Reified.Schema.Http` contracts, and its
workflow model is never required to use one. The dependency points one way, from Axial to Reified, and only through
published packages. Do not add an execution concept — a workflow type, a service contract, an ambient runtime — to
this repository to close that gap; add it to Axial, or leave the caller an ordinary function.
