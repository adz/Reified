---
weight: 12
title: Refined fields in Schema
description: Use invariant-carrying refined types as schema fields, and let interpreters read their rules.
targetFramework: net8.0
---

# Refined fields in Schema

A refined type moves a field-local rule into the model, where it holds no matter how the value was built. Schema
resolves a refined field type the same way it resolves primitives: every refined type has exactly one canonical schema.
This page shows the result; [Refined Schemas](/schema/refined-values.html) is the deeper reference for defining your
own domain types and contributing their canonical schemas.

`Signup.Email` as a plain `string` leaves nothing stopping `{ Email = ""; Age = 36; Newsletter = true }`.

[Reified.Refinements](/refined/index.html) ships the common refined types. Use them as field types directly:

```fsharp
open Reified.Refinements

type Registration =
    { Owner: NonBlankString
      Seats: int
      Aliases: NonEmptyList<NonBlankString> }

let registrationSchema =
    schema<Registration> {
        field _.Owner
        field _.Seats
        field _.Aliases
        construct (fun owner seats aliases ->
            { Owner = owner; Seats = seats; Aliases = aliases })
    }
```


The fields stay bare. Every refined type has exactly one schema, so the field resolves it from the type the same way
`string` resolved `Schema.text`. Rules that need a parameter, a length range, a pattern, are constraints rather
than types, and go on the field as their own line: `field _.Name { constrain (Constraint.lengthBetween 2 80) }`.

`NonEmptyList<NonBlankString>` composes: the outer refinement resolves, and so does the item.

## It flows through

A refinement carries its constraints, so the interpreters see them:

```fsharp
JsonSchema.generate registrationSchema
// "owner":   {"type":"string","pattern":"\\S"}
// "seats":   {"type":"integer"}
// "aliases": {"type":"array","items":{"type":"string"},"minItems":1}
```


```fsharp
Inspect.model registrationSchema
// owner   -> [ "present" ]
// seats   -> [ ]
// aliases -> [ "minLength" ]
```


Failures arrive on the right paths:

```text
owner: This value must be present.
aliases: Length must be at least 1; got 0.
```


The generated JSON Schema, inspection metadata, and parse diagnostics all read the refinement definition. Each check runs
once, at the layer that owns it.

Your own domain types participate the same way. See [Refined Schemas](/schema/refined-values.html) for defining `Email`
or `WorkspaceName` and contributing a canonical schema.
