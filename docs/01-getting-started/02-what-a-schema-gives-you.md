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
| test data that obeys the rules | `SchemaGen` | generated values that satisfy the declaration (see [Testing schema guarantees](/data/testing-schema-guarantees.html)) |

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
| Error errors ->
    for issue in SchemaErrors.toList errors do
        printfn "%s: %s" (SchemaPath.format issue.Path) (SchemaError.render issue.Error)
// age: Expected a value at least 13, but was 11.
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
`compile` does the work up front; `serialize` and `deserialize` are the hot path. The codec enforces JSON
structure and required fields but skips constraint metadata, so decoding failures are not `SchemaErrors`:
`Json.deserialize` raises `JsonCodecException` with a schema-relative path, and `Json.tryDeserialize` returns the
first structural failure as `Result<_, string>`. Parse untrusted input with `Schema.parse` when you need
path-aware accumulated diagnostics.

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

```fsharp no-check reason="Illustrative pseudocode: `configContract`, `raw`, and `...` stand for the reader's own contract, input, and handling, not values this page constructs."
match Contract.parse configContract raw with
| Ok config -> ...
| Error (ContractError.VersionUnrecognized version) -> ...
| Error (ContractError.ParseFailed(version, diagnostics)) -> ...
| Error (ContractError.Migration failure) -> ...
```


Old clients keep working without version-specific fields in the current model. Malformed payloads carry
`ContractError.ParseFailed` with path-aware `SchemaErrors`. Migration failures carry `ContractError.Migration`: a
hand-written migration returns `MigrationError.MigrationFailed` (a message, no paths), while a migrated value that
fails the current schema's recheck returns `MigrationError.RevalidationFailed` with the same path-aware
`SchemaErrors` as `Schema.parse`.

→ [Contracts and versioning](/schema/versioned-contracts.html)

## Build test data that obeys the rules

`SchemaGen` derives values that satisfy the declaration's rules; raise the minimum age and the generated fixtures
follow. When a rule has no sound generation — a `pattern`, a custom predicate, or any opaque constraint — generation
fails closed with `UnsupportedConstraint`, naming the field path and the rule, instead of emitting fixtures that
ignore it.

```fsharp no-check reason="SchemaGen.model lives in the non-packable Reified.Schema.Testing adapter and returns an FsCheck generator; this is the call shape, resolved in the linked page's own context, not by this page's compile check."
open Reified.Schema.Testing

let generated = SchemaGen.model signupSchema
// Result<FsCheck.FSharp.Gen<Signup>, SchemaGenerationError>
```

The generator lives in the non-packable `Reified.Schema.Testing` adapter; copy or adapt the pattern shown
in [Testing schema guarantees](/data/testing-schema-guarantees.html).

→ [Testing schema guarantees](/data/testing-schema-guarantees.html) · [Building test cases](/data/how-to-build-test-cases.html)
