---
weight: 25
title: Inspecting Schema Metadata Tutorial
description: Walk the same schema as data for docs and JSON Schema.
targetFramework: net8.0
---

# Inspecting Schema Metadata Tutorial

A schema is data, not a validator. This tutorial reads a schema's fields, shapes, formats, and constraints without
parsing any input or constructing any model — the foundation for JSON Schema emitters, documentation generators, and
UI metadata.

## Describe A Schema

`Inspect.model` turns a built schema into a plain metadata tree:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
open Reified

let description = Inspect.model signupSchema

description.Fields |> List.map _.Name    // ["email"; "age"]
description.Fields |> List.map _.Order   // [0; 1]
```


Nothing executes: no getters run, no constructors are called, no checks fire. The description is immutable data.

## Read Field Shapes And Constraints

Each field carries a `SchemaDescription` — shape, declared format, and constraint metadata:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let email = description.Fields |> List.find (fun field -> field.Name = "email")

email.Schema.Format          // Some SchemaFormat.email (when declared with Schema.withFormat)

match email.Schema.Shape with
| SchemaShape.Primitive kind -> printfn "primitive %A" kind
| SchemaShape.Refined underlying -> printfn "refined over %A" underlying.Shape
| SchemaShape.Nested model -> printfn "nested with %d fields" model.Fields.Length
| SchemaShape.Many item -> printfn "collection of %A" item.Shape
| SchemaShape.Union union -> printfn "union with cases %A" (union.Cases |> List.map _.Tag)
```


Refined values expose their raw representation through `SchemaShape.Refined`, so a boundary interpreter can render an
`Email` field as a constrained string without knowing the domain type.

## Understand The Constraint Types

There is one constraint type. `Schema.constrain` takes the same `Reified.Constraint.Constraint<'value>` you would check
directly, and inspection hands back the same `ConstraintDescription` that `Constraint.inspect` returns. Schema erases
the value type when it stores a constraint in its heterogeneous field plan, but that erasure is internal: nothing in
the public inspection surface is Schema-specific.

Boundary supply is separate, because it is decided before a typed value exists. `Schema.mustSupply` and
`Schema.mayOmit` record it, and inspection exposes it as `Supply` beside the constraints rather than mixed in among
them.

## Lower Constraints To Another Format

A description is a small recursive tree — atoms, `All`, `Any`, `Optional`, and `Opaque` — and every interpreted atom
reuses the same expectation types the violations use, so lowering is one traversal:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let jsonKeyword (atom: ConstraintAtom) =
    match atom with
    | CardinalityAtom (Cardinality.Maximum maximum) -> Some $"\"maxLength\":{maximum}"
    | FormatAtom (Pattern pattern) -> Some $"\"pattern\":\"{pattern}\""
    | _ -> None

let keywords (description: SchemaDescription) =
    description.Constraints
    |> List.collect ConstraintDescription.atoms
    |> List.choose jsonKeyword
```


`ConstraintDescription.atoms` deliberately stops at an opacity boundary, and it is only safe where dropping a rule is
sound. An interpreter that *claims enforcement* must consult the whole expression instead: dropping a conjunct weakens
an `All`, and dropping a disjunct strengthens an `Any` — which would reject values the library accepts.

Atoms are shape-neutral. `Cardinality.Maximum 5` becomes `maxLength`, `maxItems`, or `maxProperties` depending on the
`SchemaShape` it is attached to, so combine the two rather than reading the description alone.

The repository keeps three worked prototypes — a JSON Schema emitter, a docs describer, and a UI metadata producer —
in `tests/Reified.Schema.Tests/SchemaInterpreterPrototypeTests.fs`, all built only on `Inspect`.

## Why This Matters

One declaration now serves parsing, validation, rules, and every metadata consumer. When a constraint changes, the
form control, the JSON Schema, the docs table, and the parser all change together.

## Next

- [Refined Value Schemas](/schema/refined-values.html) for how refinement layers stay inspectable.
- [Schema reference](/api.html) for the full `Inspect` API.
