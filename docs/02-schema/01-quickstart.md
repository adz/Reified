---
weight: 10
title: Quickstart
description: Declare one record schema, parse untrusted input into it, and compile the same declaration into a JSON codec.
targetFramework: net8.0
---

# Quickstart

A `Schema<'model>` is one declaration of a structured boundary. It names fields, gives each a value schema, runs
constraints and refinements, accumulates failures with paths, and calls your constructor only once the fields succeed.

The same declaration drives several interpreters, so parsing, JSON serialization, JSON Schema generation, and metadata
inspection stay in step with no second source of truth.

```bash
dotnet add package Reified.Schema
```


```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL
```


## First schema and parse

A schema can consist of primitives alone:

```fsharp
type Signup =
    { Email: string
      Age: int
      Newsletter: bool }

let signupSchema =
    schema<Signup> {
        field _.Email
        field _.Age
        field _.Newsletter
        construct (fun email age newsletter ->
            { Email = email; Age = age; Newsletter = newsletter })
    }
```


`field _.Email` gives the getter and nothing more. The field's value schema comes from its type, `string` resolves
`Schema.text`, `int` resolves `Schema.int`, `bool` resolves `Schema.bool`, and the wire name is derived by lowercasing
only the property name's first character with `Char.ToLowerInvariant`, leaving the rest unchanged: `Email` becomes
`email`, but an acronym such as `URLValue` derives `uRLValue`, not `urlValue`. Use `fieldAs` when that default is not
the wire name you want, including for Unicode identifiers whose first character has no single-character lowercase
mapping. `construct` receives the fields in declaration order; the compiler checks its argument types and its
result.

Pass a wire name explicitly when it differs from the property: `fieldAs "email_address" _.Email`. To accept an old
input spelling while always writing the canonical name, use a field block with `alias "Email"` or
`aliases [ "Email"; "emailAddress" ]`.

Deriving the name from `_.Email` uses a quotation. That compiles on .NET and on the Fable targets that support
quotations, including JavaScript; reach for `fieldAs` on Fable's Rust and PHP targets. See
[SchemaDSL](dsl.html) and [Compiler-Directed, AOT, and Fable](/notes/aot-trimming-fable.html).

### Parse

`Data` is a source-neutral input tree. The same schema reads form posts, CLI arguments, JSON, and configuration,
see [Input Sources](/schema/input-sources.html).

```fsharp
let input =
    Data.ofNameValues [
        "email", "ada@example.org"
        "age", "36"
        "newsletter", "true"
    ]

Schema.parse signupSchema input
// Ok { Email = "ada@example.org"; Age = 36; Newsletter = true }
```


`"36"` arrives as text and lands as `int`. Decoding a primitive from its serialized form is part of the field's value
schema.

No `Signup` is produced unless every field succeeds and the constructor succeeds.

### Read the failures

Independent fields are all interpreted, so one parse reports every problem rather than the first:

```fsharp
match Schema.parse signupSchema input with
| Ok signup -> printfn "%A" signup
| Error errors ->
    for issue in SchemaErrors.toList errors do
        printfn "%s: %s" (SchemaPath.format issue.Path) (SchemaError.render issue.Error)
```


Input missing `newsletter` and carrying `"age": "not-a-number"`:

```text
age: Expected int format.
newsletter: This value was omitted.
```


Paths come from the structure of the declaration. Application code never repeats field names, nested object names, list
indexes, or map keys alongside separate validation expressions. For nesting and collections, see
[Nested Models And Collections](/schema/tutorials/nested-and-collections.html).

### Serialize

The `Json` module compiles the same declaration into a JSON codec. It uses no runtime reflection, so it works under
NativeAOT, trimming, and Fable.

```fsharp
let codec = Json.compile signupSchema

let json = Json.serialize codec { Email = "ada@example.org"; Age = 36; Newsletter = true }
// {"email":"ada@example.org","age":36,"newsletter":true}

Json.deserialize codec json
```


Compile the codec once and reuse it. `Json.compile` does the work up front; `serialize` and `deserialize` are the hot
path. The compiled codec keeps no per-value state, so one codec is safe to share across threads: each
`serialize`/`deserialize` call uses its own pooled buffer and its own per-decode field slots. Compilation is a pure
walk over the built (immutable) schema, so it is also safe to run once at startup or concurrently with other
compilations. See [JSON Codecs](/schema/json-codecs.html) for buffers, streams, and decode diagnostics.

### The other interpreters

The same `signupSchema` also drives:

```fsharp
JsonSchema.generate signupSchema
// {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object",
//  "properties":{"email":{"type":"string"},"age":{"type":"integer"},
//                "newsletter":{"type":"boolean"}},
//  "required":["email","age","newsletter"]}

Inspect.model signupSchema
// finite metadata: field names, shapes, constraints, no execution
```


| Input | Interpreter | Result |
| --- | --- | --- |
| `Data` | `Schema.parse` | model or `SchemaErrors` |
| an existing typed value | `Schema.check` | the same value or `SchemaErrors` |
| schema | `Json.compile` | reusable JSON codec |
| schema | `JsonSchema.generate` | JSON Schema document |
| schema | `Inspect.model` | metadata without execution |
| versioned `Data` | `Contract.parse` | current model or `ContractError` |

`Schema.check` is for values that did not arrive as `Data`, a record literal, a database mapper's output, an import.
It runs the same field rules and calls the same constructor:

```fsharp
let existingValue = { Email = "ada@example.org"; Age = 36; Newsletter = true }
Schema.check signupSchema existingValue
```


`Inspect.model` is what forms, admin UIs, and documentation generators read.

## Where to go next

Choose the least restrictive representation that prevents the problem:

1. Plain fields, the schema is the admission decision.
2. Refined fields, field-local invariants hold everywhere, and the record stays public with `with` intact.
3. Field constraints, boundary-specific rules, visible where they apply.
4. Private model and draft, relationships between fields.

- [Refined fields in Schema](/schema/refined-fields.html), refined types as fields and how interpreters read them.
- [Boundary-specific field rules](/schema/boundary-field-rules.html), field blocks and boundary constraints.
- [Construction Guarantees](/schema/trusted-construction.html), what each rung does and does not promise.
- [SchemaDSL](dsl.html), the full declaration vocabulary.
- [Derived Schemas](derivation/index.html), generate that declaration from an attributed F# wire record.
- [Field Blocks and Plain Functions](/schema/field-desugaring.html), a field block read as ordinary functions over one `Schema`.
- [Refined Schemas](/schema/refined-values.html), your own domain types as fields.
- [Tutorials](tutorials/index.html), a signup form, nested models and collections, and metadata inspection.
- [Redisplay And Field Errors](/schema/redisplay-and-field-errors.html), failed parses that keep the user's input.
- [Versioned Contracts](/schema/versioned-contracts.html), evolving the wire format without freezing the domain model.
