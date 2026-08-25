---
title: Derivation Attributes
linkTitle: Attributes
description: Complete attribute reference for schema derivation, with handwritten Schema DSL equivalents.
weight: 20
targetFramework: net8.0
---

# Derivation Attributes

Open `Reified.DerivedSchema` to use the attributes without suffixes:

```fsharp
open Reified.DerivedSchema
```


Attributes are read from F# source at generation time. They do not use runtime reflection. Each generated operation in
the table has the same meaning as its linked handwritten [Schema DSL](/schema/dsl.html) equivalent.

## Type and construction attributes

| Attribute | Applies to | Short description | Schema DSL equivalent |
| --- | --- | --- | --- |
| `[<DeriveSchema>]` | record | Generate a schema and companion operations for a public wire record. `Chain` and `Version` optionally identify a version series. | [`schema<'model> { ... }`](/schema/dsl.html#fields-without-blocks) |
| `[<DeriveUnion>]` | union | Derive the recommended internally tagged union with discriminator `type`. Fieldless and directly named payload cases may be mixed. | [`Schema.union`](/schema/union-schemas.html#generate-the-same-schemas) |
| `[<DeriveUnion "kind">]` | union | Derive the same named-field format with a custom discriminator. | [`Schema.unionWith`](/schema/advanced-union-handling.html#build-a-representation-directly) |
| `[<DeriveUnion(Representation = UnionRepresentationKind.Adjacent, PayloadField = "Fields", PayloadStyle = UnionPayloadStyleKind.Positional)>]` | union | Keep an adjacent positional contract such as FSharp.SystemTextJson's default. | [`Schema.unionWith`](/schema/advanced-union-handling.html#generate-a-compatibility-format) |
| `[<DeriveUnion(Representation = UnionRepresentationKind.External, PayloadStyle = UnionPayloadStyleKind.NamedWithUnwrappedSingle, UnwrapFieldless = true)>]` | union | Keep external tagging with named multi-field and unwrapped empty/single-field cases. | [`Schema.unionWith`](/schema/advanced-union-handling.html#generate-a-compatibility-format) |
| `[<SchemaName "wire_name">]` | record field or nullary union case | Override the configured wire name or case tag. | [`fieldAs "wire_name" _.Field`](/schema/dsl.html#fields-without-blocks) |
| `[<SchemaConstructor>]` | static member | Call this member with fields in declaration order instead of constructing a record literal. | [`construct`](/schema/dsl.html#constructors) |

## Field attributes

| Attribute | Short description | Schema DSL equivalent |
| --- | --- | --- |
| `[<Pattern "expr">]` | Require text to match a regular expression. | [`constrain (pattern "expr")`](/schema/dsl.html#constraint-equivalents) |
| `[<Min n>]` | Set the minimum natural length of text, list, or map. | [`constrain (minLength n)`](/schema/dsl.html#constraint-equivalents) |
| `[<Max n>]` | Set the maximum natural length of text, list, or map. | [`constrain (maxLength n)`](/schema/dsl.html#constraint-equivalents) |
| `[<Length n>]` | Require an exact natural length. | [`constrain (length n)`](/schema/dsl.html#constraint-equivalents) |
| `[<LengthBetween(min, max)>]` | Bound natural length inclusively. | [`constrain (lengthBetween min max)`](/schema/dsl.html#constraint-equivalents) |
| `[<Present>]` | Require a string, collection, or optional value to be present/non-empty. | [`constrain present`](/schema/dsl.html#constraint-equivalents) |
| `[<Supplied>]` | Require the input object to contain this field key. | [`mustSupply`](/schema/dsl.html#constraint-equivalents) |
| `[<Format "name">]` | Attach open format metadata without adding a check. | [`format (SchemaFormat.create "name")`](/schema/dsl.html#constraint-equivalents) |
| `[<AtLeast n>]` | Inclusive numeric lower bound (`>=`). | [`constrain (atLeast n)`](/schema/dsl.html#constraint-equivalents) |
| `[<GreaterThan n>]` | Exclusive numeric lower bound (`>`). | [`constrain (greaterThan n)`](/schema/dsl.html#constraint-equivalents) |
| `[<AtMost n>]` | Inclusive numeric upper bound (`<=`). | [`constrain (atMost n)`](/schema/dsl.html#constraint-equivalents) |
| `[<LessThan n>]` | Exclusive numeric upper bound (`<`). | [`constrain (lessThan n)`](/schema/dsl.html#constraint-equivalents) |
| `[<MultipleOf n>]` | Require a numeric value to be a whole multiple of `n`. | [`constrain (multipleOf n)`](/schema/dsl.html#constraint-equivalents) |
| `[<Distinct>]` | Require all list elements to be distinct. | [`constrain distinct`](/schema/dsl.html#constraint-equivalents) |
| `[<Email>]` | Apply the built-in email constraint to text. | [`constrain email`](/schema/dsl.html#constraint-equivalents) |
| `[<Default value>]` | Supply a value when the input key is omitted; invalid on `option` fields. | [`defaultValue value`](/schema/dsl.html#constraint-equivalents) |

Numeric bound and default attributes accept source literals supported by their target field. The generator reads the
literal text, preserving decimal precision rather than round-tripping it through reflection.

## Example

```fsharp
[<DeriveSchema>]
type Product =
    { [<SchemaName "product_code"; Pattern "^[A-Z0-9]+$"; Present>]
      Code: string
      [<AtLeast 0; LessThan 1000000>]
      Price: decimal
      [<Min 1; Distinct>]
      Tags: string list
      [<Default true>]
      Active: bool }
```


Multiple constraints are emitted in attribute order and execute like multiple `constrain` operations in a field block.
Doc comments on records and fields become schema descriptions and generated XML documentation.
