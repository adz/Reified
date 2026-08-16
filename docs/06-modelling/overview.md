---
weight: 1
title: Reified.Schema
description: The core package's mental model, installation, and guides.
targetFramework: net8.0
---

# Reified.Schema

[Result](/validating-values/result/index.html) and the [Values](/validating-values/index.html) packages supply operations over individual values and ordinary
`Result` composition. Reified.Schema assembles checks, constraints, parsing steps, and refinements into declarations for
whole structured models. It adds field identity, path-aware accumulated errors, checked reconstruction, and multiple
interpreters; it does not replace the underlying value-level APIs.

## Mental Model

One schema declaration, several interpreters:

| Input | Interpreter | Result |
| --- | --- | --- |
| `Data` | `Schema.parse schema` | model or `SchemaErrors` |
| draft or imported value | `Schema.check schema` | the same value or `SchemaErrors` |
| schema | `Inspect.model` | finite metadata without execution |
| schema | `Json.compile` | reusable compiled JSON codec |
| schema | `JsonSchema.generate` | JSON Schema document |
| versioned `Data` | `Contract.parse` | current model or `ContractError` |
| schema | repository-only `SchemaGen.raw` / `SchemaGen.model` adapter | FsCheck generators |

`Schema.check` covers typed values that did not arrive as structured data: a draft assembled with an ordinary record
literal (named fields, any order, compiler-checked completeness), or an existing value from an import or database
mapper. It runs every field's constraints and refinements again and re-invokes the record constructor, so
cross-field invariants hold too. Success returns the value itself, not a proof wrapper — when every value of a type
must satisfy an invariant, give the type a private representation and a checked `Refinement`;
[Trusted Construction](/modelling/trusted-construction.html) shows how drafts keep record syntax and `with` updates alongside
that guarantee.

The declaration vocabulary covers primitive and refined values, nested models, lists, maps, optional values, three
tagged-union shapes, and recursive models. `Contract` keeps frozen wire versions and typed migrations outside the
current domain model.

## Guides

- [Getting Started](/getting-started/index.html) — one model built up in four stages: plain fields, refined fields, field
  constraints, and a private model behind a checked constructor. **Start here.**
- [Schema Overview Examples](/modelling/overview-examples.html) — short examples of inference, checked construction, refinement,
  recursion, and core interpreters.
- [Tutorials](tutorials/index.html) — parse a signup form, nest models, apply rules, and inspect metadata.
- [SchemaDSL](dsl.html) — constructor-last declarations and field blocks.
- [Derived Schemas](derivation/index.html) — generate declarations from F# records, configure MSBuild, and browse attributes.
- [Field Blocks and Plain Functions](/modelling/field-desugaring.html) — how `withSchema`, `constrain`, `refine`, and `validate`
  correspond to ordinary schema transformations.
- [Input Sources](/modelling/input-sources.html) — HTTP form-like, CLI, JSON-like, and configuration input.
- [Redisplay And Field Errors](/modelling/redisplay-and-field-errors.html) — failed parses that keep the user's input.
- [Trusted Construction](/modelling/trusted-construction.html) — checked public records, refined fields, and private aggregates.
- [Refined Value Schemas](/modelling/refined-values.html) — domain values like `Email` as reusable field schemas.
- [Union Schemas](/modelling/union-schemas.html) — tagged discriminated unions as schema fields.
- [JSON Codec](/json/index.html) — compile the same declaration into a runtime-reflection-free JSON codec for trusted payloads.
- [HTTP Servers](/http-contracts/index.html) — schema-trusted requests, problem details, and generated OpenAPI.
- [Versioned Contracts](/http-contracts/contracts.html) — evolve wire formats without freezing the domain model.
- [Recommended Patterns](patterns/index.html) — private aggregates, legal transitions, wire/domain separation, project layout,
  and the repository's test-adapter pattern.
- [Packages and Platforms](/notes/packages-and-platforms.html) — package boundaries and .NET/Fable JavaScript support.

## In Practice

- [Runnable Examples](/modelling/examples.html) — executed during the docs build, mirrored back into the site.
- [Benchmarks](/notes/benchmarks.html) — measured parse and codec numbers on .NET and Fable.
- [Compiler-Directed, AOT, and Fable](/notes/aot-trimming-fable.html) — why the guarantees hold by construction.
- Comparisons: [vs zod](/how-it-compares/zod-comparison.html), [vs FluentValidation](/how-it-compares/fluentvalidation-comparison.html).

## Installation

Schema installs as part of `Reified`.

Or install it individually with `dotnet add package Reified.Schema`.

Schema metadata, input parsing, checking, accumulated errors, and executable validation live in this package. Checks
and refined values arrive through its focused package dependencies, so Schema users do not need a second install.

`Reified.Schema.Json` is separate and optional: add it only if you want a compiled, runtime-reflection-free JSON codec generated from
your schema (`Json.compile`). Parsing, checking, rules, redisplay, and metadata inspection do not need it.

JSON Schema generation is not a separate package: `JsonSchema.generate` is a module in `Reified.Schema` itself.

`Reified.Schema.Json` also installs as part of `Reified`, or individually with `dotnet add package Reified.Schema.Json`.

See [JSON Codec](/json/index.html) for what that package buys you.
