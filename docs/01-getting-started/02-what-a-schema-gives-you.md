---
title: What a schema gives you
linkTitle: What a schema gives you
description: The seven jobs one schema declaration does, parsing input, checking values, JSON, published contracts, form metadata, versioning, and test data.
weight: 2
type: docs
targetFramework: net8.0
---

A model needs input parsing, validation of existing values, serialization, a published shape, UI metadata,
older-payload support, and test data. Separate configuration, attributes, serializer contracts, Swagger annotations,
and fixture builders repeat its rules.

One schema supplies all seven:

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup =
    { Email: string
      Age: int
      Newsletter: bool }

let signupSchema =
    schema<Signup> {
        field _.Email { constraints [ present; email ] }
        field _.Age { constrain (atLeast 13) }
        field _.Newsletter
        construct (fun email age newsletter ->
            { Email = email; Age = age; Newsletter = newsletter })
    }
```


| What you need | Read the schema with | You get |
| --- | --- | --- |
| input turned into a model | `Schema.parse` | the model, or `SchemaErrors` with paths |
| a value you already hold checked | `Schema.check` | the same value, or `SchemaErrors` |
| JSON in and out | `Json.compile` | a reflection-free codec |
| the shape published | `JsonSchema.generate` | a JSON Schema document |
| the model described to a UI | `Inspect.model` | field metadata, for forms and admin UIs |
| older payloads accepted | `Contract.parse` | old wire input mapped to the current model |
| test data that obeys the rules | `SchemaGen` | generated values that satisfy the declaration |

## Turn untrusted input into a model

`Schema.parse` takes a `Data` tree from a form post, query string, JSON, or configuration. It converts each field,
checks each rule, and calls the constructor.
Every independent field is checked, so a failure carries *all* problems with their paths, rather than only the
first. No `Signup` exists unless it passed the checks.

→ [Schema quickstart](/schema/quickstart.html) · [Input sources](/schema/input-sources.html)

## Check a value you already hold

A record literal in a test, a row mapped from a database, or a payload deserialized elsewhere is already a
`Signup` value. Its type does not establish whether the rules hold. `Schema.check` runs the same field rules
and the same constructor against a value that already exists:

```fsharp
let imported = { Email = "ada"; Age = 11; Newsletter = false }

match Schema.check signupSchema imported with
| Ok signup -> printfn "%A" signup
| Error errors -> for issue in SchemaErrors.toList errors do printfn "%s" (SchemaError.render issue.Error)
// age:   Expected a value at least 13, but was 11.
// email: Expected an email address, but was ada.
```


`Schema.check` uses the parsing rules, error shape, and paths on an existing value. For models that remain publicly
constructible, this is the admission check. To make invalid values unrepresentable, see
[Trusted construction](/schema/trusted-construction.html).

→ [Trusted construction](/schema/trusted-construction.html)

## Read and write JSON

```fsharp
let codec = Json.compile signupSchema     // compile once, typically at startup

Json.serialize codec { Email = "ada@example.org"; Age = 36; Newsletter = true }
// {"email":"ada@example.org","age":36,"newsletter":true}
```


There is no second description of the wire shape to keep in step. The codec is built from the schema's typed
field plan rather than from runtime type inspection, so it works under NativeAOT, trimming, and Fable. Records with many fields rely on build settings the
`Reified.Schema` package applies for NativeAOT; see [Compiler-Directed, AOT, and Fable](/notes/aot-trimming-fable.html#records-with-many-fields).
`compile` does the work up front; `serialize` and `deserialize` are the hot path. Decoding failures come back
as the same path-aware `SchemaErrors`.

→ [JSON codecs](/schema/json-codecs.html)

## Publish the shape to other tools

```fsharp
JsonSchema.generate signupSchema
// {"type":"object",
//  "properties":{"email":{"type":"string", …},
//                "age":{"type":"integer","minimum":13},
//                "newsletter":{"type":"boolean"}},
//  "required":["email","age","newsletter"]}
```


`"minimum": 13` comes from the declaration's `atLeast 13`, so the published document uses the rule the code
enforces. Clients generated from it use the same shape as the parser and codec.

→ [`JsonSchema` API reference](/api/Reified.JsonSchema.html)

## Describe the model to a form or admin UI

```fsharp
Inspect.model signupSchema
// email      -> [ "present"; "email" ]
// age        -> [ "atLeast" ]
// newsletter -> [ ]
```


Finite metadata: field names, shapes, the constraints on each. Nothing is parsed and nothing is checked. Forms,
admin UIs, and documentation generators use it to *describe* the model, choose an input control per field, or show
the rule before submission.

→ [`Inspect` API reference](/api/Reified.Inspect.html)

## Accept payloads from older versions

A schema describes the model you have now. A contract registers the versions you have shipped, with a
migration from each one, and reads versioned input into the current model:

```fsharp no-check reason="Illustrative pseudocode: `...` and `use config` are placeholders for the reader's own handling, not valid F#."
match Contract.parse configContract raw with
| Ok config -> use config
| Error (ContractError.VersionUnrecognized version) -> ...
| Error (ContractError.ParseFailed(version, diagnostics)) -> ...
```


Old clients keep working without version-specific fields in the current model. Migration failures and malformed
payloads carry the same `SchemaErrors` with the same paths.

→ [Contracts and versioning](/schema/versioned-contracts.html)

## Build test data that obeys the rules

`SchemaGen` produces values that satisfy the declaration. Raise the minimum age and the generated fixtures follow.

→ [Data](/data/index.html) · [Building test cases](/data/how-to-build-test-cases.html)
