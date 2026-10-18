---
title: Packages and platforms
linkTitle: Packages and platforms
description: What to install, and which packages run on .NET and on Fable JavaScript.
weight: 95
targetFramework: net8.0
---

# Packages and platforms

Install everything at once:

```sh
dotnet add package Reified
```


`Reified` is an umbrella package with no code of its own. It references every runtime package below.

Schema is the integrated path through the packages:

<div class="package-map">

```text
Constraint ─┐
Parse ──────┤
Data ───────┼──> Schema ───> contract tooling
Refinements ┘
```

</div>

Install `Reified.Schema` when structured input must become a trusted model. Constraint, Parse, Data, and Refinements can
each be used without Schema. Every package depends only on what it uses.

## .NET and Fable

These run on .NET and compile to Fable JavaScript, which is a separate compilation of the same F# sources. CI
compiles the Fable surface and runs it on Node; the compiled modules are declaration-only and marked side-effect-free,
so browsers can tree-shake them, and they work in other JavaScript environments that provide the primitives they use.
The repository has no browser test suite, so browser support is verified by that side-effect-free compile, not by
execution in a browser.

| Package | What it gives you | Install |
| --- | --- | --- |
| `Reified.Constraint` | Reusable, inspectable value rules and structured violations | `dotnet add package Reified.Constraint` |
| `Reified.Refinements` | Types that carry an invariant after construction | `dotnet add package Reified.Refinements` |
| `Reified.Parse` | Serialized primitive decoding | `dotnet add package Reified.Parse` |
| `Reified.Data` | Source-neutral structured data, human and JSON rendering, native JSON conversion | `dotnet add package Reified.Data` |
| `Reified.Schema` | Schema declaration, parsing, checking, accumulated errors, inspection, JSON Schema, lossless JSON-to-`Data` parsing, and compiled typed JSON codecs; ships NativeAOT build settings for [records with many fields](/notes/aot-trimming-fable.html#records-with-many-fields) | `dotnet add package Reified.Schema` |
| `Reified.Result` | Independent `Result` composition and `result { }` | `dotnet add package Reified.Result` |

A `netstandard2.1` target by itself does not imply JavaScript support. This list records the packages with a deliberate
Fable surface and repository coverage to keep it working.

### Declaring fields under Fable

Both field forms compile with Fable. `field` reads an F# quotation to derive the wire name, so it needs a target
that supports quotations; use `fieldAs` where quotations are unavailable. Field typing, schema inference, constraints,
constructors, parsing, checking, and codecs are available with either form on every target; the .NET-only conveniences
such as `DateOnly` and `Data.ofJsonDocument` are compile-time gated out of the Fable surface. The target/version table
and both field forms live in
[Compiler-Directed: AOT and Fable](/notes/aot-trimming-fable.html#derived-wire-names-under-fable).

## Development tools

These generate code or test data at development time. They are .NET 8+ and have no Fable surface, because nothing they
produce runs in your application at run time.

| Tool | What it does | How to get it |
| --- | --- | --- |
| `Reified.SchemaGen.MSBuild` | Runs schema generation over your `[<DeriveSchema>]` records and `.contract` files before each compile | `dotnet add package Reified.SchemaGen.MSBuild` |
| `Reified.SchemaGen` | The generation library itself, the record frontend, the `.contract` parser, and the emitter | Not published; used through the build package above |
| `Reified.Schema.Testing` | Derives FsCheck generators from a schema so tests produce accepted values | Not published; copy or adapt the pattern from this repository |

Install `Reified.SchemaGen.MSBuild` separately from the `Reified` umbrella: MSBuild
`build/` assets are not transitive, so an umbrella dependency would install the targets without ever running them. Add
it to the project whose records you want generated.

`Reified.SchemaGen` and `Reified.Schema.Testing` are not packable.
See [Derived Schemas](/schema/derivation/index.html) and [Testing patterns](/data/testing-schema-guarantees.html).
