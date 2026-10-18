---
weight: 20
title: Choosing an API
linkTitle: Choosing an API
type: docs
description: Schema.parse, Schema.check, Json.deserialize, Json.tryDeserialize, and Contract.parse side by side.
targetFramework: net8.0
---

# Choosing an API

Five operations read a declaration. They differ in the input they accept, the validation they run, and the error they
return. Pick by where the input came from and whether the producer is trusted.

| Operation | Input | Validation run | Constructor | Error type | Trust the payload? |
| --- | --- | --- | --- | --- | --- |
| `Schema.parse` | `Data` | Field decoding, constraints, refinements, and constructor invariants | Runs only after every field succeeds | `SchemaErrors` (accumulated, path-aware) | No — the untrusted boundary |
| `Schema.check` | an existing `'model` | Same field rules and constructor as `Schema.parse`, read back from the value | Re-invoked with the checked fields; returns the original value on success | `SchemaErrors` (accumulated, path-aware) | No — the value's history is uncertain |
| `Json.deserialize` | JSON `string` | Wire shape and required fields only; constraint metadata is skipped | Runs; a checked constructor failure raises | raises `JsonCodecException` | Yes — storage, internal service, cache |
| `Json.tryDeserialize` | JSON `string` | Same as `Json.deserialize` | Same as `Json.deserialize` | `Result<'model, string>` with the first structural failure | Yes — same trusted path, non-throwing |
| `Contract.parse` | `Data` | Version selection, that version's schema parse, migrations, then a `Schema.check` of the migrated value | Via each version's parse and the final recheck | `ContractError` | No — old wire shapes must stay readable |

`Json.parseData` is the bridge between the first and third rows: it turns JSON text into `Data` with no schema
applied. `Schema.parseRetainingInput` wraps `Schema.parse` and keeps the original `Data` for redisplay and auditing.

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup = { Email: string; Age: int }

let signupSchema =
    schema<Signup> {
        field _.Email { constraints [ present; email ] }
        field _.Age { constrain (atLeast 13) }
        construct (fun email age -> { Email = email; Age = age })
    }

let data = Data.ofNameValues [ "email", "ada"; "age", "11" ]

Schema.parse signupSchema data
// Result<Signup, SchemaErrors>

Schema.check signupSchema { Email = "ada"; Age = 11 }
// Result<Signup, SchemaErrors>

let codec = Json.compile signupSchema

Json.tryDeserialize codec """{"email":"ada@example.org","age":36}"""
// Ok signup — constraint metadata is not run on the trusted path

Json.tryDeserialize codec """{"email":"ada@example.org","age":"thirty"}"""
// Error "JSON decode failed at $.age: expected digit"
```

## When the same payload needs different treatment

- A request body from a client you do not control goes through `Json.parseData` (or `Json.tryParseData`) into
  `Data`, then `Schema.parse` — never through `Json.deserialize`, which would skip the rules.
- A value read back from your own database or another service you trust can use `Json.deserialize`/
  `Json.tryDeserialize`. If the payload carries constraint attributes that matter, remember the trusted path does
  not read them.
- A value produced by a serializer, mapper, or import goes through `Schema.check`, which re-runs the rules on a
  value that already exists.
- A payload whose shape has shipped in several versions goes through `Contract.parse`, which selects the version,
  parses, migrates, and rechecks the current model.

The same trust distinction is stated once in [JSON codecs](/schema/json-codecs.html) and in the
[Schema or Generated DTO](/schema/schema-or-generated-dto.html) decision.
