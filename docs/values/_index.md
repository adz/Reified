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
---

# Values

**Values is a grouping, not a package.** There is no `Reified.Values` on NuGet and no `Reified.Values` namespace. It is
the name for three packages that answer the same question — *may this value into my program?* — each installed on
its own:

| Package | Answers | Returns |
| --- | --- | --- |
| [`Reified.Constraint`](./constraint/) | Is this typed value acceptable? | `Result<unit, Violation>` |
| [`Reified.Refinements`](./refined/) | Can I build a type that records the answer? | `Result<'refined, Violation>` |
| [`Reified.Parse`](./parse/) | What does this serialized text decode to? | `Result<'value, ParseError>` |

```sh
dotnet add package Reified.Constraint   # reusable, inspectable value rules
dotnet add package Reified.Refinements      # invariant-carrying domain values
dotnet add package Reified.Parse        # serialized primitive decoding
```

`Reified.Refinements` depends on `Reified.Constraint`. `Reified.Parse` depends on neither. None of them depends on
`Reified.Result`: every one returns the standard F# `Result`, so they compose with
[`Reified.Result`]({{% relref "/result/" %}}), FsToolkit.ErrorHandling, or your own helpers — your choice, not the
library's.

## The three in one function

```fsharp
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

- [Overview](./constraint/) — one reusable description of valid values.
- [Reusable constraints](./constraint/constraints/)
- [ConstraintDSL](./constraint/dsl/)
- [Working with violations](./constraint/violations/)
- [Contextual localization](./constraint/localization/) and [adding a language](./constraint/adding-a-language/)
- [Under Fable](./constraint/fable/)

### Refined

- [Overview](./refined/) — types that record a successful check.
- [Define refined types](./refined/domain-values/)
- [Built-in refined values](./refined/catalog/)
- [Composition](./refined/composition/)
- [Refined fields in a schema](./refined/schema/)

### Parse

- [Overview](./parse/) — named parsers for serialized primitive values.

### Across all three

- [Quickstart](./quickstart/)
- [Tutorial: constraints and Result](./tutorials/constraint-result/)
- [Introductory reference app](./reference-app/)
- API reference: [`Reified.Constraint`]({{% relref "/values/reference/constraint/" %}}),
  [`Reified.Refinements`]({{% relref "/values/reference/refined/" %}}),
  [`Reified.Parse`]({{% relref "/values/reference/parse/" %}})

## Related

[`Reified.Result`]({{% relref "/result/" %}}) composes the failures these packages return into one application error
type. [`Reified.Schema`]({{% relref "/schema/" %}}) applies the same constraints and refinements at declared fields
of a structured input, and returns accumulated diagnostics carrying the path of each failure.
