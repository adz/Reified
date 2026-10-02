---
title: Derived Schemas
linkTitle: Derived Schemas
description: Generate Schema declarations from ordinary F# records and attributes.
type: docs
weight: 60
targetFramework: net8.0
---

# Derived Schemas

`[<DeriveSchema>]` removes the mechanical duplication between a wire record and its `Schema<_>` declaration. The
build reads ordinary F# source, infers a schema from each marked record, and generates normal constructor-last Schema
DSL code before F# compilation.

```fsharp no-check reason="Declares its own namespace as the first thing in the file, which cannot follow the site's F# prelude opens; not independently checkable."
namespace MyApp.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Signup =
    { [<Email; Present>]
      Email: string
      [<AtLeast 18>]
      Age: int
      Tags: string list }
```


The generated companion module provides:

```fsharp no-check reason="Generated companion members produced by the schemagen build package; the generator does not run in this audit."
Signup.schema    // Schema<Signup>
Signup.parse     // Data -> Result<Signup, SchemaErrors>
Signup.validate  // Signup -> Result<Signup, SchemaErrors>
```


This is source generation, not runtime reflection. The attributes are inert metadata; the generated F# uses the same
[Schema DSL](/schema/dsl.html) as a hand-written declaration and therefore works with parsing, inspection, JSON codecs,
JSON Schema, NativeAOT, trimming, and Fable.

`Reified.SchemaGen` matches `[<DeriveSchema>]` by its bare attribute name in the source syntax tree, before the
project type-checks, not by resolving it to the specific `Reified.DerivedSchema.DeriveSchemaAttribute` type. An
unrelated type of your own also named `DeriveSchema`, in scope in the same file, would match the same way.

## Guides

- [Set up build generation](/schema/derivation/msbuild.html), package reference, generated-file modes, and MSBuild properties.
- [Attributes](/schema/derivation/attributes.html), every supported attribute and its Schema DSL equivalent.
- [How inference works](/schema/derivation/inference.html), records, field types, names, constructors, unions, and diagnostics.
- [Generated code](/schema/derivation/generated-code.html), exactly what the companion file contains, and where your own mapping code goes.

## When to derive

Derivation is intended for public, permissive boundary records: a wire shape you do not own, or one with frozen
versions that must keep decoding. Keep domain invariants in refined values or private domain types, then map the
parsed wire record through a domain constructor; see [Separate Wire and Domain Models](/schema/patterns/wire-and-domain-models.html).
If you own the model end to end and it has no version history, write the `Schema<'model>` by hand instead and
skip the DTO. See [Schema or Generated DTO](/schema/schema-or-generated-dto.html) for the full decision.
