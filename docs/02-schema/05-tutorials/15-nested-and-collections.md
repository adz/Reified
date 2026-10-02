---
weight: 15
title: Nested Models And Collections Tutorial
description: Parse an order with a nested address and repeated line items.
targetFramework: net8.0
---

# Nested Models And Collections Tutorial

This tutorial parses an order that contains a nested address and a collection of line items. `Address` and `Item`
own their canonical schemas, so `Order` can infer both the nested field and the list item schema.

## Declare The Schemas

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Address =
    { Street: string; City: string }

    static member Schema(_: Address) : Schema<Address> =
        schema<Address> {
            field _.Street
            field _.City
            construct (fun street city -> { Street = street; City = city })
        }

type Item =
    { Sku: string; Quantity: int }

    static member Schema(_: Item) : Schema<Item> =
        schema<Item> {
            field _.Sku
            field _.Quantity {
                constrain (greaterThan 0)
            }
            construct (fun sku quantity -> { Sku = sku; Quantity = quantity })
        }

type Order =
    { Address: Address
      Items: Item list }

let orderSchema =
    schema<Order> {
        field _.Address
        field _.Items {
            constrain (minLength 1)
        }
        construct (fun address items -> { Address = address; Items = items })
    }
```


## Adapt Nested Input

Configuration-style keys carry nesting with `:` separators and numeric collection indexes; JSON-like input produces
the same `Data` shape:

```fsharp
let raw =
    Data.ofConfiguration
        [ "address:street", "12 Analytical Way"
          "address:city", "London"
          "items:0:sku", "SKU-1"
          "items:0:quantity", "2"
          "items:1:sku", "SKU-2"
          "items:1:quantity", "0" ]
```


## Parse And Read Item Errors

Every item is parsed and every item error accumulates into the same `SchemaErrors`; one bad line item does not hide
the others:

```fsharp
let parsed = Schema.parseRetainingInput orderSchema raw

parsed.ErrorsFor "items[1].quantity"   // quantity 0 fails greaterThan 0
parsed.ErrorsFor "items[0].sku"        // [], the first item is fine
```


Nested diagnostics are prefixed with the field name (`address.city`), collection diagnostics with the item index
(`items[1].quantity`), and the raw values redisplay by the same paths:

```fsharp
Data.redisplayPath "items[1].quantity" parsed.Input   // "0"
```


## Count Constraints

Collection constraints (`minLength`, `maxLength`, `lengthBetween`, `distinct`) attach to the collection field itself
and report on the collection path (`items`), separately from per-item errors.

## When A Nested Field Needs More Than Its Canonical Schema

`Address` and `Item` above resolve through their own `static member Schema`, which is why `Order`'s fields need no
`withSchema`. Reach for a field block with `withSchema` instead when a nested type has no canonical schema of its
own, [Input Sources](/schema/input-sources.html) builds one locally for exactly that case, or when the field is
recursive, [SchemaDSL's recursive schemas](/schema/dsl.html#recursive-schemas) works a `Schema.defer` example
through in full.

## Next

- [Input Sources](/schema/input-sources.html) for the full adapter catalog.
- [SchemaDSL](/schema/dsl.html#recursive-schemas) for `withSchema` and recursive composition with `Schema.defer`.
