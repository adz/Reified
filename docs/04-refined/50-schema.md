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

If Schema is new to you, start with [the Schema quickstart](/schema/quickstart.html) for fields,
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

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
field _.Quantity { constrain (Constraint.greaterThan 0) }
```


`Schema.constrain` is available for a standalone value schema too, but inside a field block
the schema is inferred from the field's type and each constraint sits on its own line.

For an application type:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let emailSchema : Schema<ContactEmail> =
    Schema.text
    |> Schema.refine ContactEmail.refinement
```


A field block receives the refinement explicitly:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
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


```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let bookingSchema : Schema<Booking> =
    bookingDraftSchema
    |> Schema.admit Booking.create Booking.toDraft
```


For the complete progression from a raw field schema to a canonical refined field, continue to
[Refined Values in Schema](/schema/refined-values.html).
