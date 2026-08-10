---
title: What a schema gives you
linkTitle: What a schema gives you
description: The seven jobs one schema declaration does — parsing input, checking values, JSON, published contracts, form metadata, versioning, and test data.
weight: 2
type: docs
---

Around one model you end up doing the same seven jobs: read input into it, check values that arrived some
other way, serialize it, publish its shape, describe it to a UI, accept last year's payloads, and build test
data for it. Each is usually its own configuration — attributes, a serializer contract, a Swagger annotation,
a fixture builder — and each has to be told the rules again.

The schema is the source for all seven. You write it once:

```fsharp
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

The job the getting-started page walks through. `Schema.parse` takes a `Data` tree from any source — a form
post, a query string, JSON, configuration — converts each field, checks each rule, and calls the constructor.
Every independent field is checked, so a failure carries *all* the problems with their paths, not just the
first. Nothing downstream has to wonder whether validation ran: no `Signup` exists unless it passed.

→ [Modelling with Schema](/schema/quickstart/) · [Input sources](/schema/input-sources/)

## Check a value you already hold

Not every model arrives as input. A record literal in a test, a row mapped out of a database, a payload
deserialized by something else — those are already `Signup` values, and the type system has no opinion about
whether the rules hold. `Schema.check` runs the same field rules and the same constructor against a value
that already exists:

```fsharp
let imported = { Email = "ada"; Age = 11; Newsletter = false }

match Schema.check signupSchema imported with
| Ok signup -> register signup
| Error errors -> for issue in SchemaErrors.toList errors do report issue
// age:   Expected a value at least 13, but was 11.
// email: Expected an email address, but was ada.
```

Same rules, same error shape, same paths as parsing — only the input differs. This is the admission decision
for models that stay publicly constructible; when you would rather make the invalid value unrepresentable in
the first place, see [Trusted construction](/schema/trusted-construction/).

→ [Trusted construction](/schema/trusted-construction/)

## Read and write JSON

```fsharp
open Reified.Schema.Json

let codec = Json.compile signupSchema     // compile once, typically at startup

Json.serialize codec { Email = "ada@example.org"; Age = 36; Newsletter = true }
// {"email":"ada@example.org","age":36,"newsletter":true}
```

There is no second description of the wire shape to keep in step. The codec is built from the schema's typed
field plan rather than from runtime type inspection, so it works under NativeAOT, trimming, and Fable.
`compile` does the work up front; `serialize` and `deserialize` are the hot path. Decoding failures come back
as the same path-aware `SchemaErrors`.

→ [JSON codecs](/schema/json-codec/)

## Publish the shape to other tools

```fsharp
JsonSchema.generate signupSchema
// {"type":"object",
//  "properties":{"email":{"type":"string", …},
//                "age":{"type":"integer","minimum":13},
//                "newsletter":{"type":"boolean"}},
//  "required":["email","age","newsletter"]}
```

`"minimum": 13` was not written twice — it is the `atLeast 13` from the declaration, which is why the
published document cannot drift from the code that enforces it. Clients generated from it are generated from
what the server actually does. HTTP endpoints publish OpenAPI the same way.

→ [HTTP servers and OpenAPI](/schema/http-servers/)

## Describe the model to a form or admin UI

```fsharp
Inspect.model signupSchema
// email      -> [ "present"; "email" ]
// age        -> [ "atLeast" ]
// newsletter -> [ ]
```

Finite metadata: field names, shapes, the constraints on each. Nothing is parsed and nothing is checked. This
is what forms, admin UIs, and documentation generators read when they need to *describe* the model rather than
enforce it — render the right input control per field, or show the rule beside it before the user submits.

→ [Schema overview](/schema/overview/)

## Accept payloads from older versions

A schema describes the model you have now. A contract registers the versions you have shipped, with a
migration from each one, and reads versioned input into the current model:

```fsharp
match Contract.parse configContract raw with
| Ok config -> use config
| Error (ContractError.VersionUnrecognized version) -> ...
| Error (ContractError.ParseFailed(version, diagnostics)) -> ...
```

Old clients keep working without the current model carrying the scar tissue. Migration failures and malformed
payloads carry the same `SchemaErrors` with the same paths.

→ [Contracts and versioning](/schema/contracts/)

## Build test data that obeys the rules

The last job runs the rules backwards: instead of rejecting values that violate the declaration, produce
values that satisfy it. Fixtures stop being hand-maintained lists that quietly drift from the rules — raise
the minimum age and the generated data follows.

→ [Data](/data/) · [Building test cases](/data/how-to-build-test-cases/)

## Why one declaration and not seven

Each of these jobs could be configured separately, and usually is. The cost is not the typing — it is that
seven descriptions of the same model are free to disagree, and they only disagree in production. Making the
declaration a value means the rule lives in exactly one place, and each job is a different reading of it. It
also means a new job costs nothing at the declaration site: the schemas you have already written gain it for
free.
