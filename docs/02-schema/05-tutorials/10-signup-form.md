---
weight: 10
title: Signup Form Tutorial
description: Declare a schema, parse form input, and redisplay errors.
targetFramework: net8.0
---

# Signup Form Tutorial

This page shows how to declare a schema, parse form input, and redisplay boundary errors without constructing invalid
models.

This tutorial parses a signup form into a trusted model. If any field fails, no model is constructed and the form can
be redisplayed with the user's original input and per-field errors.

## Declare The Model And Schema

The schema declares each field once: external name, getter, and constraints.

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
open Reified
open Reified.SchemaDSL

type Signup = { Email: string; Age: int }

let signupSchema =
    schema<Signup> {
        field _.Email {
            constraints [ email; maxLength 254 ]
        }
        field _.Age {
            constrain (atLeast 13)
        }
        construct (fun email age -> { Email = email; Age = age })
    }
```


`schema<Signup>` anchors the model type. The closing constructor must match every field in declaration order, so
missing or mistyped arguments fail at `construct`.

## Adapt The structured data

Form posts are name/value pairs:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let raw =
    Data.ofNameValues
        [ "email", "not-an-email"
          "age", "12" ]
```


## Parse

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let parsed = Schema.parseRetainingInput signupSchema raw
```


`parsed` is a `RetainedParseResult<Signup, SchemaError>`. On success `parsed.Result` is `Ok signup` and every constraint
already holds. Here both fields fail, so no `Signup` exists anywhere:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
parsed.IsValid              // false
parsed.ErrorsFor "email"    // [ SchemaError.InvalidFormat "email" ]
parsed.ErrorsFor "age"      // [ SchemaError.OutOfRange ... ]
```


## Redisplay The Form

The original input is retained on the parsed value, addressed by the same paths:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
Data.redisplayPath "email" parsed.Input   // "not-an-email", exactly as typed
Data.redisplayPath "age" parsed.Input     // "12"
```


A form template needs only `parsed.Input` and `parsed.ErrorsFor` — there is no half-valid model to guard against.
Use `SchemaError.render` for field-level messages or `RetainedParseResult.renderErrors parsed` for a summary list.

## Use The Trusted Model

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
match parsed.Result with
| Ok signup -> register signup      // constraints already hold; no re-checking downstream
| Error _ -> renderForm parsed
```


`Signup` here is a public record, so the guarantee belongs to the successful parse result, not to the type — other
code can still write a `Signup` literal that skips the schema. That is the right trade for a boundary form model.
When a value's construction history is uncertain, `Schema.check signupSchema value` runs the same constraints over an
already assembled value; when an invariant must hold for every value of the type, use a private representation with a
smart constructor. [Construction Guarantees](/schema/trusted-construction.html) covers the full division.

## Next

- [Nested Models And Collections](/schema/tutorials/nested-and-collections.html) for models inside models.
- [Redisplay And Field Errors](/schema/redisplay-and-field-errors.html) for the full redisplay guide.
- [Construction Guarantees](/schema/trusted-construction.html) for which claims need a private type rather than a schema.
