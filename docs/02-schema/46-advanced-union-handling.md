---
weight: 46
title: Advanced Union Handling
type: docs
description: Preserve existing F# union JSON with explicit tags, payload styles, and field names.
targetFramework: net8.0
---

# Advanced Union Handling

Use the default model for new contracts. Use `Schema.unionWith` when stored documents, clients, or another serializer
already require a different union shape.

The chosen format lives in the schema. Reified uses it consistently for parsing, JSON output, JSON Schema, generated
test data, and inspection.

## Ready-made compatibility formats

| Format | Use | JSON |
| --- | --- | --- |
| Internal tag and named fields | `UnionRepresentations.recommended` | `{ "type": "move", "x": 1, "y": 2 }` |
| FSharp.SystemTextJson default | `UnionRepresentations.fsharpSystemTextJsonDefault` | `{ "Case": "Move", "Fields": [1, 2] }` |
| FSharp.SystemTextJson external, named, and unwrapped | `UnionRepresentations.fsharpSystemTextJsonExternalNamed` | `"stop"`, `{ "rename": "Ada" }`, `{ "resize": { "width": 1, "height": 2 } }` |
| Adjacent tag and named payload | `UnionRepresentations.reifiedAdjacent` | `{ "type": "move", "value": { "x": 1, "y": 2 } }` |
| External tag and named payload | `UnionRepresentations.compactExternal` | `"stop"`, `{ "move": { "x": 1, "y": 2 } }` |

Pass one of these values with the same case catalogue used by `Schema.union`:

```fsharp no-check reason="The case catalogue is application-owned."
let commandSchema =
    Schema.unionWith
        UnionRepresentations.fsharpSystemTextJsonDefault
        commandCases
```


That profile reads and writes FSharp.SystemTextJson's default `Case`/`Fields` format:

```json
{ "Case": "Stop" }
{ "Case": "SetVolume", "Fields": [12.5] }
{ "Case": "Move", "Fields": [10, 20] }
```


## Generate a compatibility format

Set the representation and payload style on `[<DeriveUnion>]`:

```fsharp no-check reason="Generated support requires the schemagen build package."
[<DeriveUnion(
    "Case",
    Representation = UnionRepresentationKind.Adjacent,
    PayloadField = "Fields",
    PayloadStyle = UnionPayloadStyleKind.Positional)>]
type ExistingCommand =
    | [<SchemaName "Stop">] Stop
    | [<SchemaName "SetVolume">] SetVolume of amount: decimal
    | [<SchemaName "Move">] Move of x: int * y: int
```


The `[<SchemaName>]` attributes preserve PascalCase case tags. The named F# fields give schemagen enough information
to construct the cases, but their names do not appear in the positional arrays.

The external named/unwrapped profile writes empty cases as strings, one-field cases as scalars, and multi-field cases
as named objects:

```fsharp no-check reason="Generated support requires the schemagen build package."
[<DeriveUnion(
    Representation = UnionRepresentationKind.External,
    PayloadStyle = UnionPayloadStyleKind.NamedWithUnwrappedSingle,
    UnwrapFieldless = true)>]
type ExistingExternalCommand =
    | Stop
    | Rename of name: string
    | Resize of width: int * height: int
```


```json
"stop"
{ "rename": "Ada" }
{ "resize": { "width": 10, "height": 20 } }
```


## Keep existing field names

If an established named-field contract uses `item`, `item1`, or `item2`, retain meaningful F# names and override the
wire names explicitly:

```fsharp no-check reason="Generated support requires the schemagen build package."
[<DeriveUnion>]
type Command =
    | SetVolume of [<SchemaName "item">] amount: decimal
    | Move of [<SchemaName "item1">] x: int * [<SchemaName "item2">] y: int
```


```json
{ "type": "setVolume", "item": 12.5 }
{ "type": "move", "item1": 10, "item2": 20 }
```


Application code still uses `amount`, `x`, and `y`. Only the JSON keys retain the compatibility names.

In a handwritten case, call `fieldAs` once per field. Its first argument is the JSON key; its second argument reads the
meaningful payload field:

```fsharp no-check reason="The example depends on the application-owned Command union."
type MovePayload = { x: int; y: int }

let tryMoveCase = function
    | Move(x, y) -> Some { x = x; y = y }
    | _ -> None

let moveCase =
    case "move" {
        tryExtract tryMoveCase
        fieldAs "item1" _.x
        fieldAs "item2" _.y
        construct (fun x y -> Move(x, y))
    }
```


## Put several fields in one array

Renaming individual fields cannot produce `{ "type": "move", "items": [10, 20] }`. That JSON uses adjacent tagging
with a positional payload:

```fsharp no-check reason="The case catalogue is application-owned."
let commandSchema =
    Schema.unionWith
        (UnionRepresentation.Adjacent(
            "type",
            "items",
            UnionPayloadStyle.Positional
        ))
        commandCases
```


```json
{ "type": "setVolume", "items": [12.5] }
{ "type": "move", "items": [10, 20] }
```


With FSharp.SystemTextJson, select adjacent tagging, use `"type"` as the tag name, use `"items"` as the union-fields
name, and leave named fields disabled.

## Build a representation directly

For a format not covered by a ready-made value, construct one of:

```fsharp
UnionRepresentation.Internal("kind")
UnionRepresentation.Adjacent("kind", "fields", UnionPayloadStyle.Named)
UnionRepresentation.External(UnionPayloadStyle.Named, true)
```


Adjacent and external formats accept these payload styles:

| Style | JSON for a two-field case | JSON for a one-field case |
| --- | --- | --- |
| `UnionPayloadStyle.Named` | `{ "x": 10, "y": 20 }` | `{ "amount": 12.5 }` |
| `UnionPayloadStyle.Positional` | `[10, 20]` | `[12.5]` |
| `UnionPayloadStyle.UnwrappedSingle` | invalid for two fields | `12.5` |
| `UnionPayloadStyle.NamedWithUnwrappedSingle` | `{ "x": 10, "y": 20 }` | `12.5` |
| `UnionPayloadStyle.PositionalWithUnwrappedSingle` | `[10, 20]` | `12.5` |

`UnwrappedSingle` requires every payload case to contain exactly one field. The adaptive styles unwrap only one-field
cases and retain named objects or positional arrays for larger cases.

## Match Reified's default in FSharp.SystemTextJson

Use this configuration when FSharp.SystemTextJson and Reified must write the same default JSON:

```fsharp no-check reason="This example requires FSharp.SystemTextJson."
let fsharpOptions =
    JsonFSharpOptions
        .Default()
        .WithUnionInternalTag()
        .WithUnionTagName("type")
        .WithUnionNamedFields()
        .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)
        .WithUnionFieldNamingPolicy(JsonNamingPolicy.CamelCase)
        .WithUnionUnwrapSingleCaseUnions(true)
        .WithSkippableOptionFields(SkippableOptionFields.FromJsonSerializerOptions)

let jsonOptions =
    JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
```


Apply `WithUnionUnwrapFieldlessTags()` only to an all-fieldless union through a per-type converter attribute. If it is
global, a mixed union can write its empty case as `"stop"` while writing its other cases as tagged objects. Reified
uses a string only when every case is fieldless.

Optional record fields omit `None` by default. Add `Schema.mustSupply` when the key must be present; `None` then writes
as JSON `null`.

## JSON Schema and inspection

JSON Schema output matches the selected JSON:

- internal unions become `oneOf` object branches with a required constant tag;
- positional payloads become fixed arrays using `prefixItems`, `minItems`, and `maxItems`;
- external unions become single-property objects, plus strings when fieldless tags are unwrapped;
- enums become string `enum` values;
- transparent values use the underlying value's schema.

`Inspect` returns the chosen `UnionRepresentation` and each case's empty, value, or named-fields form. Tools built on
Reified therefore see the same format that the parser and JSON codec use.
