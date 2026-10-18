# `UnionPayloadStyle`

`UnionPayloadStyle` controls how a case's authored fields are represented inside adjacent or external union tagging. It
is the `payloadStyle` argument of `UnionRepresentation.Adjacent` and `UnionRepresentation.External`, and the
`PayloadStyle` argument of `[<DeriveUnion>]`.

| Style | JSON for a two-field case | JSON for a one-field case |
| --- | --- | --- |
| `UnionPayloadStyle.Named` | `{ "x": 10, "y": 20 }` | `{ "amount": 12.5 }` |
| `UnionPayloadStyle.Positional` | `[10, 20]` | `[12.5]` |
| `UnionPayloadStyle.UnwrappedSingle` | invalid for two fields | `12.5` |
| `UnionPayloadStyle.NamedWithUnwrappedSingle` | `{ "x": 10, "y": 20 }` | `12.5` |
| `UnionPayloadStyle.PositionalWithUnwrappedSingle` | `[10, 20]` | `12.5` |

`UnwrappedSingle` requires every payload case to contain exactly one field. The adaptive styles
(`NamedWithUnwrappedSingle`, `PositionalWithUnwrappedSingle`) unwrap only one-field cases and retain named objects or
positional arrays for larger cases.

See [Advanced Union Handling](/schema/advanced-union-handling.html) for JSON examples of each style.
