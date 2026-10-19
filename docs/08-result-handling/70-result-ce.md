---
weight: 70
title: The result computation expression
description: Write dependent fallible steps as straight-line code with result { }.
targetFramework: net8.0
---

# The result computation expression

`result { }` passes each success to the next step and
returns the first `Error`:

```fsharp
open System
open Reified.Result
open Reified.ResultDSL

type SignupError =
    | NameMissing
    | AgeNotANumber of string
    | AgeOutOfRange of int

type Signup = { Name: string; Age: int }

let parseName (name: string) : Result<string, SignupError> =
    if String.IsNullOrEmpty name then Error NameMissing else Ok name

let parseAge (age: string) : Result<int, SignupError> =
    match Int32.TryParse age with
    | true, value -> Ok value
    | false, _ -> Error(AgeNotANumber age)
```


## What the keywords do

`let!` binds the value inside `Ok` to the name on its left. `do!` runs a step whose success value is `unit`, so there
is no name. `return` wraps a plain value back up as `Ok`. `return!` uses a complete `Result` as the block's result.

```fsharp
result {
    let! (name: string) = (parseName "Ada": Result<string, SignupError>)
    let! (age: int) = (parseAge "36": Result<int, SignupError>)
    return { Name = name; Age = age }
}
// Result<Signup, SignupError>
```


On the right of `let!` is a `Result<'value, 'error>`; on the left is the `'value`. The block's own type is
`Result<'whatever you return, 'error>`, the error type is shared by every step, which is why they all fail with
`SignupError` here.

`do!` and `return!` in the same block:

```fsharp
let requireAdult age : Result<unit, SignupError> =
    if age >= 18 then Ok () else Error (AgeOutOfRange age)

let signupAdultOnly name age =
    result {
        let! name = parseName name
        let! age = parseAge age
        do! requireAdult age          // unit success, nothing to bind
        return! Ok { Name = name; Age = age }
    }

signupAdultOnly "Ada" "36"   // Ok { Name = "Ada"; Age = 36 }
signupAdultOnly "Ada" "12"   // Error (AgeOutOfRange 12)
```


`do!` runs a step whose success value is `unit`, so there is no name to bind; `return!` uses a complete `Result` as the
block's result.

## Failure stops the block

```fsharp
let signup name age =
    result {
        let! name = parseName name
        let! age = parseAge age
        return { Name = name; Age = age }
    }

signup "Ada" "36"    // Ok { Name = "Ada"; Age = 36 }
signup "" "36"       // Error NameMissing
signup "Ada" "abc"   // Error (AgeNotANumber "abc")
```


`signup "" "36"` returns `Error NameMissing`; the age is never parsed because the block stops at the first failure, so
its own failure is never seen. A later binding does not run after an earlier one fails.

## Control flow inside a block

The builder supports the ordinary constructs, so a block is not restricted to a straight run of bindings. The
reader and validation calls below are placeholders for the caller's own host I/O; `result { }` only sequences them
and never opens a file or performs I/O itself:

```fsharp no-check reason="Placeholders for the caller's own I/O."
result {
    use reader = openReader path          // disposed on the way out, success or failure
    let! header = readHeader reader

    for line in lines do                  // stops at the first failing iteration
        do! validate line

    while not (isDone ()) do
        do! step ()

    return header
}
```


`try/with` and `try/finally` work as usual. Note what they do and do not catch: they handle .NET **exceptions**, not
`Error` values. An `Error` is an ordinary return value, so it does not trigger `with`, it just ends the block.

## When to use it

- a later step needs a value bound by an earlier one;
- continuing after a failure makes no sense;
- the pipeline has grown past two or three steps and the nesting from `bind` is getting hard to read.

When the steps are independent of each other and the caller should hear about all the failures rather than the first,
use [collecting every error](/result-handling/collecting-errors.html) instead.
