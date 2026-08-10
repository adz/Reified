---
weight: 30
title: Parse
type: docs
notoc: true
description: Decode serialized strings into primitive F# values, without losing why a conversion failed.
---

# Parse

`Reified.Parse` changes serialized text into primitive typed values. `"42"` becomes `42`, `"true"` becomes `true`. The
point of the package is not that it converts — `Int32.TryParse` converts — but that when conversion fails it says which
of three different things went wrong, in a value you can act on.

```sh
dotnet add package Reified.Parse
```

```fsharp
open Reified

Parse.int "12"     // Ok 12
Parse.int ""       // Error (MissingValue "int")
Parse.int "twelve" // Error (InvalidFormat ("int", "twelve"))
Parse.int "99999999999" // Error (OutOfRange ("int", "99999999999"))
```

`ParseError` is an independent leaf type. It carries no prose and no culture, so it stays comparable data you can map
into your own error case, assert on in a test, or pass across a boundary.

## The three cases

```fsharp
type ParseError =
    | MissingValue of target: string
    | InvalidFormat of target: string * input: string
    | OutOfRange of target: string * input: string
```

| Case | What it means | Example |
| --- | --- | --- |
| `MissingValue target` | The input was absent, empty, or only whitespace. There was nothing to convert. | `Parse.int "   "` → `MissingValue "int"` |
| `InvalidFormat (target, input)` | Text was supplied but does not spell a value of that type. | `Parse.bool "yes"` → `InvalidFormat ("bool", "yes")` |
| `OutOfRange (target, input)` | Text spells a well-formed number the destination type cannot hold. | `Parse.int "99999999999"` → `OutOfRange ("int", "99999999999")` |

`target` names the destination type — `"int"`, `"bool"`, `"decimal"`, `"Guid"` — so a caller can tell which conversion
failed without tracking it separately. `input` is the offending text, retained for redisplay.

The distinction between the three matters more than it looks. An empty field and a misspelled one usually deserve
different messages, and only `OutOfRange` tells a user that their number was understood but too big.

## The parsers

| Parser | Returns | Accepts |
| --- | --- | --- |
| `Parse.int` | `int` | Optionally signed digits, invariant culture. |
| `Parse.long` | `int64` | As `int`, with the wider range. |
| `Parse.decimal` | `decimal` | Digits with an optional sign, decimal point, and thousands separators, invariant culture. |
| `Parse.float` | `float` | As `decimal`, plus exponent notation. |
| `Parse.bool` | `bool` | `"true"` or `"false"`, any casing. Not `"1"`, `"yes"`, or `"on"`. |
| `Parse.guid` | `System.Guid` | Any format `Guid.TryParse` accepts, including braced and hyphenless. |
| `Parse.dateTime` | `System.DateTime` | Invariant-culture date and time text, such as `"2026-03-04T09:30:00"`. |
| `Parse.dateTimeOffset` | `System.DateTimeOffset` | As `dateTime`, with an offset such as `"+10:00"`. |
| `Parse.dateOnly` | `System.DateOnly` | Invariant-culture date text. .NET 8+ only. |
| `Parse.timeOnly` | `System.TimeOnly` | Invariant-culture time text. .NET 8+ only. |
| `Parse.enum<'enum>` | `'enum` | A case name, case-insensitive, or its numeric text. |

Every parser takes `string` and returns `Result<'value, ParseError>`. Every numeric parser uses invariant culture, so
the same text parses the same way on every machine — a decimal point is always `.`, never `,`.

Only the numeric parsers can produce `OutOfRange`; the others report `MissingValue` or `InvalidFormat`.

## The three failures in practice

```fsharp
// Missing: nothing was supplied.
Parse.decimal ""
// Error (MissingValue "decimal")

// Malformed: something was supplied, but it is not a decimal.
Parse.decimal "19,95 AUD"
// Error (InvalidFormat ("decimal", "19,95 AUD"))

// Out of range: well-formed, but too large for the type.
Parse.int "2147483648"
// Error (OutOfRange ("int", "2147483648"))
```

Match on the case when the three need different treatment:

```fsharp
let describe error =
    match error with
    | MissingValue target -> $"Please supply a {target}."
    | InvalidFormat (_, input) -> $"'{input}' is not in the expected format."
    | OutOfRange (_, input) -> $"'{input}' is too large."
```

## Hand off to your own error type

Most call sites do not keep `ParseError`. Map it into the application's vocabulary at the point of use, and the
signature stops mentioning Reified at all:

```fsharp
type PortError = PortNotANumber of string

let port raw : Result<int, PortError> =
    Parse.int raw |> Result.mapError (fun _ -> PortNotANumber raw)
```

`Result.orError` from `Reified.Result` is shorter when the error case carries nothing:

```fsharp
Parse.int raw |> Result.orError PortMissing
```

Keep the `ParseError` instead — `Result.mapError InvalidInteger` into a case that carries it — when something later
needs to tell the three failures apart or redisplay the offending text.

When the text is one field of structured input rather than a standalone value, do not do this by hand.
[`Schema`]({{% relref "/schema/quickstart" %}}) parses each field, accumulates every failure, and reports the
field path alongside it.

## Optional input

`Parse.optional` distinguishes an absent value from a bad one. Absence succeeds as `None`; malformed present text still
fails.

```fsharp
Parse.optional Parse.int None
// Ok None

Parse.optional Parse.int (Some "42")
// Ok (Some 42)

Parse.optional Parse.int (Some "bad")
// Error (InvalidFormat ("int", "bad"))
```

`Parse.optionalOr` supplies a fallback only when the input is absent:

```fsharp
Parse.optionalOr 80 Parse.int None
// Ok 80

Parse.optionalOr 80 Parse.int (Some "443")
// Ok 443

Parse.optionalOr 80 Parse.int (Some "bad")
// Error (InvalidFormat ("int", "bad"))
```

The fallback never recovers from malformed input. A default is for an omitted setting, not a wrong one.

## Combined optional helpers

Named helpers pair the common primitive parsers with `optional`:

```fsharp
Parse.intOption (Some "42")        // Ok (Some 42)
Parse.boolOption None              // Ok None
Parse.decimalOption (Some "12.5")  // Ok (Some 12.5M)
Parse.guidOption (Some "89d45a4b-f634-4db0-9a41-7e8461957be1")
// Ok (Some 89d45a4b-f634-4db0-9a41-7e8461957be1)
```

The defaulting helpers pair them with `optionalOr`:

```fsharp
Parse.intOrDefault 80 None                // Ok 80
Parse.boolOrDefault false (Some "true")   // Ok true
Parse.decimalOrDefault 5.5M (Some "bad")  // Error (InvalidFormat ("decimal", "bad"))
```

Use `*Option` when absence should stay `None`, and `*OrDefault` when absence should become a concrete value. Both
preserve the error from malformed present text.

## Parse, then refine

Parsing changes representation. Checking a typed value is a constraint's job, and admitting it into a domain type is a
refinement's. They stay separate steps, and each contributes its own error:

```fsharp
open Reified
open Reified.Refinements
open Reified.Result
open Reified.ResultDSL

type QuantityError =
    | InvalidInteger of ParseError
    | InvalidQuantity of Violation

let quantity raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidInteger
        let! quantity = parsed |> Constraint.guard (Constraint.greaterThan 0) |> Result.mapError InvalidQuantity
        return quantity
    }
```

See [Refined values]({{% relref "/values/refined/" %}}) for the refinement model, and the
[Parse API reference]({{% relref "/values/reference/parse/" %}}) for every parser.
