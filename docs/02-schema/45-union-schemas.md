---
weight: 45
title: Union Schemas
type: docs
description: Handwritten and generated schemas for enums, domain wrappers, and general F# unions.
targetFramework: net8.0
---

# Union Schemas

Three handwritten tools make the default wire model explicit:

- `Schema.enum` writes an all-fieldless union as a string;
- `Schema.convert` writes a single-case domain wrapper as its underlying value;
- `Schema.union` writes every other union as an object with a `type` tag and named fields.

After choosing the shape by hand, `[<DeriveUnion>]` can generate the same schema from the F# declaration.

## Write the default schema by hand

### General unions: `Schema.union`

Consider a union with an empty case and a named payload field:

```fsharp
type Command =
    | Stop
    | SetVolume of amount: decimal
```

Select the payload case with a function that returns its fields and rejects every other case:

```fsharp no-check reason="Uses the application-owned Command union."
let trySetVolumeCase = function
    | SetVolume amount -> Some amount
    | _ -> None
```

Then connect each F# case to `Schema.union`:

```fsharp no-check reason="Uses the application-owned Command union."
let commandSchema =
    Schema.union [
        UnionCase.empty "stop" Stop (function Stop -> true | _ -> false)
        case "setVolume" {
            tryExtract trySetVolumeCase
            fieldAs "amount" id
            construct SetVolume
        }
    ]
```

`UnionCase.empty` takes the JSON tag, the value to construct, and a function that recognizes the case. A `case` block
takes the tag, selects its payload with `tryExtract`, then declares named fields and the case constructor with the
record-schema vocabulary. The extractor returns `None` for every other case; getters inside the block operate only on
its selected payload.

The schema reads and writes:

```json
{ "type": "stop" }
{ "type": "setVolume", "amount": 12.5 }
```


Every case uses the same `type` property. An empty case inside a mixed union remains `{ "type": "stop" }`; it does
not become the string `"stop"`.

Duplicate tags and a payload field named `type` are rejected when the schema is built: `Schema.union` throws
`System.ArgumentException` for either, because a tag must identify exactly one case and a payload field must not
collide with the discriminator. Case inspectors are checked when an existing value is validated with `Schema.check`:
an inspector that matches no case reports `union.case`, and more than one matching inspector reports
`union.case.ambiguous`. Parsing can only report an unknown tag, since tags are already unique.

### String enums: `Schema.enum`

An all-fieldless union is a JSON string. Use `Schema.enum` with an explicit list of string tags and values;
no reflection or separate `string` field is needed:

```fsharp
type Status =
    | Pending
    | InProgress
    | Complete
```


```fsharp
let statusSchema =
    Schema.enum
        [ EnumCase.create "pending" Pending
          EnumCase.create "inProgress" InProgress
          EnumCase.create "complete" Complete ]
```


```fsharp
let codec = Json.compile statusSchema
let encoded = Json.serialize codec InProgress // "\"inProgress\""
let decoded = Json.deserialize codec encoded // InProgress
```

```json
"inProgress"
```


### Domain wrappers: `Schema.convert`

A single-case union containing one value can use that value directly:

```fsharp
type CustomerId = CustomerId of string
```


```fsharp no-check reason="Uses the application-owned CustomerId union."
let customerIdSchema =
    Schema.text
    |> Schema.convert CustomerId (fun (CustomerId value) -> value)
```


```json
"customer-123"
```


Adding another case would change this JSON from a bare string to a tagged object. Use this form for domain wrappers,
not for unions expected to gain alternatives.

## Generate the same schemas

Schemagen applies the same three rules from the complete F# declaration:

1. Every case is fieldless: generate `Schema.enum`.
2. Exactly one case has exactly one field: generate a transparent `Schema.convert` schema.
3. Otherwise: generate `Schema.union` with a `type` tag and named fields.

Mark a general union with `[<DeriveUnion>]`:

```fsharp no-check reason="Needs the Reified.SchemaGen.MSBuild generator."
[<DeriveUnion>]
type Command =
    | Stop
    | SetVolume of amount: decimal
    | Move of x: int * y: int
```


Schemagen generates JSON equivalent to the handwritten schemas:

```json
{ "type": "stop" }
{ "type": "setVolume", "amount": 12.5 }
{ "type": "move", "x": 10, "y": 20 }
```


All-fieldless and single-case/single-field unions do not need `[<DeriveUnion>]` when they appear in a generated record.
Schemagen recognizes those two shapes automatically. It reads F# source during the build and uses no runtime
reflection.

## Advice: name every payload field

Give every value a meaningful name:

```fsharp
// Recommended
type NamedCommand =
    | SetVolume of amount: decimal
    | Move of x: int * y: int
```


The names become JSON properties:

```json
{ "type": "setVolume", "amount": 12.5 }
{ "type": "move", "x": 10, "y": 20 }
```


Avoid unnamed fields:

```fsharp
// Avoid
type UnnamedCommand =
    | SetVolume of decimal
    | Move of int * int
```


FSharp.SystemTextJson accepts that declaration. With internal tagging and named fields, generated F# names can reach
the wire:

```json
{ "type": "setVolume", "item": 12.5 }
{ "type": "move", "item1": 10, "item2": 20 }
```


Those valid but meaningless keys can become a public contract and generate client members such as
`item: BigDecimal`. Renaming them later is a breaking wire change.

Reified schemagen does not invent placeholder names. It rejects an unnamed field on a general union with a build
error. Name the field and rerun schemagen.

If those keys already belong to an established contract, keep them explicitly. See
[Advanced Union Handling](/schema/advanced-union-handling.html#keep-existing-field-names).

## Next

Use [Advanced Union Handling](/schema/advanced-union-handling.html) to keep an existing serializer format, select
internal, adjacent, or external tagging, choose named or positional payloads, and match FSharp.SystemTextJson options.
