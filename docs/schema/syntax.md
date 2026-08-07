---
weight: 20
title: Schema Syntax
description: Record fields, field blocks, canonical schemas, and checked constructors.
---

# Schema Syntax

Every Reified package puts its concise vocabulary behind one opt-in module named `Syntax`, so the preamble is always
the package namespace, then its `Syntax`. Nothing is auto-opened; if a name below is not in scope, the second line
is missing.

```fsharp
open Reified.Schema
open Reified.Schema.Syntax
```

The same shape applies to `Reified.Data.Syntax`, `Reified.Constraint.Syntax`, and `Reified.Result.Syntax` — two
lines, every package.

A record schema is one constructor-last computation expression:

```fsharp
schema<Signup> {
    field _.Email
    field _.Age
    construct Signup.create
}
```

`field` and `construct` use implicit yield. Do not write `yield`.

## Fields without blocks

`field` takes the getter and nothing else. The getter fixes the field type, Schema resolves that type's canonical
schema, and the wire name is the property name, camelCased:

```fsharp
field _.Name        // wire name "name"
field _.Age         // wire name "age"
field _.Tags        // wire name "tags"
```

This works for built-in primitives and composites, built-in refined values, and application types that contribute a
static `Schema` member.

## Naming a field explicitly

`fieldAs` sets the wire name when it is not the camelCased property name. Explicit names are never transformed:

```fsharp
fieldAs "email_address" _.Email
fieldAs "type" _.Number
```

`field` derives its name by reading a quotation of the getter, once, while the schema value is built. That runs on
.NET and on the Fable targets with quotation support, so both forms are available almost everywhere — see
[Compiler-Directed, AOT, and Fable](../aot-trimming-fable/) for the version and target requirements. `fieldAs` is
the portable spelling for Fable's Rust and PHP targets, which have no quotation support.

## Field blocks

A block groups transformations for one field:

```fsharp
field _.Email {
    withSchema Schema.text
    constrain present
    refine
    validate validateCompanyEmail
}
```

Operations run from top to bottom:

1. `withSchema` sets the current raw schema.
2. `constrain` adds one portable constraint; `constraints` adds a list in declaration order. Both preserve the value type.
3. `describe`, `format`, and `defaultValue` add type-preserving metadata. They can use the inferred schema or follow
   `withSchema`; `defaultValue` also supplies the field when input omits it.
3. `refine` changes the current schema from its raw type to the getter type.
4. `validate` runs executable value-preserving logic over the current type.

The block must finish with the getter type. A plain `int` field does not need refinement:

```fsharp
field _.Age {
    withSchema Schema.int
    constrain (atLeast 18)
}
```

Group adjacent rules with `constraints`:

```fsharp
field _.Email {
    constraints [ present; email; maxLength 254 ]
}
```

The typed vocabulary in `Reified.Schema.Syntax` covers every portable schema constraint. The field type checks every
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

See [Derivation Attributes](../derivation/attributes/) for the complete attribute mapping.

## Refinement changes the stage

```fsharp
field _.Email {
    withSchema Schema.text
    constrain present       // operates on string
    refine                              // string -> ContactEmail
    validate validateCompanyEmail       // operates on ContactEmail
}
```

The parameterless operation resolves `Refinement<string,ContactEmail>` at compile time. A missing contribution is a
compile error; Schema does not use reflection or a runtime registry.

## Constructors

`construct` accepts a total constructor:

```fsharp
construct (fun email age -> { Email = email; Age = age })
```

`constructResult` accepts cross-field construction that can fail:

```fsharp
constructResult Signup.createChecked
```

All independent fields must succeed before either constructor runs. A `constructResult` failure attaches to the current
object path.

The field chain is recursive and has no fixed arity limit.

## Recursive schemas

Use `Schema.defer` where a field refers back to the schema being defined:

```fsharp
let rec schema : Lazy<Schema<Category>> =
    lazy (
        Syntax.schema<Category> {
            field _.Name
            field _.Children {
                withSchema (Schema.listWith (Schema.defer schema))
            }
            construct Category.create
        })
```

Only the opening builder is qualified here because the binding named `schema` shadows the unqualified builder.
Ordinary declarations use unqualified `schema`, `field`, and `construct`.
