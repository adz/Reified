---
title: Getting Started
linkTitle: Getting started
description: Declare one model, parse untrusted input into it, and derive a JSON codec from the same declaration.
weight: 1
type: docs
targetFramework: net8.0
menu:
  main:
    weight: 1
---

Reified gives F# applications one model for trusted values and structured boundaries on .NET and Fable. Declare an
invariant once; checking, diagnostics, codecs, contract documents, and test data read the same declaration.

A compiled JSON codec runs under NativeAOT and trimming without runtime reflection. In the recorded benchmark,
it is competitive with `System.Text.Json` on serialization and faster on decode because it avoids reflection and
boxing on each call. A schema parses input and provides field-aware diagnostics, JSON Schema export, and
localizable messages. Refined types prove an invariant at construction, so later code need not re-check it.
`Parse` and `Result` handle typed conversion and composition; `Data` is a source-neutral structured value.

The example below is compiled and executed on every CI run from
[`examples/Reified.GettingStarted`](https://github.com/adz/Reified/blob/main/examples/Reified.GettingStarted/Program.fs);
the outputs below are that program's real output.

## How the packages fit

<div class="package-map">

```text
Constraint ─┐
Parse ──────┤
Data ───────┼──> Schema ───> contract tooling
Refinements ┘
```

</div>

Use Schema when structured input must become a trusted model. Constraint, Parse, Data, and Refinements can each
be used alone without Schema, and work together. "Contract tooling" is the
generated derivation, versioning, and MSBuild pipeline built on Schema, covered from
[Derived Schemas](/schema/derivation/index.html) onward, not a separate package.

Install the complete runtime set for this walkthrough:

```bash
dotnet add package Reified
```


The model is an ordinary record with no Reified-specific members:

```fsharp
type Signup =
    { Email: string
      Age: int
      Newsletter: bool }
```


Declare how untrusted input becomes one:

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

let signupSchema =
    schema<Signup> {
        field _.Email { constraints [ present; email ] }
        field _.Age { constrain (atLeast 13) }
        field _.Newsletter
        construct (fun email age newsletter ->
            { Email = email; Age = age; Newsletter = newsletter })
    }
```


`Data` is a source-neutral input tree, so the same schema reads a form post, CLI arguments, JSON, or
configuration:

```fsharp
let input =
    Data.ofNameValues
        [ "email", "ada@example.org"
          "age", "36"
          "newsletter", "true" ]

Schema.parse signupSchema input
// Ok { Email = "ada@example.org"; Age = 36; Newsletter = true }
```


`"36"` arrived as text and landed as an `int`. No `Signup` exists unless every field and the constructor
succeeded, so downstream code can rely on validation having run.

For a malformed address, an age below the limit, and a missing field:

```fsharp
let invalidInput =
    Data.ofNameValues
        [ "email", "ada"
          "age", "11" ]

match Schema.parse signupSchema invalidInput with
| Ok signup -> printfn "%A" signup
| Error errors ->
    for issue in SchemaErrors.toList errors do
        printfn "%s: %s" (SchemaPath.format issue.Path) (SchemaError.render issue.Error)
```


```text
age: Expected a value at least 13, but was 11.
email: Expected an email address, but was ada.
newsletter: This value was omitted.
```


Every independent field is checked, so one parse reports every problem rather than the first. The paths come
from the declaration's structure; application code need not repeat field names alongside checks.
Each sentence is rendered from the rule that failed.

The same `signupSchema` also produces a JSON codec:

```fsharp
open Reified

let codec = Json.compile signupSchema     // compile once, typically at startup

Json.serialize codec { Email = "ada@example.org"; Age = 36; Newsletter = true }
// {"email":"ada@example.org","age":36,"newsletter":true}
```


There is no second description of the wire shape to keep in step, and no runtime reflection: the codec is
compiled from the schema's typed field plan, so it works under NativeAOT, trimming, and Fable.

## The problem it solves

One rule, "an age is at least 13", usually ends up written four times: in the parser that reads the request,
in the validator that guards the domain, in the form that shows the message, and in the test that builds a
fixture. They start identical and drift. When they drift, the parser accepts what the validator rejects, or the
form shows a message no code enforces.

Reified makes the rule a value. A checker can *execute* it, a renderer can *explain* it, and interpreters can
*export* it to JSON Schema or OpenAPI or *sample* it to generate values.

## One rule on one value

For one value, install `Reified.Constraint`; no schema is needed:

```fsharp
open Reified

let retryCount : Constraint<int> = Constraint.between 0 10

3 |> Constraint.check retryCount
// Ok ()

42
|> Constraint.check retryCount
|> Result.mapError Violation.render
// Error "expected a value between 0 and 10, but was 42"
```


A `Constraint` carries its description, and a `Violation` carries the failed rule and offending value as data.
The message comes from the rule and can be localized or reformatted without changing it.

→ [Constraint](/constraints/constraint.html)

## Attach the rule to a type

A constraint holds wherever you remember to run it. A *refinement* holds for every value of a type, because
the only way to build one is through the check:

```fsharp
open Reified.Refinements

Refine.nonBlankString "Ada"    // Ok (NonBlankString "Ada")
Refine.nonBlankString "  "     // Error ...
```


Downstream code takes `NonBlankString` without re-checking it. Domain types such as `CustomerId`, `Email`, and
`WorkspaceName` can each be defined over a constraint and constructed through it.

→ [Refined values](/refined/index.html)

## Back to the model

Refined types are field types, so the schema from the top of this page absorbs them without extra syntax:

```fsharp
type Registration =
    { Owner: NonBlankString
      Seats: int }

let registrationSchema =
    schema<Registration> {
        field _.Owner
        field _.Seats { constrain (atLeast 1) }
        construct (fun owner seats -> { Owner = owner; Seats = seats })
    }
```


The `Owner` field carries no rules of its own: every refined type has exactly one schema, and the field
resolves it from the type. Use a constraint on the field when the rule belongs to *this boundary*; use a
refined type when it belongs to the domain.

→ [Schema quickstart](/schema/quickstart.html)

## Everything else is derived from that declaration

The schema is a value, so parsing is only one of the things that can read it:

```fsharp
JsonSchema.generate signupSchema
// {"type":"object",
//  "properties":{"email":{"type":"string", …},
//                "age":{"type":"integer","minimum":13},
//                "newsletter":{"type":"boolean"}},
//  "required":["email","age","newsletter"]}
```


`"minimum": 13` comes from the declaration's `atLeast 13`. The same declaration supports checking an existing
value, accepting payloads from older versions, describing the model to a form, and generating test data that obeys
the rules.

→ [What a schema gives you](/getting-started/what-a-schema-gives-you.html)

## Failures are ordinary F# values

Failures use the standard F# `Result` type rather than exceptions or a framework result type. `Schema.parse`
returns
`Result<'model, SchemaErrors>`; a value check returns `Result<'value, Violation>`. Both errors are data you can
match on, group by path, translate, or serialize into a problem-details response.
[`Reified.Result`](/result-handling/index.html) adds the composition, `result { }` for fail-fast sequencing, accumulating
builders for collecting every error at once, over the standard `Result` type rather than replacing it.

## The words this documentation uses

The documentation distinguishes three operations:

| Word | Operation | Example |
| --- | --- | --- |
| **parse** | Change one representation into another. Text becomes a typed value. | `Parse.int "42"` |
| | The same move over a whole tree of untrusted input, accumulating every failure with its path. | `input \|> Schema.parse signupSchema` |
| **check** | Test a value you already hold against a rule. The value does not change. | `Constraint.check (atLeast 13) age` |
| **refine** | Give a checked value a type that carries the rule from then on, so nothing downstream re-checks it. | `ContactEmail.create raw` |

"Validation" is the everyday word for all of this, and the documentation uses it that way when talking about the
area as a whole. Use the three terms above when the distinction matters.

## Continue from here

For structured boundary models, continue with Schema.

→ [Continue with Schema](/schema/index.html)

→ [More on where to go next](/getting-started/where-to-go-next.html)

The focused packages are independently installable when you need only one capability. See
[Packages and platforms](/notes/packages-and-platforms.html) for the complete inventory and platform support.
