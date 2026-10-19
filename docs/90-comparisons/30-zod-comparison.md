---
weight: 30
title: vs zod
description: The same parse-don't-validate philosophy, in F#, with no reflection and Fable support.
targetFramework: net8.0
---

# Reified vs zod

Reified's schema group is the same idea zod made mainstream: declare the shape once, parse unknown input through it, and
get either a typed value or structured, path-aware issues. The differences come from F# and .NET.

## The Same Moves

Table entries on the right are shorthand for the complete declarations shown in
[Schema quickstart](/schema/quickstart.html) and [Union schemas](/schema/union-schemas.html). `maxLength`,
`constrain`, and `present` come from `Reified.ConstraintDSL`; `schema`, `field`, `construct`, and `Schema.union` come
from `Reified.SchemaDSL`.

| zod | Reified |
| --- | --- |
| `z.object({ name: z.string().max(80) })` | `schema<...> { field _.Name { constrain (maxLength 80) }; construct ctor }` |
| `schema.safeParse(input)` | `Schema.parse schema raw` → `Result` |
| `result.error.issues` with paths | `SchemaErrors.toList errors` and `SchemaPath.format issue.Path` |
| `z.string().email().brand<'Email'>()` | `Schema.refine` over a private representation and constraint-backed `Refinement` |
| `z.discriminatedUnion("type", ...)` | `Schema.union [ UnionCase.fields ... ]` |
| `z.infer<typeof schema>` | not needed, the schema is declared against your record type directly |
| `zod-to-json-schema` | `JsonSchema.generate` (in `Reified.Schema`, from the same metadata) |

zod derives the static type from the schema; Reified declares the schema against a type you own. Your domain type stays
an ordinary F# record with real members, and each `field _.Property` declaration names one of those members as its
getter. The computation expression tracks each field's type in declaration order, so the `construct` lambda's parameters
are checked against the getters' types at compile time: a missing, extra, reordered, or mistyped parameter is a compile
error, not a runtime failure.

## What Reified Adds Beyond Parsing

- **Trusted-path serialization**: `Json.compile` turns the same declaration into a compiled JSON codec measured against
  `System.Text.Json` on a representative customer aggregate and a 24-field record: decoding was faster and serialization
  roughly matched in local runs (see [Benchmarks](/notes/benchmarks.html) for the workload and versions). zod validates
  on the way in but does not own the way out.
- **Redisplay**: failed parses keep the structured data, so form fields re-render with the user's values next to their
  errors without extra state.
- **Workflow policies**: environment-aware requirements over already-trusted models, applied per workflow.

## Runtime Differences That Matter

- **No reflection**: Reified schemas are explicit declarations compiled into plans; nothing depends on runtime type
  inspection, so NativeAOT and aggressive trimming work by construction.
- **Fable**: the schema core, including its `Json` codecs, compiles to JavaScript through Fable, so the browser and the
  server can share one declaration, encode and decode included, the role zod plays across the TypeScript stack. See
  [Compiler-Directed, AOT, and Fable](/notes/aot-trimming-fable.html#derived-wire-names-under-fable) for the
  supported targets.
- **Errors are values with rendering**: `SchemaError` is a typed union with a default English renderer
  (`RetainedParseResult.renderErrors`) and a one-function mapping into your own error union (`Result.mapError` over
  `parsed.Result`), rather than a bag of issue objects.

`Schema.parseRetainingInput` produces the retained parse result those one-liners name:

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup = { Email: string; Age: int }

let signupSchema =
    schema<Signup> {
        field _.Email { constrain email }
        field _.Age { constrain (atLeast 13) }
        construct (fun email age -> { Email = email; Age = age })
    }

let parsed =
    Schema.parseRetainingInput signupSchema
        (Data.ofNameValues [ "email", "ada@example.com"; "age", "12" ])

parsed.IsValid                     // false
RetainedParseResult.renderErrors parsed
// [ "age: Expected a value at least 13, but was 12." ]
```

## Where zod Fits Better

zod fits TypeScript-first codebases. For F#/.NET, Reified's schema group covers structured parsing; code that does not need a schema can use plain `Result`.
