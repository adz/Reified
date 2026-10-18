---
title: Test Schema Guarantees
weight: 50
description: Derive accepted values from a schema and test transitions, codecs, and mappings against the same rules.
targetFramework: net8.0
---

# Test schema guarantees

> `Reified.Schema.Testing` is repository tooling, not a published package. There is nothing to install. Copy or adapt
> the pattern shown here into your own test project.

The compiler checks type shape and visibility. It cannot prove that a constructor contains the right business rule or
that a total transition preserves it.

A **schema guarantee** here means the behavior established by a successful `Schema.parse` or `Schema.check` call. It is
not a permanent property of a public record that callers can construct elsewhere.

A **transition** is a function that changes one accepted domain value into another. A total transition returns the new
value directly and therefore claims it cannot break the type's rules.

Use generated examples to check those claims near the owning module.

## Generate accepted models

Reified's tests use the non-packable `Reified.Schema.Testing` FsCheck adapter. Applications can adapt this pattern
locally. `SchemaGen.model` generates structured data, parses it through the schema, discards samples the schema
rejects (constructor failures included), and runs `Schema.check` on each surviving model before yielding it. The
outer `Result` reports generation-support failures such as `UnsupportedConstraint`; a model the generator yields has
already passed both parse and check.

```fsharp no-check reason="FsCheck is a test-only dependency of Reified.Schema.Testing; it's not among the assemblies this audit pipeline resolves, so this block can't be checked here even though the pattern is real."
open System
open Reified
open Reified.SchemaDSL
open Reified.Schema.Testing
open FsCheck.FSharp

type Booking =
    private
        { Start: DateTimeOffset
          End: DateTimeOffset }

[<RequireQualifiedAccess>]
module Booking =
    let start booking = booking.Start
    let finish booking = booking.End

    let create start finish =
        if start <= finish then Ok { Start = start; End = finish }
        else Error "End must not be before start."

    let shift days booking =
        { Start = (start booking).AddDays days
          End = (finish booking).AddDays days }

    let schema : Schema<Booking> =
        schema<Booking> {
            fieldAs "start" start
            fieldAs "end" finish
            constructResult create
        }

let bookingGenerator =
    SchemaGen.model Booking.schema
    |> Result.defaultWith (failwithf "%A")

let bookings = Gen.sample 100 bookingGenerator
```


Keep the adapter in test code, not as a runtime dependency. This `Booking` uses a simplified two-field
constructor rather than the draft-plus-typed-error split [Build A Private Aggregate](/schema/patterns/private-aggregates.html)
and [Model Legal Transitions](/schema/patterns/legal-transitions.html) use for the same shape, since schema
generation, not construction, is this page's subject.

## Check total transitions

A transition returning `Booking` claims it always preserves the aggregate invariant. `shift` keeps `start <= finish`
because it adds the same number of days to both endpoints, but it is only total while both results stay inside the
`DateTimeOffset` range: `AddDays` throws `ArgumentOutOfRangeException` at the limits. The generated samples on this
page stay far from those limits, so the example below does not throw. A production transition should bound its inputs
or return a result rather than rely on generated values landing in a safe range.

```fsharp no-check reason="Continues the Booking/bookings illustration above; not a standalone compilation unit."
let shiftedBookings =
    bookings
    |> Array.map (Booking.shift 10.0)

let allRemainValid =
    shiftedBookings
    |> Array.forall (Schema.check Booking.schema >> Result.isOk)
```


Use an FsCheck property in a real test suite so it explores different values and shift amounts.

## Check codecs and mappings

For generated domain values, test that serialization and decoding preserve the value expected by the application.

For wire-to-domain mappings, generate wire values and assert that accepted values produce a domain value that passes its
own constructor or schema.

Do not derive all test input from the schema. Schema-derived generation is good at accepted values. Keep separate broad
or hand-picked inputs for malformed data, constraint edges, and constructor failures.

## Supply custom generators when needed

Patterns, custom constraints, and application-specific distributions cannot always be reversed automatically.
`SchemaGen.rawWith` accepts generators keyed by field path.

```fsharp no-check reason="bookingDeriveSchema is the caller's own DeriveSchema-generated wire schema; not one this fragment can construct standalone."
let overrides =
    Map.ofList [ "reference", Gen.constant (Data.Text "BK-42") ]

let rawGenerator =
    SchemaGen.rawWith overrides bookingDeriveSchema
    |> Result.defaultWith (failwithf "%A")
```


The adapter reports `Error (SchemaGenerationError.UnsupportedConstraint (path, rule))` instead of guessing a generator
that might disagree with the schema.
