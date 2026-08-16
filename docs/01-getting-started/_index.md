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

Reified is a set of small F# libraries for .NET and Fable. You declare an invariant once — on a value, on a
field, on a whole model — and the checking, the diagnostics, the codecs, the contract documents, and the test
data are all read from that one declaration.

This page walks one complete transaction end to end, then widens out to the pieces it used. Everything on it
is compiled and executed on every CI run from
[`examples/Reified.GettingStarted`](https://github.com/adz/Reified/blob/main/examples/Reified.GettingStarted/Program.fs);
the outputs below are that program's real output.

```bash
dotnet add package Reified.Schema
dotnet add package Reified.Schema.Json
```

Start with an ordinary record. Nothing about it is Reified-specific:

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

Feed it something realistic. `Data` is a source-neutral input tree, so the same schema reads a form post, a
query string, JSON, or configuration:

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
succeeded, so downstream code does not have to wonder whether validation ran.

Now feed it something a real user would send — a malformed address, an age below the limit, a missing field:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let input =
    Data.ofNameValues
        [ "email", "ada"
          "age", "11" ]

match Schema.parse signupSchema input with
| Ok signup -> register signup
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
from the structure of the declaration — application code never repeats field names alongside the checks.
Nobody wrote those three sentences: each one is rendered from the rule that failed.

And now the declaration pays for itself. The same `signupSchema`, read by a different interpreter, is a JSON
codec:

```fsharp
open Reified.Schema.Json

let codec = Json.compile signupSchema     // compile once, typically at startup

Json.serialize codec { Email = "ada@example.org"; Age = 36; Newsletter = true }
// {"email":"ada@example.org","age":36,"newsletter":true}
```

There is no second description of the wire shape to keep in step, and no runtime reflection: the codec is
compiled from the schema's typed field plan, so it works under NativeAOT, trimming, and Fable.

That is the whole idea. The rest of this page is the same idea at smaller and larger scales.

## The problem it solves

One rule — "an age is at least 13" — usually ends up written four times: in the parser that reads the request,
in the validator that guards the domain, in the form that shows the message, and in the test that builds a
fixture. They start identical and drift. When they drift, the parser accepts what the validator rejects, or the
form shows a message no code enforces.

Reified's answer is to make the rule a value. A rule you can inspect can be *executed* by a checker,
*explained* by a renderer, *exported* to JSON Schema or OpenAPI, and *sampled* by a generator — from one
declaration.

## One rule on one value

The smallest version needs no schema at all. Install `Reified.Constraint`:

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

Nobody wrote that failure sentence separately. A `Constraint` carries its own description, and a `Violation`
carries the rule that failed and the offending value as data — so the message cannot fall out of step with the
check, and it can be localized or reformatted without touching the rule.

→ [Constraint](/validating-values/constraint.html)

## Attach the rule to a type

A constraint holds wherever you remember to run it. A *refinement* holds for every value of a type, because
the only way to build one is through the check:

```fsharp
open Reified.Refinements

Refine.nonBlankString "Ada"    // Ok (NonBlankString "Ada")
Refine.nonBlankString "  "     // Error ...
```

Downstream code takes `NonBlankString` and stops re-checking. Your own domain types work the same way —
`CustomerId`, `Email`, `WorkspaceName` — each defined over a constraint and constructed through it.

→ [Refined values](/domain-types/index.html)

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

→ [Modelling with Schema](/modelling/quickstart.html)

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

`"minimum": 13` was not written twice — it is the `atLeast 13` from the declaration, read for a different
purpose. The same declaration does four more jobs: checking a value you already hold, accepting payloads from
older versions, describing the model to a form, and generating test data that obeys the rules.

→ [What a schema gives you](/getting-started/what-a-schema-gives-you.html)

## Failures are ordinary F# values

There is no exception model and no framework result type. `Schema.parse` returns
`Result<'model, SchemaErrors>`; a value check returns `Result<'value, Violation>`. Both errors are data you can
match on, group by path, translate, or serialize into a problem-details response.
[`Reified.Result`](/validating-values/result/index.html) adds the composition — `result { }` for fail-fast sequencing, accumulating
builders for collecting every error at once — over the standard `Result` type rather than replacing it.

## The words this documentation uses

Three operations sound similar and are not. The rest of the documentation uses these words precisely:

| Word | Operation | Example |
| --- | --- | --- |
| **parse** | Change one representation into another. Text becomes a typed value. | `Parse.int "42"` |
| | The same move over a whole tree of untrusted input, accumulating every failure with its path. | `input \|> Schema.parse signupSchema` |
| **check** | Test a value you already hold against a rule. The value does not change. | `Constraint.check (atLeast 13) age` |
| **refine** | Give a checked value a type that carries the rule from then on, so nothing downstream re-checks it. | `ContactEmail.create raw` |

"Validation" is the everyday word for all of this, and the documentation uses it that way when talking about the
area as a whole. When the distinction matters, it will be one of the three above instead.

## Where to go next

The library is three groups: rules that live in values, boundaries that produce models, and failures and
fixtures as ordinary values. Each page below states what it is for, so you can start from the job you have.

→ [Where to go next](/getting-started/where-to-go-next.html)

## Installing

```sh
dotnet add package Reified
```

That is the whole set: value rules, refined types, parsing, `Result` composition, data, and Schema with its JSON
codecs. On .NET 8 it also brings the HTTP contract package; a `netstandard2.1` consumer gets the rest. Every package is also independently installable if you want one capability on its own — see
[Packages and platforms](/notes/packages-and-platforms.html) for the list, what each one gives you, and which run on
Fable as well as .NET.

Reified is pre-1.0 and has not been published to NuGet yet, so that line does not resolve for now.
