---
title: Derivation Attributes
linkTitle: Attributes
description: Complete attribute reference for schema derivation, with handwritten Schema DSL equivalents.
weight: 20
---

# Derivation Attributes

Open `Reified.DerivedSchema` to use the attributes without suffixes:

```fsharp
open Reified.DerivedSchema
```

Attributes are read from F# source at generation time. They do not use runtime reflection. Each generated operation in
the table has the same meaning as its linked handwritten [Schema DSL](../dsl/) equivalent.

## Type and construction attributes

| Attribute | Applies to | Short description | Schema DSL equivalent |
| --- | --- | --- | --- |
| `[<DeriveSchema>]` | record | Generate a schema and companion operations for a public wire record. `Chain` and `Version` optionally identify a version series. | [`schema<'model> { ... }`](../dsl/#fields-without-blocks) |
| `[<DeriveUnion "kind">]` | union | Derive an internally tagged union; every case carries one marked record payload. | [`Schema.union`](../union-schemas/#define-cases) |
| `[<SchemaName "wire_name">]` | record field or nullary union case | Override the configured wire name or case tag. | [`fieldAs "wire_name" _.Field`](../dsl/#fields-without-blocks) |
| `[<SchemaConstructor>]` | static member | Call this member with fields in declaration order instead of constructing a record literal. | [`construct`](../dsl/#constructors) |

## Field attributes

| Attribute | Short description | Schema DSL equivalent |
| --- | --- | --- |
| `[<Pattern "expr">]` | Require text to match a regular expression. | [`constrain (pattern "expr")`](../dsl/#constraint-equivalents) |
| `[<Min n>]` | Set the minimum natural length of text, list, or map. | [`constrain (minLength n)`](../dsl/#constraint-equivalents) |
| `[<Max n>]` | Set the maximum natural length of text, list, or map. | [`constrain (maxLength n)`](../dsl/#constraint-equivalents) |
| `[<Length n>]` | Require an exact natural length. | [`constrain (length n)`](../dsl/#constraint-equivalents) |
| `[<LengthBetween(min, max)>]` | Bound natural length inclusively. | [`constrain (lengthBetween min max)`](../dsl/#constraint-equivalents) |
| `[<Present>]` | Require a string, collection, or optional value to be present/non-empty. | [`constrain present`](../dsl/#constraint-equivalents) |
| `[<Supplied>]` | Require the input object to contain this field key. | [`mustSupply`](../dsl/#constraint-equivalents) |
| `[<Format "name">]` | Attach open format metadata without adding a check. | [`format (SchemaFormat.create "name")`](../dsl/#constraint-equivalents) |
| `[<AtLeast n>]` | Inclusive numeric lower bound (`>=`). | [`constrain (atLeast n)`](../dsl/#constraint-equivalents) |
| `[<GreaterThan n>]` | Exclusive numeric lower bound (`>`). | [`constrain (greaterThan n)`](../dsl/#constraint-equivalents) |
| `[<AtMost n>]` | Inclusive numeric upper bound (`<=`). | [`constrain (atMost n)`](../dsl/#constraint-equivalents) |
| `[<LessThan n>]` | Exclusive numeric upper bound (`<`). | [`constrain (lessThan n)`](../dsl/#constraint-equivalents) |
| `[<MultipleOf n>]` | Require a numeric value to be a whole multiple of `n`. | [`constrain (multipleOf n)`](../dsl/#constraint-equivalents) |
| `[<Distinct>]` | Require all list elements to be distinct. | [`constrain distinct`](../dsl/#constraint-equivalents) |
| `[<Email>]` | Apply the built-in email constraint to text. | [`constrain email`](../dsl/#constraint-equivalents) |
| `[<Default value>]` | Supply a value when the input key is omitted; invalid on `option` fields. | [`defaultValue value`](../dsl/#constraint-equivalents) |

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
