# `UnionRepresentation`

`UnionRepresentation` is a complete, inspectable union wire representation. For a format not covered by a ready-made
value in `UnionRepresentations`, construct one directly:

```fsharp
UnionRepresentation.Internal("kind")
UnionRepresentation.Adjacent("kind", "fields", UnionPayloadStyle.Named)
UnionRepresentation.External(UnionPayloadStyle.Named, true)
```

- `Internal tagField` writes an internal tag inside the payload object, with named fields.
- `Adjacent (tagField, payloadField, payloadStyle)` writes the tag beside a single payload member holding the fields.
- `External (payloadStyle, unwrapFieldless)` writes a single-property object keyed by the case tag; `unwrapFieldless`
  encodes fieldless cases as bare strings.

Adjacent and external formats accept every `UnionPayloadStyle`. See
[`UnionPayloadStyle`](/api/Reified.UnionPayloadStyle.html) for how each style shapes the payload, and
[Advanced Union Handling](/schema/advanced-union-handling.html) for the JSON each combination produces.
