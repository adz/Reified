---
title: For AI agents
description: High-signal Reified.Constraint guidance for coding agents.
weight: 100
targetFramework: net8.0
---

# For AI agents

- `Reified.Constraint` owns `Constraint<'value>`, `Violation`, and the `ConstraintDescription` read model.
- A constraint is a reusable description of acceptable typed values; `Constraint.check` runs it.
- `Constraint.satisfies` returns `bool`; `Constraint.check` returns a structured violation; `Constraint.guard` retains the
  unchanged input after success.
- Constraints never parse, normalize, or replace values.
- Interpreted constructors carry inspectable `ConstraintAtom` data used by execution, export, and violations.
- `custom`, `customWith`, `notWith`, and `contramap` are opaque escape hatches. They run normally but cannot claim
  portable enforcement in exported contracts.
- Keep `Violation` structured through application logic. Render it through a `Renderer` at the presentation edge.
- Reuse the same constraint with direct checking, `Refinement.define`, and `Schema.constrain`.
- `Reified.Constraint` does not depend on `Reified.Result`; it returns the standard F# `Result`.

```fsharp
open Reified

let quantity : Constraint<int> =
    Constraint.between 1 999

let admitQuantity value =
    Constraint.guard quantity value
```


Read [Parsing](/parsing/index.html) for serialized primitive decoding, [Refined](/refined/index.html) for
invariant-carrying types, and [Schema](/schema/index.html) for structured input with field paths.

For compact prompt context, load [`/constraints/llms.txt`](/constraints/llms.txt).
