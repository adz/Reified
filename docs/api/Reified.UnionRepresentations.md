# `UnionRepresentations`

`UnionRepresentations` holds complete, ready-made union wire representations for recommended authoring and
serializer-compatible lifting. Pass one of these values together with the same case catalogue used by `Schema.union` to
`Schema.unionWith`; the chosen format is then used consistently for parsing, JSON output, JSON Schema, generated test
data, and inspection.

| Format | Use | JSON |
| --- | --- | --- |
| Internal tag and named fields | `UnionRepresentations.recommended` | `{ "type": "move", "x": 1, "y": 2 }` |
| FSharp.SystemTextJson default | `UnionRepresentations.fsharpSystemTextJsonDefault` | `{ "Case": "Move", "Fields": [1, 2] }` |
| FSharp.SystemTextJson external, named, and unwrapped | `UnionRepresentations.fsharpSystemTextJsonExternalNamed` | `"stop"`, `{ "rename": "Ada" }`, `{ "resize": { "width": 1, "height": 2 } }` |
| Adjacent tag and named payload | `UnionRepresentations.reifiedAdjacent` | `{ "type": "move", "value": { "x": 1, "y": 2 } }` |
| External tag and named payload | `UnionRepresentations.compactExternal` | `"stop"`, `{ "move": { "x": 1, "y": 2 } }` |

`UnionRepresentations.recommended` is Reified's default internally tagged, named-field representation.
`fsharpSystemTextJsonDefault` reads and writes FSharp.SystemTextJson's default `Case`/`Fields` positional format.

See [Advanced Union Handling](/schema/advanced-union-handling.html) for the recipes that use these profiles.
