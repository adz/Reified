---
weight: 18
title: DataDSL
type: docs
description: The complete Reified.DataDSL vocabulary, its explicit equivalents, and a full example without opening it.
targetFramework: net8.0
---

# DataDSL

`Reified.DataDSL` provides short names and the `=>` and `?=>` operators for data construction and editing.

## Without opening Reified.DataDSL

This example declares nested data, omits an optional field, applies an atomic patch, and renders the result without
opening `Reified.DataDSL`:

```fsharp
open Reified

let nickname : string option = None

let customer =
    Data.data [
        Data.assoc "name" "Ada"
        Data.optionalAssoc "nickname" nickname
        Data.assoc "deletedAt" Data.Null
        Data.assoc "address" (
            Data.data [
                Data.assoc "city" "Adelaide"
                Data.assoc "postcode" 5000
            ])
        Data.assoc "roles" [ "author" ]
    ]

let request =
    customer
    |> Data.patch [
        DataEdit.replace "address.postcode" 5001
        DataEdit.append "roles" "admin"
        DataEdit.remove "deletedAt"
    ]

Data.render request
// => "{ name: \"Ada\", address: { city: \"Adelaide\", postcode: 5001 }, roles: [\"author\", \"admin\"] }"
```


For a single edit, apply the direct `Data` operation instead:

```fsharp
customer |> Data.replace "name" "Grace"
```


## The same example with Reified.DataDSL

```fsharp
open Reified
open Reified.DataDSL

let customerViaDsl =
    data [
        "name" => "Ada"
        "nickname" ?=> nickname
        "deletedAt" => nil
        "address" => [ "city" => "Adelaide"; "postcode" => 5000 ]
        "roles" => [ "author" ]
    ]

let requestViaDsl =
    customerViaDsl
    |> Data.patch [
        replace "address.postcode" 5001
        append "roles" "admin"
        remove "deletedAt"
    ]
```


## Literal and edit mappings

Every DSL name maps to an explicit `Reified.Data` or `DataEdit` function; the complete `DataDSL` ↔ explicit API table,
the matching names, and the case-generation names are in the [`DataDSL` API reference](/api/Reified.DataDSL.html).

`set` makes the final path contain the value, adding a missing final object field when necessary. `replace` requires
the target to exist, so a misspelled or unexpectedly absent path fails. `Data.tryMatch` is always qualified because it
is not part of the DSL.
