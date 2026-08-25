---
weight: 90
title: FsToolkit.ErrorHandling
description: How Reified Result, Constraint, Refined, and Schema relate to FsToolkit.ErrorHandling.
targetFramework: net8.0
---

# FsToolkit.ErrorHandling

FsToolkit.ErrorHandling provides a broad set of combinators and computation expressions for `Result`, asynchronous
results, validation, and related standard F# types.

Reified separates four roles, each installable on its own:

- `Reified.Result` supplies a smaller `Result` surface, `result { }`, and the accumulating builders that
  collect every error rather than stopping at the first.
- `Reified.Constraint` describes which values are acceptable, and derives the failure from that description.
- `Reified.Refinements` constructs values whose types record successful checks.
- `Reified.Schema` declares structured boundaries and accumulates path-aware failures.

None of these depends on `Reified.Result`; every one of them returns the standard F# `Result`, so they compose
with `Reified.Result`, with FsToolkit.ErrorHandling, or with your own helpers.

Existing FsToolkit Result helpers can remain in an application. Both libraries use the standard F# `Result` type.

| FsToolkit pattern | Reified equivalent |
| --- | --- |
| `Result.requireTrue` | `Result.require`, then `Result.orError` |
| `Result.requireSome` | `Result.fromOption`, then `Result.orError` |
| `result { }` | `result { }` |
| `asyncResult { }`, `taskResult { }` | no equivalent; Reified does not model effects |
| `List.traverseResultA`, `List.sequenceResultA` | `Result.traverseAll`, `Result.sequenceAll` |
| accumulating validation over boundary fields | a record `schema<'model> { }` interpreted by `Schema.parse` or `Schema.check` |

`Result.requireTrue` and `Result.requireSome` are two-argument functions in FsToolkit: the error comes first, the
value second. Reified splits that in two, so the error is always attached last with `orError`:

```fsharp
open Reified.Result

type SignupError =
    | TermsNotAccepted
    | NameMissing

let acceptedTerms = true
let submittedName = Some "Ada"

// Reified
acceptedTerms |> Result.require |> Result.orError TermsNotAccepted
submittedName |> Result.fromOption |> Result.orError NameMissing
```


The FsToolkit equivalents read as `acceptedTerms |> Result.requireTrue TermsNotAccepted` and
`submittedName |> Result.requireSome NameMissing`.

With `open Reified.ResultDSL`, the `Result.` prefix on `require` and `orError` drops — `fromOption` stays qualified,
since `ResultDSL` exports only the CE and the local admission functions (`okIf`, `failIf`, `require`, `orError`,
`mapError`), not the whole `Result` module:

```fsharp
open Reified.ResultDSL

acceptedTerms |> require |> orError TermsNotAccepted
submittedName |> Result.fromOption |> orError NameMissing
```


Schema adds one property that Result combinators do not provide: the declaration is inspectable. The same field and
constraint metadata can parse input, return complete paths, emit JSON Schema/OpenAPI, compile a JSON codec, and drive
forms or documentation.
