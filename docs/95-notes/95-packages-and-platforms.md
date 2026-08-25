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


`Reified` is an umbrella. It has no code of its own — it just references every runtime package below, so you get the
whole set and can ignore the rest of this page.

Install one package instead when you want one capability and nothing else. Every package is independently installable
and depends only on what it needs.

## .NET and Fable

These run on .NET and compile to Fable JavaScript, which is a separate compilation of the same F# sources. Each is
exercised on Node and in the browser without depending on either host, so they also work in other JavaScript
environments that provide the primitives they use.

| Package | What it gives you | Install |
| --- | --- | --- |
| `Reified.Result` | `Result` composition and `result { }` | `dotnet add package Reified.Result` |
| `Reified.Constraint` | Reusable, inspectable value rules and structured violations | `dotnet add package Reified.Constraint` |
| `Reified.Refinements` | Types that carry an invariant after construction | `dotnet add package Reified.Refinements` |
| `Reified.Parse` | Serialized primitive decoding | `dotnet add package Reified.Parse` |
| `Reified.Data` | Source-neutral structured data, human and JSON rendering, native JSON conversion | `dotnet add package Reified.Data` |
| `Reified.Schema` | Schema declaration, parsing, checking, accumulated errors, inspection, JSON Schema | `dotnet add package Reified.Schema` |
| `Reified.Schema.Json` | Lossless JSON-to-`Data` parsing and compiled typed JSON codecs | `dotnet add package Reified.Schema.Json` |

A `netstandard2.1` target by itself does not imply JavaScript support. This list records the packages with a deliberate
Fable surface and repository coverage to keep it working.

### Declaring fields under Fable

Both field forms compile with Fable:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
field _.Email                     // derives the wire name "email"
fieldAs "email_address" _.Email   // declares it
```


`field` reads an F# quotation to derive the wire name, so it needs a target that supports quotations: JavaScript,
TypeScript, Python, and BEAM from Fable 5.10, and Dart from 5.13. Fable's Rust and PHP targets have no quotation
support, so declare names there with `fieldAs`. Everything else — field typing, schema inference, constraints,
constructors, parsing, checking, and codecs — is available on every target either way.

## Development tools

These generate code or test data at development time. They are .NET 8+ and have no Fable surface, because nothing they
produce runs in your application at run time.

| Tool | What it does | How to get it |
| --- | --- | --- |
| `Reified.Schema.Contracts.Build` | Runs schema generation over your `[<DeriveSchema>]` records and `.contract` files before each compile | `dotnet add package Reified.Schema.Contracts.Build` |
| `Reified.Schema.Contracts` | The generation library itself — the record frontend, the `.contract` parser, and the emitter | Not published; used through the build package above |
| `Reified.Schema.Testing` | Derives FsCheck generators from a schema so tests produce accepted values | Not published; copy or adapt the pattern from this repository |

`Reified.Schema.Contracts.Build` is the one you install. It is deliberately kept out of the `Reified` umbrella: MSBuild
`build/` assets are not transitive, so an umbrella dependency would install the targets without ever running them. Add
it to the project whose records you want generated.

`Reified.Schema.Contracts` and `Reified.Schema.Testing` are not packable, and there is nothing to install for either.
See [Derived Schemas](/schema/derivation/index.html) and [Testing patterns](/data/testing-schema-guarantees.html).
