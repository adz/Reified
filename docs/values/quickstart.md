---
weight: 10
title: Quickstart
description: Check typed values, decode serialized primitives, and construct invariant-carrying domain types.
---

# Quickstart

Install only the packages an application needs — Values is a grouping, not a package:

```bash
dotnet add package Reified.Constraint    # reusable, inspectable value rules
dotnet add package Reified.Refinements   # invariant-carrying domain values
dotnet add package Reified.Parse         # serialized primitive decoding
```

```fsharp
open Reified
open Reified.Refinements
```

Each package stays focused:

| Concern | API | Returns |
| --- | --- | --- |
| Test one typed value | `Constraint<'value>` | `Result<unit, Violation>` |
| Keep a checked input | `Constraint.guard` | `Result<'value, Violation>` |
| Decode serialized primitives | `Parse.int`, `Parse.guid`, and other parsers | `Result<'value, ParseError>` |
| Construct an invariant-carrying type | `Refinement.create`, `Refine.*` | `Result<'refined, Violation>` |

## Constrain a typed value

```fsharp
let nameCheck : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.maxLength 80 ]

let checkedName : Result<string, Violation> =
    "Ada" |> Constraint.guard nameCheck
```

A check returns `unit`; `Constraint.guard` returns the unchanged input after success. Constraints never replace or
normalize a value.

Render a failure only when it becomes user-facing text:

```fsharp
""
|> Constraint.check nameCheck
|> Result.mapError Violation.render
// Error "value must be present"
```

## Decode serialized input

```fsharp
let parsed : Result<int, ParseError> = Parse.int "42"
```

## Construct an invariant-carrying value

```fsharp
let name : Result<NonBlankString, Violation> =
    Refine.nonBlankString "Ada"
```

The successful check is now part of the type, so nothing downstream re-checks it.

## A complete boundary function

These packages return the standard F# `Result`, so any Result vocabulary composes them. This example uses
[`Reified.Result`]({{% relref "/result/" %}}), but FsToolkit.ErrorHandling or your own helpers work identically:

```fsharp
open Reified.Result
open Reified.ResultDSL

type QuantityError =
    | InvalidInteger of ParseError
    | InvalidQuantity of Violation

let quantity raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidInteger
        let! quantity = parsed |> Constraint.guard (Constraint.greaterThan 0) |> Result.mapError InvalidQuantity
        return quantity
    }
```

`Parse.int` changes representation. `Constraint.greaterThan` admits only positive integers. Mapping both errors at the
bind sites gives the application one deliberate error type.

Keep the `Violation` structured inside `QuantityError`; the UI, log, or HTTP boundary decides when and how to render
it. See [Working with violations](./constraint/violations/) for grouped failures, inspection, and localization.

## Where Schema fits

These packages perform explicit operations over individual values. The caller decides which input each failure belongs
to and how to represent the application's error type.

[Reified.Schema]({{% relref "/schema/" %}}) is the structured-boundary layer. A `Schema<'model>` declares fields and
constructors, applies constraints and refinements at those fields, and returns accumulated `SchemaError` values with
input paths. The same declaration can also drive JSON codecs, JSON Schema, forms, contracts, and inspection.

Use these packages directly for local functions, domain constructors, and workflows. Use Schema when an entire form,
request, configuration document, or other structured input must become a model with field-aware diagnostics. The
approaches compose: Schema uses constraints and refinements defined independently here. Start with
[the Schema quickstart]({{% relref "/schema/quickstart/" %}}) when that is your boundary.

## Continue

- [Constraint](./constraint/)
- [Working with violations](./constraint/violations/)
- [Refined](./refined/)
- [Define Refined Types](./refined/domain-values/)
- [Parse](./parse/)
- [Tutorial: constraints and Result](./tutorials/constraint-result/)
