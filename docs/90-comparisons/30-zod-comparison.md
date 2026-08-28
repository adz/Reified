---
weight: 30
title: vs zod
description: The same parse-don't-validate philosophy, in F#, with no reflection and Fable support.
targetFramework: net8.0
---

# Reified vs zod

This page maps zod's model onto Reified for readers who know "parse, don't validate" from TypeScript.

Reified's schema group is the same idea zod made mainstream: declare the shape once, parse unknown input through it, and
get either a typed value or structured, path-aware issues. If you like zod, Reified should feel familiar — the
differences come from F# and .NET, not from a different philosophy.

## The Same Moves

| zod | Reified |
| --- | --- |
| `z.object({ name: z.string().max(80) })` | `schema<...> { field _.Name { constrain (maxLength 80) }; construct ctor }` |
| `schema.safeParse(input)` | `Schema.parse schema raw` → `Result` |
| `result.error.issues` with paths | `SchemaErrors.toList errors` and `SchemaPath.format issue.Path` |
| `z.string().email().brand<'Email'>()` | `Schema.refine` over a private representation and constraint-backed `Refinement` |
| `z.discriminatedUnion("type", ...)` | `Schema.union [ UnionCase.fields ... ]` |
| `z.infer<typeof schema>` | not needed — the schema is declared against your record type directly |
| `zod-to-json-schema` | `JsonSchema.generate` (in `Reified.Schema`, from the same metadata) |

One inversion worth noticing: zod derives the static type from the schema; Reified declares the schema against a type
you own. Your domain type stays an ordinary F# record with real members, and the compiler checks constructor/getter
alignment field by field.

## What Reified Adds Beyond Parsing

- **Trusted-path serialization**: `Json.compile` turns the same declaration into a compiled JSON codec on par with
  `System.Text.Json` — zod validates on the way in but does not own the way out.
- **Redisplay**: failed parses keep the structured data, so form fields re-render with the user's values next to their
  errors without extra state.
- **Workflow policies**: environment-aware requirements over already-trusted models, applied per workflow.

## Runtime Differences That Matter

- **No reflection**: Reified schemas are explicit declarations compiled into plans; nothing depends on runtime type
  inspection, so NativeAOT and aggressive trimming work by construction.
- **Fable**: the schema core, including its `Json` codecs, compiles to JavaScript through Fable, so the browser and the
  server can share one declaration — encode and decode included — the role zod plays across the TypeScript stack.
- **Errors are values with rendering**: `SchemaError` is a typed union with a default English renderer
  (`RetainedParseResult.renderErrors`) and a one-function mapping into your own error union (`RetainedParseResult.mapErrors`), rather
  than a bag of issue objects.

## Where zod Fits Better

zod is the right tool in TypeScript-first codebases — Reified is not trying to run there. The comparison matters when an
F#/.NET team asks "what is our zod?": the answer is the schema group of Reified, with plain `Result` for code that never
needed a schema at all.
