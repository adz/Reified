---
weight: 10
title: Quickstart
description: Define a reusable constraint, check a typed value, and keep violations structured until rendering.
targetFramework: net8.0
---

# Quickstart

Install the constraint package:

```bash
dotnet add package Reified.Constraint
```


```fsharp
open Reified

let name : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.maxLength 80 ]
```


`Constraint.check` confirms whether a typed value is acceptable:

```fsharp
Constraint.check name "Ada"
// Ok ()

Constraint.check name ""
// Error [ Blank ]
```


Use `Constraint.guard` when the successful result should retain the unchanged input:

```fsharp
let checkedName : Result<string, Violation> =
    Constraint.guard name "Ada"
```


Constraints never parse or normalize values. [Parsing](/parsing/index.html) changes serialized text into a typed
value. [Refined](/refined/index.html) records successful admission in a destination type.

Keep each `Violation` structured inside application errors. Render it only at the UI, log, or other presentation edge:

```fsharp
Constraint.check name ""
|> Result.mapError Violation.render
// Error "value must be present"
```


Continue with [Constraint](/constraints/constraint.html) for the core model, [Reusable constraints](/constraints/constraints.html)
for interpreted and opaque rules, and [Working with violations](/constraints/violations.html) for inspection and rendering.
