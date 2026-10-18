---
weight: 40
title: Extracting values
description: Get back to a plain value, an option, or a default.
targetFramework: net8.0
---

# Extracting values

At the edge of a pipeline, extract a plain value when the caller does not handle `Result`.

```fsharp
open Reified.Result

type SignupError = AgeMissing
```


## A default

`defaultValue` returns the success value, or the fallback you supply.

```fsharp
Ok 36 |> Result.defaultValue 0                                   // 36
(Error AgeMissing: Result<int, SignupError>) |> Result.defaultValue 0   // 0
```


## An option

`toOption` and `toValueOption` drop the error entirely.

```fsharp
Ok 36 |> Result.toOption                                          // Some 36
(Error AgeMissing: Result<int, SignupError>) |> Result.toOption    // None
(Error AgeMissing: Result<int, SignupError>) |> Result.toValueOption // ValueNone
```


These discard why the operation failed. Use them when the caller does not need the reason: once the error is gone, it cannot be reported, logged, or returned to a user.

## Pattern matching

Pattern match when the final branch handles both sides:

```fsharp
open System

let parseAge (raw: string) =
    Int32.TryParse raw
    |> Result.fromTry
    |> Result.orError AgeMissing

match parseAge "abc" with
| Ok age -> printfn "age is %d" age
| Error failure -> printfn "rejected: %A" failure
```


A `Result` from this package is the standard F# type, so every existing technique applies, matching, `function`
shorthand, active patterns.

## Keeping the error

If you need the value *and* the reason it might be missing, do not extract at all. Keep the `Result` until the last
possible point, and let the caller decide. Extracting early is what forces the error back into a `null`, a sentinel,
or an exception.
