---
weight: 45
title: Union Schemas
type: docs
description: Handwritten and generated schemas for enums, domain wrappers, and general F# unions.
targetFramework: net8.0
---

# Union Schemas

Start with the three handwritten tools. They make the default wire model explicit:

- `Schema.enum` writes an all-fieldless union as a string;
- `Schema.convert` writes a single-case domain wrapper as its underlying value;
- `Schema.union` writes every other union as an object with a `type` tag and named fields.

After choosing the shape by hand, `[<DeriveUnion>]` can generate the same schema from the F# declaration.

## Write the default schema by hand

### General unions: `Schema.union`

Consider a union with an empty case and a payload case:

```fsharp
type SetVolumeFields = { Amount: decimal }

type Command =
    | Stop
    | SetVolume of SetVolumeFields
```


First build the payload's record schema:

```fsharp no-check reason="The example depends on the application-owned SetVolumeFields record."
let setVolumeFields =
    schema<SetVolumeFields> {
        field _.Amount
        construct (fun amount -> { Amount = amount })
    }
```


Then connect each F# case to `Schema.union`:

```fsharp no-check reason="The example depends on the application-owned Command union and payload schema."
let commandSchema =
    Schema.union
        [ UnionCase.empty "stop" Stop _.IsStop
          UnionCase.fields
              "setVolume"
              SetVolume
              (function SetVolume fields -> Some fields | _ -> None)
              setVolumeFields ]
```


`UnionCase.empty` takes the JSON tag, the value to construct, and a function that recognizes the case.
`UnionCase.fields` takes the tag, the case constructor, a function that extracts its fields, and the fields' schema.

The schema reads and writes:

```json
{ "type": "stop" }
{ "type": "setVolume", "amount": 12.5 }
```


Every case uses the same `type` property. An empty case inside a mixed union remains `{ "type": "stop" }`; it does
not become the string `"stop"`.

Reified rejects duplicate tags, a payload field named `type`, and inspectors that match either no case or more than
one case.

### String enums: `Schema.enum`

An all-fieldless union is a JSON string:

```fsharp
type Status =
    | Pending
    | InProgress
    | Complete
```


```fsharp no-check reason="The example depends on the application-owned Status union."
let statusSchema =
    Schema.enum
        [ EnumCase.create "pending" Pending
          EnumCase.create "inProgress" InProgress
          EnumCase.create "complete" Complete ]
```


```json
"inProgress"
```


### Domain wrappers: `Schema.convert`

A single-case union containing one value can use that value directly:

```fsharp
type CustomerId = CustomerId of string
```


```fsharp no-check reason="The example depends on the application-owned CustomerId union."
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

```fsharp no-check reason="Generated support requires the schemagen build package."
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
