---
weight: 10
title: Quickstart
description: Compose operations that can fail into one application error type, using the standard F# Result.
targetFramework: net8.0
---

# Quickstart

`Reified.Result` is a standalone leaf. Nothing else in Reified is required, and it depends on no other Reified package:

```bash
dotnet add package Reified.Result
```


```fsharp
open Reified.Result
open Reified.ResultDSL
```


The package works with the standard F# `Result<'value, 'error>` — it does not wrap or replace it. What it supplies is
the vocabulary around it:

| Concern | API | Returns |
| --- | --- | --- |
| Turn an option, nullable, or `TryParse` tuple into a Result | `Result.orError`, `Result.fromTry` | `Result<'value,'error>` |
| Compose dependent failures | `result { }` | `Result<'value,'error>` |
| Collect independent failures | `result.list { }` with `and!` | `Result<'value,'error list>` |
| Apply a fallible operation across a sequence | `Result.traverse`, `Result.sequence` | `Result<'value list,'error>` |
| Observe without changing the value | `Result.tap`, `Result.tapError` | `Result<'value,'error>` |

## Compose dependent steps

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
type QuantityError =
    | InvalidInteger of string
    | NotPositive of int

let quantity raw =
    result {
        let! parsed =
            Int32.TryParse raw
            |> Result.fromTry
            |> Result.orError (InvalidInteger raw)

        let! positive =
            if parsed > 0 then Ok parsed else Error (NotPositive parsed)

        return positive
    }
```


Each step's own failure is mapped into one deliberate application error type at the bind site.

## Collect independent failures

`let!` fails fast; `and!` accumulates. The boundary is the compiler's, not a setting:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
result.list {
    let! name = parseName input.Name
    and! age  = parseAge input.Age
    return name, age
}
// Error [ NameMissing; AgeNotANumber "x" ]
```


Everything inside one `and!` group is accumulated into a list. A later `let!` binds sequentially and fails fast, because
it can depend on the earlier results. See [Collecting every error](/result-handling/collecting-errors.html) for the exact boundary.

## Where the other packages fit

This accumulation is **flat**: a list of your error values with no field identity and no path. Admitting values in the
first place belongs to [Constraints](/constraints/index.html), while path-aware accumulated diagnostics over a
whole structured input belong to [Reified.Schema](/schema/index.html).

All of them return the standard F# `Result`, so these helpers work on their output — but none of them requires this
package.

## Continue

- [Creating a Result](/result-handling/creating.html)
- [Transforming values](/result-handling/transforming.html)
- [Handling errors](/result-handling/handling-errors.html)
- [The result computation expression](/result-handling/result-ce.html)
- [Collecting every error](/result-handling/collecting-errors.html)
- [Comparison with FsToolkit.ErrorHandling](/comparisons/fstoolkit-comparison.html)
