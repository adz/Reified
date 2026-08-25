<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/content/img/reified-logo-dark.svg">
    <img src="docs/content/img/reified-logo-light.svg" alt="Reified" width="420">
  </picture>
</p>

Declare value and model invariants once. Derive validation, parsing, diagnostics, codecs, contracts, and test data from the same declarations.

[![ci](https://github.com/adz/Reified/actions/workflows/ci.yml/badge.svg)](https://github.com/adz/Reified/actions/workflows/ci.yml)
[![release](https://github.com/adz/Reified/actions/workflows/release.yml/badge.svg)](https://github.com/adz/Reified/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/Reified.svg)](https://www.nuget.org/packages/Reified)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

## Why Reified

- **Type-safe data refinement and narrowing functions** — define reusable `Constraint<'value>` rules, check them with `Constraint.check`, and construct invariant-carrying values with `Refinement.create` and Reified's refined types.
- **Hierarchical error accumulation with structured, keyed paths** — `SchemaErrors`, `SchemaIssue`, and `SchemaPath` retain property names and collection indexes for precise API, form, and document errors.
- **First-class NativeAOT, trimming, and Fable support through explicit compile-time schemas** — schemas and compiled JSON codecs do not depend on runtime reflection.
- **One schema for parsing, validation, JSON codecs, JSON Schema, contracts, and test-data generation** — field names, value shapes, constraints, and constructors stay aligned.
- **Compiler-directed, type-safe JSON codecs** — compile codecs once from typed schemas without reflection or boxed object-array record construction.
- **Inspectable constraints rather than predicate-only validation** — built-in constraint metadata drives checking, diagnostics, JSON Schema export, and generation.
- **Accumulating boundary validation** — report independent field, nested-object, and collection failures together instead of stopping at the first error.
- **Explicit, evolution-friendly discriminated-union wire formats** — model existing JSON with internal, adjacent, external, or untagged representations; the recommended internal-tag format supports OpenAPI discriminators and mainstream code generation.
- **Build-time schema derivation for F# records** — `[<DeriveSchema>]` generates ordinary typed schemas at compile time without runtime reflection.
- **Portable structured data for .NET and JavaScript** — use the same `Data`, constraints, schemas, and core assertions on .NET and Fable.

## Declare the rule once

Most validation stacks keep the rule and its message in separate places. Reified makes a constraint inspectable data, so checking, diagnostics, export, and generation read the same declaration.

```fsharp
open Reified

let retryCount : Constraint<int> =
    Constraint.between 0 10

3 |> Constraint.check retryCount
// Ok ()

42
|> Constraint.check retryCount
|> Result.mapError Violation.render
// Error "expected a value between 0 and 10, but was 42"
```

Nobody wrote the failure sentence separately. Change the bounds and every interpreter observes the new rule.

## Declare a whole model

A schema describes how structured input becomes a model. It returns the typed value only after every field and constructor invariant succeeds.

```fsharp
open Reified
open Reified.ConstraintDSL
open Reified.SchemaDSL

type Signup =
    { Email: string
      Age: int }

let signupSchema =
    schema<Signup> {
        field _.Email {
            constraints [ present; email ]
        }
        field _.Age {
            constrain (atLeast 13)
        }
        construct (fun email age -> { Email = email; Age = age })
    }

match Schema.parse signupSchema input with
| Ok signup -> register signup
| Error errors -> display errors
```

The same `signupSchema` can drive a compiled JSON codec, JSON Schema, form metadata, versioned migrations, and matching test data.

## Read and write JSON from that same declaration

You do not write a second description of the wire shape. `Json.compile` turns the schema you already have into a codec that both encodes and decodes.

```fsharp
open Reified.Schema.Json

let codec = Json.compile signupSchema   // compile once, typically at startup

Json.serialize codec { Email = "ada@example.com"; Age = 36 }
// {"email":"ada@example.com","age":36}

Json.deserialize codec """{"email":"ada@example.com","age":36}"""
// { Email = "ada@example.com"; Age = 36 }

match Json.tryDeserialize codec """{"email":"ada@example.com","age":"thirty"}""" with
| Ok signup -> Some signup
| Error message -> None   // JSON decode failed at $.age: expected digit
```

The codec is compiled from the schema's typed field plan, so there is no runtime reflection and it stays AOT-, trimming-, and Fable-safe. It is the trusted-path counterpart to `Schema.parse`: it enforces the wire shape but skips constraint checking, because payloads from producers you trust already passed those checks. Untrusted input still goes through `Schema.parse`, which accumulates every violation with its path.

## Or derive the schema from the record itself

For wire records — DTOs whose whole job is to cross a boundary — declaring the schema by hand is duplication. Mark the record instead:

```fsharp
open Reified.DerivedSchema

[<DeriveSchema>]
type Signup =
    { [<Present; Email>]
      Email: string
      [<AtLeast 13>]
      Age: int }

Signup.schema     // Schema<Signup>
Signup.parse      // Data -> Result<Signup, SchemaErrors>
Signup.validate   // Signup -> Result<Signup, SchemaErrors>
```

This is the same schema as the handwritten one above — not an equivalent one. `Reified.Schema.Contracts.Build` reads the attributes from F# source at build time and generates ordinary constructor-last Schema DSL, which then compiles normally. The attributes are inert metadata; nothing is reflected over at runtime, and everything downstream — parsing, JSON codecs, JSON Schema, test data — works exactly as it does for a schema you wrote by hand.

Derivation is the preferred approach for DTOs. Keep it to public, permissive boundary records, and map the parsed result through a domain constructor so real invariants live in refined values and domain types rather than on the wire record.

## Packages

Install `Reified` to get the complete library, or install an individual package when you need only one capability.

- `Reified` — umbrella package that references all runtime packages
- `Reified.Constraint` — reusable, inspectable value rules and structured violations
- `Reified.Refinements` — types that carry an invariant after construction
- `Reified.Parse` — serialized primitive decoding
- `Reified.Result` — composition over the standard F# `Result` type
- `Reified.Data` — portable structured input and test data
- `Reified.Schema` — structured model admission, diagnostics, inspection, and JSON Schema
- `Reified.Schema.Json` — compiled JSON codecs
- `Reified.Schema.Contracts.Build` — MSBuild integration for derived record and wire contracts

The contract compiler and schema-derived testing adapter are repository tooling, not runtime packages.

`Reified.Schema.Contracts.Build` is not in the umbrella. MSBuild targets do not travel through a transitive package reference, so a project that derives schemas at build time references it directly.

## Documentation

- [Documentation](https://adz.github.io/Reified/)
- [Getting started](https://adz.github.io/Reified/getting-started/)
- [Schema and JSON](https://adz.github.io/Reified/schema/)
- [API reference](https://adz.github.io/Reified/api.html)

## Axial integration

[Axial](https://github.com/adz/Axial) describes asynchronous workflows with explicit failures and dependencies. Reified began as a library inside Axial and was forked out to stand alone; neither core depends on the other.
