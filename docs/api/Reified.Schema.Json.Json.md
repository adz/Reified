# `Json`

`Json.compile` interprets a `Schema<'model>` as a reusable `JsonCodec<'model>`. Compile once, then use the codec to
serialize trusted models or deserialize JSON into the same field plan used by `Schema.parse`.

The codec is built from the explicit schema rather than runtime type inspection. It supports NativeAOT, trimming, and
Fable, and it preserves schema paths in decoding failures.

See [JSON Codecs](/schema/json-codecs.html) for streams, buffers, diagnostics, and the boundary between trusted codec
decoding and untrusted `Data` parsing.
