---
title: For AI agents
description: High-signal Constraint, Refined, and Parse guidance for coding agents.
weight: 100
---

# For AI agents

Values is a navigation grouping over three packages that admit values: `Reified.Constraint`, `Reified.Refinements`, and
`Reified.Parse`. There is no `Reified.Values` package and no `Reified.Values` namespace. `Reified.Refinements` depends on
`Reified.Constraint`; `Reified.Parse` depends on neither; none depends on `Reified.Result`.

- Use `Constraint<'value>` to test one typed value without replacing it.
- Use `Constraint<'value>` when executable checking and portable metadata must stay together.
- Use `Constraint.guard` to keep the original value after a successful check.
- Use `Parse.*` only to decode serialized primitive input.
- Use `Refinement<'underlying,'refined>` only for invariant-carrying destination types with a total reverse projection.
- Map `ParseError` and `Violation` into the application's error type at composition boundaries.
- Use `Schema.refine` for a refinement, `Schema.convert` for total mappings, `Schema.tryConvert` for fallible
  mappings, and `Schema.admit` for structured draft-to-domain construction.

```fsharp
open Reified.Constraint
open Reified.Parse
open Reified.Refinements
open Reified.Result

type InputError =
    | InvalidCount of ParseError
    | NonPositiveCount of Violation

let count raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidCount
        let! count = parsed |> Constraint.guard (Constraint.greaterThan 0) |> Result.mapError NonPositiveCount
        return count
    }
```

The `result { }` above is [`Reified.Result`]({{< relref "/result/" >}}) and is optional — these packages return the
standard F# `Result`, so any Result vocabulary composes them.

Start with [Getting Started](./getting-started/), then read [Define Refined Types](./refined/domain-values/).

For compact prompt context, load [`/values/llms.txt`](/values/llms.txt).
