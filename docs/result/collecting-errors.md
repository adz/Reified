---
weight: 80
title: Collecting every error
description: Report all independent failures at once with result.list and and!.
---

# Collecting every error

`result { }` stops at the first failure. When the steps do not depend on each other — the fields of a form, the
columns of a row — the caller usually wants every problem at once, not the first one repeatedly.

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

For any other container, map at the end — `Result.mapError Set.ofList`, `Result.mapError NonEmptyList.ofList`, and so
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

The third case is the one to understand. Name and age both succeeded, so the block reached the `let!`, which failed on
its own — one error. And in the second case the `let!` never ran at all, so `AgeOutOfRange` could not have appeared
alongside the other two even if the age had been out of range.

The rule: **everything you want reported together must be in the same `and!` group.** A failure in an earlier group
hides every later one.

## Composing blocks

A binding that already carries the collected type passes through without being wrapped again, so the output of one
accumulating block can feed another:

```fsharp
let already: Result<int, SignupError list> = Error [ NameMissing ]

result.list {
    let! first = already
    and! second = parseAge "abc"
    return first + second
}
// Error [NameMissing; AgeNotANumber "abc"]
```

The errors are flattened into one list rather than nested.

## What it does not do

The collected errors are a flat container. They carry no field names, no paths, and no indication of which binding
produced which error — only the order they were bound in.

If the independent bindings are fields of structured input — a form post, a request body, a configuration file — that
association is `Schema`'s job, and it is the default next step. Declare the model once and `Schema.parse` accumulates
every field failure with the path that produced it, keeps the raw value for redisplay, renders messages in the
caller's language, and hands back your domain type:

```fsharp
let signupSchema =
    schema<Signup> {
        field _.Name { constrain Constraint.present }
        field _.Age { constrain (Constraint.atLeast 18) }
        construct (fun name age -> { Name = name; Age = age })
    }

Data.ofNameValues form |> Schema.parse signupSchema
// Error carries name -> present, age -> atLeast 18
```

See [the Schema quickstart]({{% relref "/schema/quickstart" %}}).

Staying at the Result layer is still reasonable when the failures are not really fields — independent checks in
application code, or a small internal function whose caller just wants a list. In that case carry the association
yourself, by mapping each step's error to a pair of field name and reason before the block collects them.

The line between the two: `result.list { ... and! ... }` accumulates one group of independent bindings.
`Schema.parse` owns accumulation at a structured boundary, with paths.

For the third case — the same operation applied across a collection — use
[`Result.traverseAll` and `Result.sequenceAll`](../collections/#traverseall-and-sequenceall), which run every mapping
and return `Result<'output list, 'error list>`.
