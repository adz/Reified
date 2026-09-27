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
serialize trusted models or deserialize JSON into the same field plan used by `Schema.parse`.

The codec is built from the explicit schema rather than runtime type inspection. It supports NativeAOT, trimming, and
Fable, and it preserves schema paths in decoding failures.

See [JSON Codecs](/schema/json-codecs.html) for streams, buffers, diagnostics, and the boundary between trusted codec
decoding and untrusted `Data` parsing.
