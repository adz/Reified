---
title: Constraints
linkTitle: Constraints
type: docs
notoc: true
description: Define reusable value rules, check typed values, and render structured violations.
weight: 3
targetFramework: net8.0
---

# Constraints

`Reified.Constraint` describes which typed values are acceptable. A constraint can be checked directly, reused by a
refined type, attached to a Schema field, inspected for documentation, and rendered in another language.

```sh
dotnet add package Reified.Constraint
```


```fsharp
open Reified
open Reified.ConstraintDSL

let username = Constraint.all [ present; minLength 3; maxLength 30 ]

Constraint.check username "ada"
// Ok ()

Constraint.check username ""
// Error [ Blank; InvalidLength (MinimumLength 3, Some 0) ]
```


Constraints return the standard F# `Result<unit, Violation>`. They do not depend on `Reified.Result`; use that
package, FsToolkit.ErrorHandling, or ordinary pattern matching when you need to compose the result with other work.

## Start here

- [Quickstart](/constraints/quickstart.html) — define and check a constraint.
- [Constraint](/constraints/constraint.html) — the core type and its guarantees.
- [ConstraintDSL](/constraints/dsl.html) — the authoring vocabulary.
- [Reusable constraints](/constraints/constraints.html) — interpreted rules and opaque escape hatches.
- [Working with violations](/constraints/violations.html) — inspect and render failures.
- [Localization](/constraints/localization/index.html) — contextual messages and language catalogues.
- [Tutorial: constraints and Result](/constraints/tutorials/constraint-result.html) — map violations into application errors.
- [Fable support](/constraints/fable.html) — use the same rules on JavaScript.

Use [Parsing](/parsing/index.html) when serialized text must first become a typed value. Use
[Refined](/refined/index.html) when successful admission should be recorded in the type. Use
[Schema](/schema/index.html) when fields of a structured input need paths and accumulated diagnostics.
