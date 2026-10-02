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

Declare the model, then attach a wire name, getter, and constraints to each field that needs one:

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

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
missing or mistyped arguments fail at `construct`. `Email` stays a plain `string` here because its rule, a length
limit alongside the format check, is specific to this field rather than to every email in the codebase; see
[Getting Started](/getting-started/index.html) for when a rule belongs on a refined type instead.

## Adapt The Structured Data

Form posts are name/value pairs:

```fsharp
let raw =
    Data.ofNameValues
        [ "email", "not-an-email"
          "age", "12" ]
```


## Parse

```fsharp
let parsed = Schema.parseRetainingInput signupSchema raw
```


`parsed` is a `RetainedParseResult<Signup>`. On success `parsed.Result` is `Ok signup` and every constraint
already holds. Here both fields fail, so no `Signup` exists anywhere:

```fsharp
parsed.IsValid              // false
parsed.ErrorsFor "email"    // [ SchemaError.Violation ... ] for the email-format constraint
parsed.ErrorsFor "age"      // [ SchemaError.Violation ... ] for the atLeast 13 constraint
```


## Redisplay The Form

The original input is retained on the parsed value, addressed by the same paths:

```fsharp
Data.redisplayPath "email" parsed.Input   // "not-an-email", exactly as typed
Data.redisplayPath "age" parsed.Input     // "12"
```


A form template needs only `parsed.Input` and `parsed.ErrorsFor`; there is no half-valid model to guard against.
Use `SchemaError.render` for field-level messages or `RetainedParseResult.renderErrors parsed` for a summary list.

## Use The Trusted Model

```fsharp no-check reason="register and renderForm are the host application's own functions; not values this page can construct standalone."
match parsed.Result with
| Ok signup -> register signup      // constraints already hold; no re-checking downstream
| Error _ -> renderForm parsed
```


`Signup` here is a public record, so the guarantee belongs to the successful parse result, not to the type. Other
code can still write a `Signup` literal that skips the schema, which is fine for a boundary form model whose only
job is to be parsed once and used. When a value's construction history is uncertain instead, `Schema.check
signupSchema value` re-runs the same constraints over an already assembled value.
[Construction Guarantees](/schema/trusted-construction.html) covers the full division, including when an invariant
that must hold for every value of the type calls for a private representation rather than a public record.

## Next

- [Nested Models And Collections](/schema/tutorials/nested-and-collections.html) for models inside models.
- [Redisplay And Field Errors](/schema/redisplay-and-field-errors.html) for the full redisplay guide.
- [Construction Guarantees](/schema/trusted-construction.html) for which claims need a private type rather than a schema.
