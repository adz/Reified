---
weight: 25
title: Inspecting Schema Metadata Tutorial
description: Walk the same schema as data for docs and JSON Schema.
targetFramework: net8.0
---

# Inspecting Schema Metadata Tutorial

`Inspect.model` reads a schema's fields, shapes, formats, and constraints without parsing input or constructing a model.
JSON Schema emitters, documentation generators, and UI tools can use this metadata.

## Describe A Schema

`Inspect.model` turns a built schema into a plain metadata tree:

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup = { Email: string; Age: int }

let signupSchema =
    schema<Signup> {
        field _.Email {
            withSchema (Schema.text |> Schema.withFormat SchemaFormat.email)
        }
        field _.Age
        construct (fun email age -> { Email = email; Age = age })
    }

let description = Inspect.model signupSchema

description.Fields |> List.map _.Name    // ["email"; "age"]
description.Fields |> List.map _.Order   // [0; 1]
```


Nothing executes: no getters run, no constructors are called, no checks fire. The description is immutable data.

## Read Field Shapes And Constraints

Each field carries a `SchemaDescription`, shape, declared format, and constraint metadata:

```fsharp no-check reason="Continues the description/signupSchema example above."
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

A description is a small recursive tree, atoms, `All`, `Any`, `Optional`, and `Opaque`, and every interpreted atom
reuses the same expectation types the violations use, so lowering is one traversal:

```fsharp
open Reified

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


`ConstraintDescription.atoms` deliberately stops at an opacity boundary, and the `keywords` traversal above is only
safe where dropping a rule is sound; it does not claim to enforce the whole expression. An interpreter that *claims
enforcement* must consult the whole expression instead: dropping a conjunct weakens an `All`, and dropping a disjunct
strengthens an `Any`, which would reject values the library accepts. A conservative alternative recurses over
`ConstraintDescription.Expression` and refuses any form it cannot represent rather than flattening across it:

```fsharp no-check reason="Continues the jsonKeyword example above."
let rec expressionKeywords (description: ConstraintDescription) : string list option =
    match description.Expression with
    | ConstraintExpression.Atom atom -> jsonKeyword atom |> Option.map List.singleton
    | ConstraintExpression.All children ->
        children
        |> List.map expressionKeywords
        |> List.fold
            (fun acc next ->
                match acc, next with
                | Some keywords, Some more -> Some(keywords @ more)
                | _ -> None)
            (Some [])
    | ConstraintExpression.Optional inner -> expressionKeywords inner
    // A disjunction or an opaque node cannot be flattened into one keyword list without
    // changing what it accepts, so refuse and keep the rule as runtime metadata instead.
    | ConstraintExpression.Any _
    | ConstraintExpression.Opaque _ -> None
```

This shape recurses over the logical form: a conjunction keeps every branch, an optional delegates to its inner
constraint, and a disjunction or opaque node returns `None` so the caller retains it as runtime metadata rather than
publishing a keyword that enforces the wrong rule.

Atoms are shape-neutral. `Cardinality.Maximum 5` becomes `maxLength`, `maxItems`, or `maxProperties` depending on the
`SchemaShape` it is attached to, so combine the two rather than reading the description alone.

The repository keeps three worked prototypes, a JSON Schema emitter, a docs describer, and a UI metadata producer, 
in `tests/Reified.Schema.Tests/SchemaInterpreterPrototypeTests.fs`, all built only on `Inspect`.

## Next

- [Refined Value Schemas](/schema/refined-values.html) for how refinement layers stay inspectable.
- [Schema reference](/api/Reified.Inspect.html) for the full `Inspect` API.
