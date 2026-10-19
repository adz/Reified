---
weight: 30
title: Error taxonomy and recipes
linkTitle: Error taxonomy and recipes
type: docs
description: Every failure type Reified produces, how they convert into each other, and a symptom-to-cause table.
targetFramework: net8.0
---

# Error taxonomy and recipes

Reified has eight failure shapes. They are deliberately distinct: a constraint failure is not a parse failure, and a
trusted-codec failure is not an accumulated `SchemaErrors`. Know which one you are holding before mapping it.

## The types

| Type | Package | Means | Carries |
| --- | --- | --- | --- |
| `Violation` | `Reified.Constraint` | A value failed a constraint | `Atomic` / `All` / `Any` tree; interpreted atoms keep their identity |
| `ParseError` | `Reified.Parse` | Serialized text could not become a primitive | `MissingValue`, `InvalidFormat (target, input)`, or `OutOfRange (target, input)` |
| `SchemaError` | `Reified.Schema` | One boundary failure at one path | `Omitted`, `Blank`, `ExpectedScalar`, `ExpectedObject`, `ExpectedMany`, `InvalidFormat`, `ParseOutOfRange`, `UnknownTag`, `Violation`, `ConstructorFailed`, `Custom` |
| `SchemaIssue` | `Reified.Schema` | A `SchemaError` plus its location | `Path: SchemaPath` and `Error: SchemaError` |
| `SchemaErrors` | `Reified.Schema` | Accumulated independent failures, in path order | `SchemaIssue list` (via `SchemaErrors.toList`) |
| `JsonCodecException` | `Reified.Schema` | Trusted codec decode failed on structure or wire shape | Exception with a schema-relative path in `Message` |
| `MigrationError` | `Reified.Schema` | A contract migration failed | `MigrationFailed message` or `RevalidationFailed SchemaErrors` |
| `ContractError` | `Reified.Schema` | A versioned contract could not select, parse, or migrate | `VersionMissing`, `VersionUnrecognized`, `VersionTooNew`, `ParseFailed (version, SchemaErrors)`, `Migration MigrationError` |

`Violation` and `ParseError` are leaf errors; `SchemaError` lowers both of them (`SchemaError.Violation`,
`SchemaError.Blank`/`InvalidFormat`/`ParseOutOfRange`) and adds path structure around them.

## Conversions and rendering

```fsharp
open Reified

SchemaError.ofParseError (ParseError.MissingValue "int")     // SchemaError.Blank
SchemaError.render (SchemaError.InvalidFormat "int")          // "Expected int format."

Constraint.check Constraint.email "ada"
|> Result.mapError Violation.render                            // Error "expected an email address, but was ada"
```

The remaining conversions are signature references; each input is named to show its type:

| Operation | Result |
| --- | --- |
| `SchemaErrors.toList errors` | `SchemaIssue list`, deterministic path order |
| `SchemaErrors.toString errors` | `string list`, one rendered line per failure |
| `Json.deserialize codec badJson` | raises `JsonCodecException` |
| `Json.tryDeserialize codec badJson` | `Result<'model, string>`, first structural failure |
| `Json.parseData badJson` | raises `JsonCodecException` when the text is not one JSON value |
| `Json.tryParseData badJson` | `Result<Data, string>` |
| `RetainedParseResult.renderErrors parsed` | `string list`, one line per failure |

Localized rendering of a `SchemaError.Violation` goes through `Violation.message` with a `Renderer`; the other
`SchemaError` cases render through the neutral English `SchemaError.render`. There is no localized `SchemaError`
renderer, so a boundary that localizes writes one app-owned function:

```fsharp
open Reified

let renderIssue (renderer: Renderer) (issue: SchemaIssue) =
    let message =
        match issue.Error with
        | SchemaError.Violation violation -> Violation.message renderer violation
        | other -> SchemaError.render other

    let path = SchemaPath.format issue.Path
    if path = "" then message else $"{path}: {message}"
```

## Mapping a whole parse into your own union

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup = { Email: string; Age: int }

type SignupError =
    | Field of path: string * message: string

let ofParse (parsed: RetainedParseResult<Signup>) : Result<Signup, SignupError list> =
    match parsed.Result with
    | Ok signup -> Ok signup
    | Error errors ->
        errors
        |> SchemaErrors.toList
        |> List.map (fun issue ->
            Field(SchemaPath.format issue.Path, SchemaError.render issue.Error))
        |> Error
```

## Symptom → cause

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| Constraints are not enforced on JSON decode | You used `Json.deserialize`/`tryDeserialize` on a schema whose rules matter | Route untrusted input through `Json.parseData` + `Schema.parse`; the trusted codec skips constraints |
| `missing required field` from a codec | Wire shape mismatch, not a rule | The payload is missing a field the codec declares required; check the producer |
| A value passed `Schema.parse` but later fails a rule | The public record was constructed directly, outside the boundary | Make the aggregate private, or re-check with `Schema.check` |
| `MissingValue` from `Parse.optional (Some "")` | Blank text is a present-but-missing input, not an absent one | Treat `Some ""` as a parse failure; the fallback in `optionalOr` applies only to `None` |
| Errors are English even though I set up a renderer | Only `Violation` localizes; `SchemaError.render` is neutral English | Use `Violation.message` for violation cases and `SchemaError.render` for the rest, as above |
| A contract fails with `VersionTooNew` | The payload's version is ahead of the contract's current version | Register the newer version as current, or reject the payload |
| A contract fails with `Migration (RevalidationFailed …)` | A migrated value no longer satisfies the current schema | The migration produced a value the current schema rejects; fix the migration or the current rules |
| `matrix` throws `ArgumentException` | More than 256 combinations | Reduce the dimensions; the product is checked before cases are materialized |
| A rendered `Data.Number` is not valid JSON | The number was built with the `Number` union case, not `Data.num` | Use `Data.num` (or a parser) so the token is validated before it is stored |

See [Redisplay and field errors](/schema/redisplay-and-field-errors.html) for path formatting and retained input, and
[Versioned contracts](/schema/versioned-contracts.html) for the migration failure cases.
