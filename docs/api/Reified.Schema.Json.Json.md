---
title: Json
api:
  family: Reified.Schema.Json.Json
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
Fable. `deserialize` and `deserializeBytes` raise `JsonCodecException` on the first structural failure; its `Path`
identifies the failing schema field. `tryDeserialize` returns `Result<_, string>` instead: its error is the exception's
rendered message (including the path), not a structured path or accumulated `SchemaErrors`.

See [JSON Codecs](/schema/json-codecs.html) for streams, buffers, diagnostics, and how trusted codec decoding differs
from untrusted `Data` parsing.
