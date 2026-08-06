# Reified

Declare value and model invariants once. Derive validation, parsing, diagnostics, codecs, contracts, and test data from the same declarations.

[![ci](https://github.com/adz/Reified/actions/workflows/ci.yml/badge.svg)](https://github.com/adz/Reified/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

> [!WARNING]
> Reified is pre-1.0. Package and namespace renaming from `Axial.*` to `Reified.*` is in progress before the first release.

## Declare the rule once

Most validation stacks keep the rule and its message in separate places. Reified makes a constraint inspectable data, so checking, diagnostics, export, and generation read the same declaration.

```fsharp
open Reified.Constraint

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
open Reified.Constraint
open Reified.Schema
open Reified.Schema.Syntax

type Signup =
    { Email: string
      Age: int }

let signupSchema =
    schema<Signup> {
        field "email" _.Email {
            constraints [ Constraint.present; Constraint.email ]
        }
        field "age" _.Age {
            constrain (Constraint.atLeast 13)
        }
        construct (fun email age -> { Email = email; Age = age })
    }

match Schema.parse signupSchema input with
| Ok signup -> register signup
| Error errors -> display errors
```

The same `signupSchema` can drive a compiled JSON codec, JSON Schema, form metadata, HTTP contracts, versioned migrations, and matching test data.

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
- `Reified.Schema.Http` — host-neutral endpoint contracts, problem details, and OpenAPI
- `Reified.Schema.Contracts.Build` — MSBuild integration for derived record and wire contracts

The contract compiler and schema-derived testing adapter are repository tooling, not runtime packages. The source tree retains the `Axial.*` names until the post-extraction rename commit.

## Install

Reified packages have not been published yet.

## Documentation and examples

- [Constraint](docs/values/constraint/_index.md)
- [Getting started with values](docs/values/getting-started.md)
- [Refined domain values](docs/values/refined/domain-values.md)
- [Getting started with Schema](docs/schema/getting-started.md)
- [JSON codecs](docs/schema/json-codec.md)
- [HTTP contracts](docs/schema/http-servers.md)
- [Versioned contracts](docs/schema/contracts.md)
- [Runnable Schema examples](docs/schema/examples.md)

## Axial integration

[Axial](https://github.com/adz/Axial) describes asynchronous workflows with explicit failures and dependencies. Its optional server adapters execute Reified HTTP contracts; neither core depends on the other.

Declare a contract with Reified. Serve it with Axial.
