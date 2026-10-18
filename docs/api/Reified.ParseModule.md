# `Parse`

`Reified.Parse` converts serialized text into primitive typed values. Unlike `Int32.TryParse`, it returns a value
identifying which of three failures occurred: `MissingValue`, `InvalidFormat`, or `OutOfRange`. Every parser takes
`string` and returns `Result<'value, ParseError>`.

| Parser | Returns | Accepts |
| --- | --- | --- |
| `Parse.int` | `int` | Optionally signed digits, invariant culture. |
| `Parse.long` | `int64` | As `int`, with the wider range. |
| `Parse.decimal` | `decimal` | Digits with an optional sign, decimal point, and thousands separators, invariant culture. |
| `Parse.float` | `float` | Digits with an optional leading sign, decimal point, thousands separators, and exponent notation, invariant culture. |
| `Parse.bool` | `bool` | `"true"` or `"false"`, any casing. Not `"1"`, `"yes"`, or `"on"`. |
| `Parse.guid` | `System.Guid` | Any format `Guid.TryParse` accepts, including braced and hyphenless. |
| `Parse.dateTime` | `System.DateTime` | Invariant-culture date and time text, such as `"2026-03-04T09:30:00"`. |
| `Parse.dateTimeOffset` | `System.DateTimeOffset` | As `dateTime`, with an offset such as `"+10:00"`. |
| `Parse.dateOnly` | `System.DateOnly` | Invariant-culture date text. .NET 8+ only. |
| `Parse.timeOnly` | `System.TimeOnly` | Invariant-culture time text. .NET 8+ only. |
| `Parse.enum<'enum>` | `'enum` | A case name, case-insensitive, or its numeric text. |

Numeric parsers use invariant culture: the same text parses the same way on every machine, and the decimal point is
`.`, not `,`. They also ignore leading and trailing whitespace; whitespace-only input is still `MissingValue`.

Only `Parse.int`, `Parse.long`, and `Parse.decimal` can produce `OutOfRange`; every other parser reports `MissingValue`
or `InvalidFormat`. `Parse.float` never reports `OutOfRange`: input beyond `float`'s range parses as `infinity` or
`-infinity`, and input too close to zero parses as `0.0`.

See [Parsing](/parsing/index.html) for the failure cases and the optional/defaulting helpers.
