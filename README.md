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

## How the packages fit

<pre>
Constraint ─┐
Parse ──────┤
Data ───────┼──> Schema ───> contract tooling
Refinements ┘
</pre>

Install Schema when structured input must become a model. Constraint, Parse, Data, and Refinements also work
without Schema.

## Why Reified

Boundary code often repeats model rules in validators, decoders, error handling, API contracts, and tests. Reified
keeps those rules in a typed declaration shared by validation, codecs, contracts, and tests.

- **Type-safe data refinement and narrowing functions** — once a value has passed its rules, represent that fact in its type instead of checking it again throughout the application.
- **Hierarchical error accumulation** — get a structured, keyed diagnostic tree that keeps property names and collection indexes intact, so an API or UI can put each message in the right place.
- **One definition of the model** — parsing, validation, JSON codecs, JSON Schema, contracts, and generated test data share field names, shapes, and rules.
- **Compatible discriminated unions** — use a tooling-friendly default for new JSON, or describe the established representation when integrating with an existing serializer.
- **Schema derivation for boundary records** — generate the declaration at build time when a record already contains the necessary wire-format rules.
- **First-class AOT compilation and Fable support through explicit schemas** — avoid runtime reflection, run the same model on .NET, and compile it to JavaScript.

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

Change the bounds and each interpreter reads the updated rule.

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
open Reified

let codec = Json.compile signupSchema   // compile once, typically at startup

Json.serialize codec { Email = "ada@example.com"; Age = 36 }
// {"email":"ada@example.com","age":36}

Json.deserialize codec """{"email":"ada@example.com","age":36}"""
// { Email = "ada@example.com"; Age = 36 }

match Json.tryDeserialize codec """{"email":"ada@example.com","age":"thirty"}""" with
| Ok signup -> Some signup
| Error message -> None   // JSON decode failed at $.age: expected digit
```

The codec is compiled from the schema's typed field plan, so there is no runtime reflection and it stays AOT-, trimming-, and Fable-safe. For NativeAOT, the package raises the compiler's generic-cycle limits so records with many fields decode; see [AOT notes](https://adz.github.io/Reified/notes/aot-trimming-fable.html#records-with-many-fields). It is the trusted-path counterpart to `Schema.parse`: it enforces the wire shape but skips constraint checking, because payloads from producers you trust already passed those checks. Untrusted input still goes through `Schema.parse`, which accumulates every violation with its path.

## Or derive the schema from the record itself

Declaring the schema by hand is duplication when a record already carries its field rules as attributes. Mark the record instead:

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

This is the same schema as the handwritten one above — not an equivalent one. `Reified.SchemaGen.MSBuild` reads the attributes from F# source at build time and generates ordinary constructor-last Schema DSL, which then compiles normally. The attributes are inert metadata; nothing is reflected over at runtime, and everything downstream — parsing, JSON codecs, JSON Schema, test data — works exactly as it does for a schema you wrote by hand.

Derivation with constraint attributes is the preferred way to declare a validated boundary model. The example above is the same schema as the handwritten `signupSchema`: admit untrusted input with `Signup.parse`, which runs every attribute exactly as `Schema.parse` runs the handwritten constraints. Keep the record public and boundary-facing, and map the parsed result through a domain constructor so real invariants live in refined values and domain types rather than on the wire record.

A derived permissive wire DTO is a different choice. Generate it with no constraint attributes and decode it with the trusted JSON path — `Json.compile` plus `Json.deserialize`/`Json.tryDeserialize` — then run the real validation once in your own `toDomain` function. The trusted decoder does not read constraint attributes, so an attributed DTO decoded that way looks validated in the editor but is not. See [Schema or Generated DTO](https://adz.github.io/Reified/schema/schema-or-generated-dto.html) for how to choose between the two.

## Packages

Install `Reified` to get the complete runtime set. For the integrated boundary model, start with `Reified.Schema`:

Every package carries a maturity rating — Experimental, Incubating, or Stable — declared in its project file and
published in its NuGet metadata. See [Package maturity](https://adz.github.io/Reified/notes/package-maturity.html)
for the levels, the promotion rule, and the application that justifies each package's rating.

- `Reified` — umbrella package that references all runtime packages
- `Reified.Schema` — structured model admission, diagnostics, inspection, JSON Schema, and compiled JSON codecs
- `Reified.SchemaGen.MSBuild` — MSBuild integration for derived record and wire contracts

Schema builds on focused packages that remain useful by themselves:

- `Reified.Constraint` — reusable, inspectable value rules and structured violations
- `Reified.Refinements` — types that carry an invariant after construction
- `Reified.Parse` — serialized primitive decoding
- `Reified.Data` — portable structured input and test data

`Reified.Result` is independent composition over the standard F# `Result` type. It works with Reified APIs but is not
part of Schema's dependency chain.

The contract compiler and schema-derived testing adapter are repository tooling, not runtime packages.

`Reified.SchemaGen.MSBuild` is not in the umbrella. MSBuild targets do not travel through a transitive package reference, so a project that derives schemas at build time references it directly.

## Documentation

- [Documentation](https://adz.github.io/Reified/)
- [Task index](https://adz.github.io/Reified/guides/task-index.html)
- [Choosing an API](https://adz.github.io/Reified/guides/choosing-an-api.html)
- [Getting started](https://adz.github.io/Reified/getting-started/)
- [Schema and JSON](https://adz.github.io/Reified/schema/)
- [Error taxonomy](https://adz.github.io/Reified/guides/error-taxonomy.html)
- [Glossary](https://adz.github.io/Reified/guides/glossary.html)
- [API reference](https://adz.github.io/Reified/api.html)

## Axial integration

[Axial](https://github.com/adz/Axial) describes asynchronous workflows with explicit failures and dependencies. Reified began as a library inside Axial and was forked out to stand alone; neither core depends on the other.
