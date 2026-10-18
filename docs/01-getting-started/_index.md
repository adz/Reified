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
configuration. Each source adapts into `Data` before `Schema.parse` — the name-value pairs below use
`Data.ofNameValues`; JSON text becomes `Data` with `Json.parseData` (portable, .NET and Fable) or
`Data.ofJsonDocument` on .NET 8+:

```fsharp
let input =
    Data.ofNameValues
        [ "email", "ada@example.org"
          "age", "36"
          "newsletter", "true" ]

Schema.parse signupSchema input
// Ok { Email = "ada@example.org"; Age = 36; Newsletter = true }
```

→ [Input sources](/schema/input-sources.html)


`"36"` arrived as text and landed as an `int`. `Schema.parse` returns a `Signup` only when every field and the
constructor succeeded; a failed parse returns `SchemaErrors`, never a `Signup`. `Signup` is an ordinary public
record, so callers can still construct one directly — the parse result is the value that validation has admitted.

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


Every independent field is checked even when others fail, so one parse reports every field problem rather than
the first. `SchemaErrors.toList` returns the failures in deterministic path order, and nested collection items
carry index paths such as `lines[0].amount`. Each sentence is rendered from the rule that failed. Cross-field
invariants live in the constructor, which runs only after every field passed; a constructor failure is reported as
a single diagnostic rather than a per-field problem.

The same `signupSchema` also produces a JSON codec:

```fsharp
open Reified

let codec = Json.compile signupSchema     // compile once, typically at startup

Json.serialize codec { Email = "ada@example.org"; Age = 36; Newsletter = true }
// {"email":"ada@example.org","age":36,"newsletter":true}
```


There is no second description of the wire shape to keep in step, and no runtime reflection: the codec is
compiled from the schema's typed field plan, so it works under NativeAOT, trimming, and Fable.

## One rule, several readers

One rule, "an age is at least 13", usually ends up written four times: in the parser that reads the request, in the
validator that guards the domain, in the form that shows the message, and in the test that builds a fixture. They
start identical and drift. Reified makes the rule a value instead: a checker can *execute* it, a renderer can
*explain* it, and interpreters can *export* it or *sample* it. The same value scales from one field up to a whole
model:

- **On one value**, a `Constraint` answers "is this valid?" and derives the failure. →
  [Constraint](/constraints/constraint.html)
- **On a type**, a refinement builds the rule into every construction, so downstream code need not re-check it. →
  [Refined values](/refined/index.html)
- **On a model**, refined types are field types, and boundary-specific rules attach as field constraints. →
  [Schema quickstart](/schema/quickstart.html)
- **From the same declaration**, JSON Schema, a codec, versioned contracts, metadata, and test data are all derived. →
  [What a schema gives you](/getting-started/what-a-schema-gives-you.html)

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
