---
title: Json
api:
  family: Reified.Json
  sections:
    - id: compile
      title: Compile codecs
      order: 10
      members: [compile]
      facets:
        task: [compile]
        audience: [common]
    - id: write
      title: Serialize JSON
      order: 20
      members: [serialize, serializeBytes, serializeWith, serializeBytesWith, serializeIndented, serializeToStream, serializeToStreamWith]
      facets:
        task: [serialize]
        capability: [json]
    - id: read
      title: Deserialize JSON
      order: 30
      members: [deserialize, deserializeBytes, tryDeserialize, deserializeStreamAsync, parseData]
      facets:
        task: [deserialize]
        capability: [json]
    - id: formatting
      title: Formatting options
      order: 40
      members: [defaults, indented, reindent]
      facets:
        capability: [formatting]
---

# `Json`

`Json.compile` interprets a `Schema<'model>` as a reusable `JsonCodec<'model>`. Compile once, then use the codec to
serialize trusted models or decode JSON using the schema's field structure. Unlike `Schema.parse`, trusted codec
decoding does not check value constraints; use `Json.parseData` followed by `Schema.parse` for untrusted input.

The codec is built from the explicit schema rather than runtime type inspection. It supports NativeAOT, trimming, and
Fable.

## Choosing a read operation

`Json` exposes three ways to turn JSON text into a model, with different input, output, and failure behavior:

| Operation | Input | Success | Failure |
| --- | --- | --- | --- |
| `deserialize` / `deserializeBytes` | JSON string / UTF-8 bytes | `'model` | raises `JsonCodecException` whose `Path` identifies the first failing schema field |
| `tryDeserialize` | JSON string | `Result<'model, string>` | `Error` with the exception's rendered message, including the path — a string, not structured `SchemaErrors` |
| `parseData` | JSON string | `Result<Data, string>` | `Error` string; produces a `Data` tree for `Schema.parse`, which then applies constraints and accumulates path-aware `SchemaErrors` |

`deserialize` and `deserializeBytes` stop at the first structural failure. `tryDeserialize` is the result-returning
form of the same behavior. `parseData` does not run schema rules at all; it is the lossless JSON-to-`Data` step that
feeds untrusted boundary parsing.

See [JSON Codecs](/schema/json-codecs.html) for streams, buffers, diagnostics, and how trusted codec decoding differs
from untrusted `Data` parsing.
