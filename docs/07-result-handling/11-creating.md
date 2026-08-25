---
weight: 10
title: Creating a Result
description: Turn predicates, booleans, TryParse tuples, options, and Choice values into a Result carrying your own error type.
targetFramework: net8.0
---

# Creating a Result

Most failures start as something that is not a `Result`: a predicate over a value, a `bool`, a `TryParse` tuple, an
option. These helpers do the conversion and let you attach your own error type.

All examples share one error type:

```fsharp
open System
open Reified.Result

type SignupError =
    | NameMissing
    | AgeMissing
    | AgeNotANumber of string
    | AgeOutOfRange of int
```


## Directly

`ok` and `error` construct a `Result`. They exist so a pipeline can end in one without a type annotation; `Ok` and
`Error` work just as well.

```fsharp
Result.ok 42        // Ok 42
Result.error NameMissing  // Error NameMissing
```


## From a predicate over a value

`okIf` keeps the value when the predicate holds; `failIf` is its inverse. Both fail with `unit`, because at that point
there is nothing to say about the failure yet — you attach the reason with `orError`.

```fsharp
"Ada" |> Result.okIf (String.IsNullOrWhiteSpace >> not)
// Ok "Ada"

"" |> Result.okIf (String.IsNullOrWhiteSpace >> not)
// Error ()

"" |> Result.okIf (String.IsNullOrWhiteSpace >> not) |> Result.orError NameMissing
// Error NameMissing

"  " |> Result.failIf String.IsNullOrWhiteSpace |> Result.orError NameMissing
// Error NameMissing
```


Splitting the predicate from the reason keeps the predicate reusable. The same `String.IsNullOrWhiteSpace` serves
every field, and each call site names its own error.

## From a standalone bool

When the condition is already computed and there is no subject value to carry forward, use `require`. It succeeds
with `unit`.

```fsharp
true  |> Result.require |> Result.orError NameMissing   // Ok ()
false |> Result.require |> Result.orError NameMissing   // Error NameMissing
```


The difference from `okIf` is behavioural: `okIf` applies a predicate to a subject and preserves it on success;
`require` takes an already-computed condition and has no subject to preserve.

## From TryParse and Choice

.NET `TryParse` methods return a `bool * 'value` tuple. `fromTry` converts one directly.

```fsharp
Int32.TryParse "36"  |> Result.fromTry     // Ok 36
Int32.TryParse "abc" |> Result.fromTry     // Error ()

Int32.TryParse "abc" |> Result.fromTry |> Result.orError (AgeNotANumber "abc")
// Error (AgeNotANumber "abc")
```


`fromChoice` converts an F# `Choice`, which some older APIs return.

```fsharp
Choice1Of2 42 |> Result.fromChoice           // Ok 42
Choice2Of2 NameMissing |> Result.fromChoice  // Error NameMissing
```


## From an option or value option

`fromOption` and `fromValueOption` are the inverse of `toOption`/`toValueOption`. They fail with `unit`, same as
`okIf`/`failIf`, so attach the reason with `orError`.

```fsharp
Some "Ada" |> Result.fromOption |> Result.orError NameMissing   // Ok "Ada"
None       |> Result.fromOption |> Result.orError NameMissing   // Error NameMissing

ValueSome 36 |> Result.fromValueOption |> Result.orError AgeMissing  // Ok 36
```


## Checking without losing the value: reach for Constraint

A reusable rule that proves a fact about a value belongs in `Reified.Constraint`, not in `Result`. `Constraint.guard`
runs a constraint and hands the original value back on success, so the check can sit in the middle of a pipeline.

```fsharp
open Reified

let positive : Constraint<int> = Constraint.greaterThan 0

36 |> Constraint.guard positive |> Result.mapError (fun _ -> AgeOutOfRange 36)   // Ok 36
-1 |> Constraint.guard positive |> Result.mapError (fun _ -> AgeOutOfRange -1)   // Error (AgeOutOfRange -1)
```


Note the success type: `Constraint.guard` returns `Ok 36`, not `Ok ()`. If the rule is only a local condition, not
something worth naming and reusing, use `okIf`/`failIf` with `orError` instead — see
[Constraint vs. Result](/constraints/overview.html) for the distinction.

## Which one to reach for

| Starting point | Function |
| --- | --- |
| a value and a local predicate | `okIf` / `failIf`, then `orError` |
| a `bool` with no subject value | `require`, then `orError` |
| `bool * 'value` from `TryParse` | `fromTry` |
| `Choice<'value, 'error>` | `fromChoice` |
| `'value option` / `'value voption` | `fromOption` / `fromValueOption`, then `orError` |
| a reusable, inspectable rule | `Constraint.guard`, then `orError` / `mapError` |

Next: [transforming values](/result-handling/transforming.html) once you have a `Result`.
