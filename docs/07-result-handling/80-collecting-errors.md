---
weight: 80
title: Collecting every error
description: Report all independent failures at once with result.list and and!.
targetFramework: net8.0
---

# Collecting every error

`result { }` stops at the first failure. For independent steps such as fields of a form or columns of a row, callers may need every failure at once.

`result.list { }` collects them. Join the independent bindings with `and!` instead of `let!`.

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

let signupAll name age =
    result.list {
        let! name = parseName name
        and! age = parseAge age
        return { Name = name; Age = age }
    }
```


Both bindings run, whatever the other one does:

```fsharp
signupAll "Ada" "36"   // Ok { Name = "Ada"; Age = 36 }
signupAll "" "abc"     // Error [NameMissing; AgeNotANumber "abc"]
signupAll "" "36"      // Error [NameMissing]
```


The error type changed. Each step still fails with a single `SignupError`, but the block collects them, so it returns
`Result<Signup, SignupError list>`. Errors appear in binding order.

## Choosing the container

The builder name picks the container, and it shows up in the block's type:

| Builder | Block type |
| --- | --- |
| `result.list { }` | `Result<'value, 'error list>` |
| `result.array { }` | `Result<'value, 'error[]>` |

```fsharp
result.array {
    let! name = parseName ""
    and! age = parseAge "abc"
    return { Name = name; Age = age }
}
// Error [|NameMissing; AgeNotANumber "abc"|]
```


For any other container, map at the end, `Result.mapError Set.ofList`, `Result.mapError NonEmptyList.ofList`, and so
on.

## `and!` accumulates; `let!` still fails fast

Both keywords work in the same block and they mean different things. Bindings joined by `and!` are independent, so all
of them run and their errors combine. A `let!` that follows depends on what came before, so it cannot run until those
bindings have succeeded.

```fsharp
let mixed name age =
    result.list {
        let! name = parseName name
        and! age = parseAge age                 // independent: both always run

        let! confirmed =                        // dependent: needs age
            if age >= 18 then Ok age else Error (AgeOutOfRange age)

        return { Name = name; Age = confirmed }
    }
```


```fsharp
mixed "Ada" "36"   // Ok { Name = "Ada"; Age = 36 }
mixed "" "abc"     // Error [NameMissing; AgeNotANumber "abc"]
mixed "Ada" "12"   // Error [AgeOutOfRange 12]
```


In the third case, name and age succeeded, then `let!` failed with one error. In the second case, `let!` never ran,
so `AgeOutOfRange` could not appear alongside the other errors.

The rule: **everything you want reported together must be in the same `and!` group.** A failure in an earlier group
hides every later one.

## Composing blocks

A binding whose error type is already the collected container passes through unchanged, and a single-error binding
is lifted to a one-error list before both join. The output of one accumulating block can therefore feed another
without nesting:

```fsharp
let already: Result<int, SignupError list> = Error [ NameMissing ]

result.list {
    let! first = already
    and! second = parseAge "abc"
    return first + second
}
// Error [NameMissing; AgeNotANumber "abc"]
```


`already` arrives as `Result<int, SignupError list>` and is used unchanged; `parseAge "abc"` arrives as
`Result<int, SignupError>` and is lifted to `Error [AgeNotANumber "abc"]`. The two are appended into one flat list
rather than nested.

## What it does not do

The collected errors are a flat container. They carry no field names, no paths, and no indication of which binding
produced which error, only the order they were bound in.

If the independent bindings are fields of structured input, a form post, a request body, a configuration file, that
association is `Schema`'s job, and it is the default next step. Declare the model once and `Schema.parse` accumulates
every field failure with the path that produced it, keeps the raw value for redisplay, renders messages in the
caller's language, and hands back your domain type:

<!-- isolated: fails a confusing SRTP-resolution error when checked in sequence with the blocks above on this
     page, despite an identical standalone repro compiling cleanly outside the docs pipeline. Cause not found;
     re-check whether isolation is still needed if this page's earlier blocks change. -->

```fsharp isolated
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup = { Name: string; Age: int }

let signupSchema =
    schema<Signup> {
        field _.Name { constrain Constraint.present }
        field _.Age { constrain (Constraint.atLeast 18) }
        construct (fun name age -> { Name = name; Age = age })
    }

let form = [ "name", ""; "age", "12" ]

match Data.ofNameValues form |> Schema.parse signupSchema with
| Ok signup -> printfn "%A" signup
| Error errors ->
    for issue in SchemaErrors.toList errors do
        printfn "%s: %s" (SchemaPath.format issue.Path) (SchemaError.render issue.Error)
```


`Data.ofNameValues` keeps each value as text. `Schema.parse` reads each field's serialized form, so `"12"` is parsed
into `12` and then fails `atLeast 18`, while `""` fails `present`:

```text
age: Expected a value at least 18, but was 12.
name: Value must be present.
```


See [the Schema quickstart](/schema/quickstart.html).

Staying at the Result layer is still reasonable when the failures are not really fields, independent checks in
application code, or a small internal function whose caller just wants a list. In that case carry the association
yourself, by mapping each step's error to a pair of field name and reason before the block collects them.

`result.list { ... and! ... }` accumulates one group of independent bindings. `Schema.parse` accumulates
failures at a structured boundary, with paths.

For the third case, the same operation applied across a collection, use
[`Result.traverseAll` and `Result.sequenceAll`](/result-handling/collections.html#traverseall-and-sequenceall), which run every mapping
and return `Result<'output list, 'error list>`.
