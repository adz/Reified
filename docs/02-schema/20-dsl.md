---
weight: 20
title: SchemaDSL
description: Record fields, field blocks, canonical schemas, and checked constructors.
targetFramework: net8.0
---

# SchemaDSL

Reified provides a DSL for more concise schema declarations:

```fsharp
open Reified
open Reified.SchemaDSL
```


A record schema is one constructor-last computation expression:

```fsharp
type Signup =
    { Email: string
      Age: int }

    static member create email age = { Email = email; Age = age }

schema<Signup> {
    field _.Email
    field _.Age
    construct Signup.create
}
```


## Fields without blocks

`field` takes the getter and nothing else. The getter fixes the field type, Schema resolves that type's canonical
schema, and the wire name is the property name, camelCased:

```fsharp no-check reason="Illustrates field declaration lines shown outside their enclosing schema<T> block."
field _.Name        // wire name "name"
field _.Age         // wire name "age"
field _.Tags        // wire name "tags"
```


This works for built-in primitives and composites, built-in refined values, and application types that contribute a
static `Schema` member.

## Naming a field explicitly

`fieldAs` sets the wire name when it is not the camelCased property name. Explicit names are never transformed:

```fsharp no-check reason="Illustrates field declaration lines shown outside their enclosing schema<T> block."
fieldAs "email_address" _.Email
fieldAs "type" _.Number
```


`field` derives its name by reading a quotation of the getter, once, while the schema value is built. That runs on
.NET and on the Fable targets with quotation support, so both forms are available almost everywhere, see
[Compiler-Directed, AOT, and Fable](/notes/aot-trimming-fable.html) for the version and target requirements. `fieldAs` is
the portable spelling for Fable's Rust and PHP targets, which have no quotation support.

## Accepting old field names

Add input-only aliases when a boundary must temporarily accept an earlier spelling or casing:

```fsharp
open Reified
open Reified.SchemaDSL

type Profile = { DisplayName: string }

let profileSchema =
    schema<Profile> {
        fieldAs "displayName" _.DisplayName {
            alias "DisplayName"
            aliases [ "display_name"; "display-name" ]
        }
        construct (fun displayName -> { DisplayName = displayName })
    }
```

`Schema.parse` and compiled JSON codecs accept the canonical name or any alias. Encoding, JSON Schema, diagnostic paths
for missing fields, and other output interpreters continue to use the canonical `displayName` name. Names are exact and
case-sensitive; list each accepted case explicitly.

Supplying more than one accepted name for the same field is an error. Schema construction also rejects aliases that
repeat that field's canonical name or collide with another field name or alias. Use
[Versioned Contracts](/schema/versioned-contracts.html) instead when a rename represents a durable versioned wire
format rather than a short compatibility window.

## Field blocks

A block groups transformations for one field:

```fsharp no-check reason="Shows the field block's typed pipeline shape against an application-owned ContactEmail refinement and validator; see Refined Schemas for a working refine+validate example."
field _.Email {
    withSchema Schema.text
    constrain present
    refine
    validate validateCompanyEmail
}
```


A field block is a typed pipeline. It starts at whatever came off the wire and has to end at the type the getter
returns:

```text
Data field -> Schema<string> -> raw constraints -> Schema<ContactEmail> -> domain validation
```


Every operation either **preserves** the current type or **changes** it, and there is exactly one that changes it:

| Operation | Effect on the current type |
| --- | --- |
| `withSchema` | Sets the starting type. |
| `constrain`, `constraints` | Preserve it. |
| `describe`, `format`, `defaultValue` | Preserve it. |
| `refine` | **Changes** it, from the raw type to the getter's type. |
| `validate` | Preserves it. |

Operations run from top to bottom:

1. `withSchema` sets the current raw schema. Without it, the field's type resolves the schema.
2. `constrain` adds one constraint; `constraints` adds a list in declaration order. Both preserve the value type.
3. `describe`, `format`, and `defaultValue` add type-preserving metadata. They can use the inferred schema or follow
   `withSchema`; `defaultValue` also supplies the field when input omits it.
4. `refine` changes the current schema from its raw type to the getter type.
5. `validate` runs executable value-preserving logic over the current type.

Raw constraints have to come before `refine`, because after it the current type is `ContactEmail` and `maxLength` is
not a rule about a `ContactEmail`, it is a rule about the text that was admitted. Put text rules above the line and
domain rules below it.

The getter fixes where the pipeline must end. `field _.Email` on a `ContactEmail` member means the block has to arrive
at `Schema<ContactEmail>`, so a block that starts at `Schema.text` and never refines will not compile.

A plain `int` field needs no refinement, because it starts and ends at the same type:

```fsharp
open Reified.ConstraintDSL

type WithAge = { Age: int }

schema<WithAge> {
    field _.Age {
        withSchema Schema.int
        constrain (atLeast 18)
    }
    construct (fun age -> { Age = age })
}
```


Group adjacent rules with `constraints`:

```fsharp
open Reified.ConstraintDSL

type WithGroupedEmail = { Email: string }

schema<WithGroupedEmail> {
    field _.Email {
        constraints [ present; email; maxLength 254 ]
    }
    construct (fun email -> { Email = email })
}
```


The typed vocabulary in `Reified.SchemaDSL` covers every [interpreted](/constraints/constraints.html)
constraint, the built-ins Reified can read as data and lower to JSON Schema. The field type checks every
entry, so `email` cannot be applied to an `int` field. Lifted constraints such as `minLength` apply to strings,
lists, arrays, and maps with shape-appropriate interpretation.

## Constraint equivalents

These are the handwritten operations emitted for derivation attributes. Use them inside a field block with
`constrain`, except for the metadata operations shown directly:

| Purpose | Schema DSL |
| --- | --- |
| Pattern | `constrain (pattern expression)` |
| Minimum, maximum, exact, or bounded natural length | `constrain (minLength n)`, `maxLength`, `length`, `lengthBetween` |
| Present value or supplied input key | `constrain present`, `mustSupply` |
| Inclusive/exclusive numeric bounds | `constrain (atLeast n)`, `greaterThan`, `atMost`, `lessThan` |
| Numeric multiple | `constrain (multipleOf n)` |
| Distinct list elements | `constrain distinct` |
| Email text | `constrain email` |
| Open format metadata | `format (SchemaFormat.create name)` |
| Omitted-input default | `defaultValue value` |

See [Derivation Attributes](/schema/derivation/attributes.html) for the complete attribute mapping.

## Refinement changes the type

```fsharp no-check reason="Shows the field block's typed pipeline shape against an application-owned ContactEmail refinement and validator; see Refined Schemas for a working refine+validate example."
field _.Email {
    withSchema Schema.text
    constrain present                   // operates on string
    refine                              // string -> ContactEmail
    validate validateCompanyEmail       // operates on ContactEmail
}
```


The parameterless operation resolves `Refinement<string,ContactEmail>` at compile time. A missing contribution is a
compile error; Schema does not use reflection or a runtime registry.

[Refined value schemas](/schema/refined-values.html) works this through end to end, including writing the refinement itself.

## Constructors

`construct` accepts a total constructor:

```fsharp
type WithEmailAge = { Email: string; Age: int }

schema<WithEmailAge> {
    field _.Email
    field _.Age
    construct (fun email age -> { Email = email; Age = age })
}
```


`constructResult` accepts cross-field construction that can fail:

```fsharp
type CheckedSignup = { Email: string; Age: int }

module CheckedSignup =
    let createChecked email age =
        if age >= 13 then Ok { Email = email; Age = age }
        else Error "Age must be at least 13."

schema<CheckedSignup> {
    field _.Email
    field _.Age
    constructResult CheckedSignup.createChecked
}
```


All independent fields must succeed before either constructor runs. A `constructResult` failure attaches to the current
object path.

The field chain is recursive and has no fixed arity limit.

## Recursive schemas

Use `Schema.defer` where a field refers back to the schema being defined:

```fsharp
type Category =
    { Name: string
      Children: Category list }

    static member create name children = { Name = name; Children = children }

let rec categorySchema () =
    schema<Category> {
        field _.Name
        field _.Children {
            withSchema (Schema.listWith (Schema.defer categorySchema))
        }
        construct Category.create
    }
```


`Schema.defer` takes a thunk, `unit -> Schema<'model>`, evaluated at most once, not a `Lazy` value: `categorySchema`
is declared as a zero-argument function specifically so `Schema.defer categorySchema` can pass it directly.
