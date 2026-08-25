---
weight: 7
title: Result handling
linkTitle: Result handling
type: docs
notoc: true
description: Compose operations that can fail, using the standard F# Result type.
menu:
  main:
    weight: 1
targetFramework: net8.0
---

# Result handling

`Reified.Result` works with the standard F# `Result<'value, 'error>` - ordinary `Ok` or `Error` values that any other F# code can pattern match.

What it provides is a module and DSL for working with them - turning ordinary values into a `Result`, chaining steps, replacing one error with another, getting values back out, and two
computation expressions for writing sequences of fallible steps as straight-line code.

```sh
dotnet add package Reified.Result
```


## A first example

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
open System
open Reified.Result
open Reified.ResultDSL

type SignupError =
    | NameMissing
    | AgeNotANumber of string
    | AgeOutOfRange of int

let parseName raw =
    raw
    |> Result.failIf String.IsNullOrWhiteSpace
    |> Result.orError NameMissing

let parseAge raw =
    Int32.TryParse raw
    |> Result.fromTry
    |> Result.orError (AgeNotANumber raw)
    |> Result.bind (fun age ->
        if age >= 0 && age < 130 then Ok age else Error (AgeOutOfRange age))

result {
    let! name = parseName "Ada"
    let! age = parseAge "36"
    return {| Name = name; Age = age |}
}
```


```text
Ok { Name = "Ada"; Age = 36 }
```


Every page below builds on this same `parseName`/`parseAge` pair, so the examples compose with each other.

## Pages

- [Creating a Result](/result-handling/creating.html) - turn options, nullables, `TryParse` tuples, booleans, and predicates into a
  `Result` with your own error type.
- [Transforming values](/result-handling/transforming.html) - `map` and `bind`, and how a chain of fallible steps compose.
- [Handling errors](/result-handling/handling-errors.html) - change the error type, replace one, and recover.
- [Extracting values](/result-handling/extracting.html) - get back to a plain value, an option, or a default.
- [Working with collections](/result-handling/collections.html) — apply a fallible operation across a sequence with `traverse` and
  `sequence`, or collect every failure with `traverseAll` and `sequenceAll`.
- [Observing a Result](/result-handling/observing.html) - log or measure mid-pipeline with `tap` and `tapError`.
- [The result computation expression](/result-handling/result-ce.html) - write dependent steps as straight-line code with `result { }`.
- [Collecting every error](/result-handling/collecting-errors.html) - report all independent failures at once with `result.list { }`
  and `and!`.
- [Comparison with FsToolkit.ErrorHandling](/comparisons/fstoolkit-comparison.html) - what each library is for, and how they
  interoperate.
- [API reference](/api.html) - every function, generated from the source.

## Related

`Reified.Result` composes failures. Admitting values in the first place is the
[Constraints](/constraints/index.html) test typed values, [Parsing](/parsing/index.html) decodes serialized primitives,
and [Refined](/refined/index.html) constructs values whose types record a successful check. All three return the
standard F# `Result`, so these helpers work on their output — but none requires this package, and this package does
not require them.

Accumulation here is **flat**: `result.list { }` collects a list of your error values with no field identity.
When a whole form, request, or document must become a model with path-aware accumulated diagnostics, that is
[Reified.Schema](/schema/index.html).
