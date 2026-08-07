---
title: Packages and platforms
linkTitle: Packages and platforms
description: Schema product packages and their supported .NET and Fable JavaScript runtimes.
weight: 95
---

# Packages and platforms

Schema packages are independently installable. “Node and browser” means the package is compiled and exercised as Fable
JavaScript without depending on one of those hosts. Host-neutral packages can also run in other JavaScript environments
that provide the JavaScript primitives they use.

| Package | .NET | Fable JavaScript | JavaScript host | Purpose |
| --- | --- | --- | --- | --- |
| `Reified.Data` | Yes | Yes | Node and browser | Source-neutral data, human rendering, JSON rendering, and native-platform JSON conversions. |
| `Reified.Result` | Yes | Yes | Node and browser | `Result` composition and `result { }`. |
| `Reified.Constraint` | Yes | Yes | Node and browser | Reusable value checks and predicates. |
| `Reified.Refinements` | Yes | Yes | Node and browser | Invariant-carrying refined values. |
| `Reified.Parse` | Yes | Yes | Node and browser | Serialized primitive parsing. |
| `Reified.Schema` | Yes | Yes | Node and browser | Schema declaration, parsing, checking, accumulated errors, and inspection. |
| `Reified.Schema.Json` | Yes | Yes | Node and browser | Portable JSON-to-`Data` parsing and compiled typed JSON codecs. |
| `Reified.Schema.Http` | Yes, .NET 8+ | No | — | Host-neutral .NET HTTP boundary contracts and OpenAPI assembly. |
| `Reified.Schema.Contracts` | Yes, .NET 8+ | No | — | Repository tool-tier contract and record source generation; not packable. |
| `Reified.Schema.Contracts.Build` | Yes, .NET 8+ | No | — | MSBuild package that runs contract generation before compilation. |
| `Reified.Schema.Testing` | Yes, .NET 8+ | No | — | Repository-only FsCheck adapter; not packable. |

The Fable JavaScript build is a separate compilation of the same F# sources. A `netstandard2.1` target by itself does
not imply JavaScript support; the table records packages with an intentional Fable surface and repository coverage.

Schema declarations compile with Fable in both field forms:

```fsharp
field _.Email                     // derives the wire name "email"
fieldAs "email_address" _.Email   // declares it
```

`field` reads an F# quotation to derive the wire name, so it needs a Fable target that supports quotations:
JavaScript, TypeScript, Python and BEAM from Fable 5.10, Dart from 5.13. Fable's Rust and PHP targets have no
quotation support, so those declare names with `fieldAs`. All field typing, schema inference, constraints,
constructors, parsing, checking, and codecs are available on every target either way.
