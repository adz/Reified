---
weight: 55
title: Secrets
description: Seal sensitive values and redact schema diagnostics and JSON output.
targetFramework: net8.0
---

# Secrets

`Secret<'value>` is a sealed reference type in `Reified.Refinements`. Its invariant is redacted display, not a
validation rule. It has reference equality, not structural equality. `ToString`, `%O`, `%A` and JavaScript
`JSON.stringify` display `<redacted>`. The supported payload accessor is `Secret.reveal`:

```fsharp isolated
open Reified.Refinements

let password = Secret.create "example-password"
assert (string password = "<redacted>")
let suppliedToClient = Secret.reveal password
```

This is not encryption or access control. The value remains in memory. Do not log the result of `reveal` or the
original string, and do not expect a raw `Data` tree or a caller-written serializer to infer which strings are
secrets. No key-name heuristic is used.

## Declare a secret field

The field resolver supplies the canonical schema for `Secret<'value>`, recursively resolving the payload schema.
Use `Schema.secret<'value> ()` for a standalone canonical schema, or `Schema.secretWith payloadSchema` for explicit
payload validation:

```fsharp isolated
open Reified
open Reified.Refinements
open Reified.SchemaDSL

type Credentials = { Password: Secret<string> }
let credentials = schema<Credentials> {
    field _.Password
    construct (fun password -> { Password = password })
}
let passwordSchema = Schema.text |> Schema.constrain (Constraint.minLength 12) |> Schema.secretWith
let parsed = Schema.parse passwordSchema (Data.Text "example-password")
```

`Schema.parse` and `Schema.check` run payload validation inside the secret boundary. Any rejected payload becomes
one value-free `secret.invalid` issue with message `<redacted>` at the secret field's path. Inner map keys, actual
values, authored validator messages and constraint operands are not kept in that issue. `SchemaCheck.complete`
also redacts violations. Exceptions from callbacks remain exceptions, but their message and inner exception are
withheld. `Schema.inspectUnderlying` rejects secret schemas: unsealing requires `Secret.reveal`.

`Schema.parseRetainingInput` replaces declared secret inputs with the placeholder before returning its `Input`, on
success or failure. Field names and aliases are matched ordinal-ignore-case, so `PASSWORD` is redacted for a
`Password` secret field even when the parse rejects it. Collections and aliases are covered. A union that can
contain secrets is retained as one redacted subtree; it is not a lossless redisplay source. The original input
supplied by the caller is not mutated. Use `Schema.parseRetainingInputWith` to apply parse options such as
`Schema.ignoreKeyCase` while retaining input.

## JSON accepts payloads but never writes them

```fsharp isolated
open Reified
open Reified.Refinements

let codec = Json.compile (Schema.secretWith Schema.int)
let decoded = Json.deserialize codec "5432"
assert (Secret.reveal decoded = 5432)
assert (Json.serialize codec decoded = "\"<redacted>\"")
```

Decoding accepts the payload's ordinary wire shape. Encoding always writes the JSON string `"<redacted>"`, even
for a numeric or object payload. This is deliberately **not a round-trip codec** and must not be used to persist
credentials for later recovery. Codec failures under the secret boundary have redacted details too.

Inspection reports `SchemaShape.Secret`, withholding payload defaults, descriptions and constraints. JSON Schema
emits `writeOnly: true` and `x-reified-secret: true`, without claiming to describe the hidden payload constraints.
Schema-derived generation fails closed for this opaque shape; supply test secrets explicitly. Defaults declared on
a payload schema are sealed before they are returned as field values or through `Schema.defaultValue`.
