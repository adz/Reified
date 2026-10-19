---
weight: 10
title: Using Data with Reified
type: docs
description: Feeding schemas, boundary parsing, and redisplay-aware error reporting.
targetFramework: net8.0
---

# Using Data with the rest of Reified

Every schema parse takes a `Data` value, so any source that can become `Data` can feed a schema.

## Feeding a schema

```fsharp
open Reified
open Reified.DataDSL
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup = { Email: string; Age: int }

let signupSchema =
    schema<Signup> {
        field _.Email { constraints [ email ] }
        field _.Age
        construct (fun email age -> { Email = email; Age = age })
    }

let input =
    data [
        "email" => "ada@example.com"
        "age" => 42
    ]

let parsed = Schema.parse signupSchema input
```


The same schema accepts input from `Data.ofJsonDocument` for a request body, `Data.ofNameValues` for a form post,
`Data.ofCliArgs` for a command line, and `Data.ofConfiguration` for settings. The schema does not depend on the source.

Adapters produce different trees. JSON carries typed
Booleans and numbers, while form, CLI, and configuration leaves are all text that the schema still has
to parse. Repeated names build a list in `ofNameValues` but overwrite in `ofConfiguration`, where lists
come from indexed key segments instead. CLI flags arrive as the text `"true"` or `"false"`. The same schema accepts
input from any of them, but sources must agree on the resulting tree, not just the logical content. See
[the adapter rules](/data/converting-data.html) for what each one produces.

A fixture written with `data [ ... ]` exercises the same parse path as production JSON without a serializer.

## Redisplay and error reporting

`Schema.parseRetainingInput` keeps the original `Data` alongside the parse result, so a failed form
round-trip can re-show exactly what the user typed next to each field error. The paths in schema
diagnostics (`address.city`, `lines[2].quantity`) address the input tree.
`Data.redisplayPath` (string form) or `Data.redisplayAt` (`DataPath` form) recovers the raw fragment
at any of them.

See [redisplay and field errors](/schema/redisplay-and-field-errors.html) for the full
pattern.

## Boundary adapters

An adapter builds `Data` from a source such as route values, query strings, headers, or a JSON body before any schema
is involved. Supporting another source means producing `Data`; every schema and interpreter downstream stays
unchanged.
