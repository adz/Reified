---
weight: 50
title: Schema Integration
description: Apply refinements, conversions, and domain admission at structured boundaries.
targetFramework: net8.0
---

# Schema Integration

Schema describes structured input: fields, wire representations, path-aware failures, accumulation, and reconstruction.
A refinement supplies the value-level step that turns an already-decoded underlying value into an
invariant-carrying domain value. Schema can apply that refinement at a field boundary and report its failures at the
field's path.

`Reified.Refinements` has no Schema dependency. Domain types can define refinements without choosing a wire format; an
application that uses `Reified.Schema` decides where those refinements participate in structured decoding and encoding.

The [Schema quickstart](/schema/quickstart.html) introduces fields,
record construction, and path-aware diagnostics. Then read [Refined Values in Schema](/schema/refined-values.html) for canonical field schemas, raw-value constraints, explicit refinement, and
schema-local restrictions. This page is the shorter API-oriented view of that integration.

## Refine a primitive schema

```fsharp
open Reified.Refinements
open Reified

let nameSchema : Schema<NonBlankString> =
    Schema.text
    |> Schema.refine NonBlankString.refinement
```


Parsing checks the underlying `string`, constructs `NonBlankString`, and reports failures at the schema path. Encoding
and checking project through `NonBlankString.Value`.

A numeric range is a constraint rather than a refined type, so it goes on the primitive:

```fsharp no-check reason="A field block is only valid inside an enclosing schema<T> { } declaration; not a standalone compilation unit."
field _.Quantity { constrain (Constraint.greaterThan 0) }
```


`Schema.constrain` is available for a standalone value schema too, but inside a field block
the schema is inferred from the field's type and each constraint sits on its own line.

For an application type:

```fsharp isolated
open Reified
open Reified.Refinements

type ContactEmail = private ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value
    let refinement = Refinement.define Constraint.email ContactEmail value

let emailSchema : Schema<ContactEmail> =
    Schema.text
    |> Schema.refine ContactEmail.refinement
```


A field block receives the refinement explicitly:

```fsharp isolated
open Reified
open Reified.SchemaDSL
open Reified.Refinements

type ContactEmail = private ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value
    let refinement = Refinement.define Constraint.email ContactEmail value

type Signup = { Email: ContactEmail; Age: int }

module Signup =
    let create email age = { Email = email; Age = age }

let signupSchema =
    schema<Signup> {
        field _.Email {
            withSchema Schema.text
            refine ContactEmail.refinement
        }

        field _.Age
        construct Signup.create
    }
```


## Choose the operation by meaning

- `Schema.refine refinement schema` constructs an invariant-carrying destination and retains refinement metadata.
- `Schema.convert forward backward schema` performs a total projected mapping.
- `Schema.tryConvert forward backward schema` performs a fallible projected mapping returning `SchemaError list`.
- `Schema.admit create project draftSchema` constructs a domain model from a structured draft while preserving fields.

```fsharp
let centsSchema : Schema<decimal> =
    Schema.int
    |> Schema.convert decimal int
```


A draft stays public and freely constructible; only the aggregate it admits into enforces the invariant, see
[Construction Guarantees](/schema/trusted-construction.html) for the full rationale. `DateTimeOffset`, not
`DateOnly`, for the same reason [Build A Private Aggregate](/schema/patterns/private-aggregates.html) gives: a
canonical schema for every target. The draft's own schema is declared before the private `Booking` below it
reuses the same field names, declaring it after would make `_.Start` resolve against `Booking` instead:

```fsharp isolated
open Reified
open Reified.SchemaDSL
open System

type BookingDraft = { Start: DateTimeOffset; End: DateTimeOffset }

let bookingDraftSchema : Schema<BookingDraft> =
    schema<BookingDraft> {
        field _.Start
        field _.End
        construct (fun start finish -> { Start = start; End = finish })
    }

type Booking =
    private
        { Start: DateTimeOffset
          End: DateTimeOffset }

module Booking =
    let create (draft: BookingDraft) : Result<Booking, SchemaError list> =
        if draft.Start <= draft.End then Ok { Start = draft.Start; End = draft.End }
        else Error [ SchemaError.Custom("end-before-start", Some "End must not be before start.") ]

    let toDraft (booking: Booking) : BookingDraft =
        { Start = booking.Start; End = booking.End }

let bookingSchema : Schema<Booking> =
    bookingDraftSchema
    |> Schema.admit Booking.create Booking.toDraft
```


For the complete progression from a raw field schema to a canonical refined field, continue to
[Refined Values in Schema](/schema/refined-values.html).
