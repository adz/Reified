---
title: "Values: admit a value, or say why not"
linkTitle: Values
type: docs
notoc: true
description: Reusable value constraints, invariant-carrying domain types, and named parsers for serialized primitives.
weight: 4
menu:
  main:
    weight: 2
targetFramework: net8.0
---

# Values

**Values is a grouping, not a package.** There is no `Reified.Values` on NuGet and no `Reified.Values` namespace. It is
the name for three packages that answer the same question — *may this value into my program?* — each installed on
its own:

| Package | Answers | Returns |
| --- | --- | --- |
| [`Reified.Constraint`](/validating-values/constraint.html) | Is this typed value acceptable? | `Result<unit, Violation>` |
| [`Reified.Refinements`](/domain-types/index.html) | Can I build a type that records the answer? | `Result<'refined, Violation>` |
| [`Reified.Parse`](/parsing-input/index.html) | What does this serialized text decode to? | `Result<'value, ParseError>` |

```sh
dotnet add package Reified.Constraint   # reusable, inspectable value rules
dotnet add package Reified.Refinements      # invariant-carrying domain values
dotnet add package Reified.Parse        # serialized primitive decoding
```

`Reified.Refinements` depends on `Reified.Constraint`. `Reified.Parse` depends on neither. None of them depends on
`Reified.Result`: every one returns the standard F# `Result`, so they compose with
[`Reified.Result`](/validating-values/result/index.html), FsToolkit.ErrorHandling, or your own helpers — your choice, not the
library's.

## The three in one function

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
open Reified
open Reified.Refinements

type Quantity =
    private Quantity of int

    static member Refinement =
        Refinement.define (Constraint.between 1 999)

let quantity (raw: string) =
    raw
    |> Parse.int                                   // Result<int, ParseError>
    |> Result.mapError InvalidInteger
    |> Result.bind (Refine.value<Quantity> >> Result.mapError InvalidQuantity)
```

`Parse` changes representation. `Constraint` describes what is acceptable. `Refined` makes the successful check
part of the type, so nothing downstream re-checks it.

## Learn and solve tasks

### Constraint

- [Overview](/validating-values/constraint.html) — one reusable description of valid values.
- [Reusable constraints](/validating-values/constraints.html)
- [ConstraintDSL](/validating-values/dsl.html)
- [Working with violations](/validating-values/violations.html)
- [Contextual localization](/validating-values/localization/index.html) and [adding a language](/validating-values/adding-a-language.html)
- [Under Fable](/validating-values/fable.html)

### Refined

- [Overview](/domain-types/index.html) — types that record a successful check.
- [Define refined types](/domain-types/domain-values.html)
- [Built-in refined values](/domain-types/catalog.html)
- [Composition](/domain-types/composition.html)
- [Refined fields in a schema](/modelling/index.html)

### Parse

- [Overview](/parsing-input/index.html) — named parsers for serialized primitive values.

### Across all three

- [Quickstart](/validating-values/quickstart.html)
- [Tutorial: constraints and Result](/validating-values/tutorials/constraint-result.html)
- [Introductory reference app](/validating-values/reference-app.html)
- API reference: [`Reified.Constraint`](/api.html),
  [`Reified.Refinements`](/api.html),
  [`Reified.Parse`](/api.html)

## Related

[`Reified.Result`](/validating-values/result/index.html) composes the failures these packages return into one application error
type. [`Reified.Schema`](/modelling/index.html) applies the same constraints and refinements at declared fields
of a structured input, and returns accumulated diagnostics carrying the path of each failure.
