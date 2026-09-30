---
weight: 25
title: Field Blocks and Plain Functions
description: Read a field block as ordinary transformations over one Schema value.
targetFramework: net8.0
---

# Field Blocks and Plain Functions

The inner field block is syntax for transforming one `Schema<_>` value. It prevents configuration for adjacent fields
from joining into one pipeline.

Both forms below use a `ContactEmail` type and a `validateCompanyEmail` function shaped like the ones
[Refined Schemas](/schema/refined-values.html) builds section by section, kept minimal here (one constraint, not
the full presence-plus-length-plus-format version that page settles on) since this page's subject is the field
block, not the refinement. `Refinement.define` pairs a constraint with the wrap/unwrap functions that make it a
type, covered there in detail:

```fsharp
open Reified
open Reified.Refinements

type ContactEmail =
    private
    | ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.define Constraint.email ContactEmail value

let validateCompanyEmail (email: ContactEmail) : Result<unit, SchemaError> =
    if (ContactEmail.value email).EndsWith("@example.com") then Ok ()
    else Error (SchemaError.Custom("company-email", Some "Company email must end with @example.com."))
```

```fsharp no-check reason="A field block is only valid inside an enclosing schema<T> { } declaration; shown here for comparison with the verified plain-function pipeline below, not as a standalone unit."
field _.Email {
    withSchema Schema.text
    refine ContactEmail.refinement
    validate validateCompanyEmail
}
```


Its schema transformation is:

```fsharp
Schema.text
|> Schema.refine ContactEmail.refinement
|> Schema.validate validateCompanyEmail
```


The outer declaration then attaches that `Schema<ContactEmail>` to the `"email"` field and its getter.

## `withSchema`

`withSchema` replaces canonical type resolution for this field:

```fsharp no-check reason="A field block is only valid inside an enclosing schema<T> { } declaration; not a standalone compilation unit."
field _.Children {
    withSchema (Schema.listWith childSchema)
}
```


There is no separate `fieldWith` declaration. Explicit schema selection is always an operation inside the field.

### Canonical resolution for generated contract types

A type resolves canonically when it exposes `static member Schema: T -> Schema<T>`. Reified supplies that member for
its built-in types, and `reified schemagen` emits it for every record it owns (the types declared in a `.contract`
file). A field whose type is such a contract, directly, or wrapped in `list`, `option`, or `Map`, therefore needs
no `withSchema`:

```fsharp no-check reason="Illustrates field declaration lines shown outside their enclosing schema<T> block."
field _.Origin        // Origin: Geo            - Geo is a generated contract type
field _.Waypoints     // Waypoints: Geo list
field _.Destination   // Destination: Geo option
```

The augmentation is intrinsic to the generated file, so it resolves from any assembly with no `open`. Types carried
by `[<DeriveSchema>]` on hand-written records are the exception: schemagen cannot add an intrinsic member to a type
it does not declare, so a field of such a type still selects its schema with `withSchema TheType.schema`.

## `constrain` and `constraints`

Portable constraints can be inspected by JSON Schema, documentation, and UI interpreters. `constraints` takes a
list for several at once; `constrain` takes one:

```fsharp no-check reason="A field block is only valid inside an enclosing schema<T> { } declaration; not a standalone compilation unit."
field _.Name {
    constraints [ present; maxLength 80 ]
}
```


The plain function is `Schema.constrain`:

```fsharp
Schema.text
|> Schema.constrain Constraint.present
|> Schema.constrain (Constraint.maxLength 80)
```

## `refine`

The plain function receives a descriptor explicitly:

```fsharp
let contactEmailSchema =
    Schema.text
    |> Schema.refine ContactEmail.refinement
```


Inside the field block, either supply the same value or use the destination type's canonical contribution:

```fsharp no-check reason="A field block is only valid inside an enclosing schema<T> { } declaration; not a standalone compilation unit."
field _.Email {
    withSchema Schema.text
    refine ContactEmail.refinement
}
```


```fsharp
type ContactEmail with
    static member Refinement(_: string, _: ContactEmail) = ContactEmail.refinement
```

```fsharp no-check reason="A field block is only valid inside an enclosing schema<T> { } declaration; not a standalone compilation unit."
field _.Email {
    withSchema Schema.text
    refine
}
```


The bare form uses the current `string` schema and the `ContactEmail` getter type as its compile-time dispatch key.

## `validate`

Executable validation preserves the current type:

```fsharp
let companyEmailSchema =
    contactEmailSchema
    |> Schema.validate validateCompanyEmail
```


Inside the block:

```fsharp no-check reason="A field block is only valid inside an enclosing schema<T> { } declaration; not a standalone compilation unit."
field _.Email {
    validate validateCompanyEmail
}
```


Schema attaches the field path if the function fails. Arbitrary executable validation is not emitted as JSON Schema
metadata; use a built-in constraint for facts other interpreters must read.
