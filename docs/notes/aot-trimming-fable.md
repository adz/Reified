---
weight: 82
title: Compiler-Directed, AOT, and Fable
description: Why Reified works under NativeAOT, aggressive trimming, and Fable by construction.
---

# Compiler-Directed: AOT and Fable

This page explains Reified's runtime-portability guarantees and what makes them hold.

Reified performs no runtime reflection in any hot path — everything is compiler-directed, for maximal deterministic
verification. That is not an optimization applied afterwards; it is an architectural rule: schemas, constructors,
getters, checks, codecs, and service access are all explicit declarations the compiler can see, so there is nothing
for the trimmer to remove by mistake and nothing NativeAOT cannot compile ahead of time. Where build-phase metadata
reading exists (the bare `field _.Name` form reads a property name from the getter expression once, when the schema
value is built), it runs during schema construction, never per parsed or encoded value — and the AOT probe executes it
natively to prove it.

## Why It Holds By Construction

- **Schemas declare construction explicitly.** `schema<Customer> { field ...; construct ctor }`
  captures the real constructor and typed getters as values. There is no property discovery, no attribute scanning,
  and no `Activator.CreateInstance`.
- **Codecs compile from the typed shape.** `Json.compile` turns the schema's retained typed constructor and fields
  into encode/decode plans — cached wire-name bytes and typed field decoders — where `System.Text.Json`'s default path
  builds converters through reflection and asks you to switch to source generators for AOT. Reified has nothing to
  switch: the explicit path is the only path.
- **Refined values are functions, not conventions.** `Schema.convert construct inspect` carries the conversion in both
  directions as ordinary closures.
- **Services are explicit.** Dependencies live in `'env` records or nominal `IHas<'service>` contracts; there is no
  runtime service map or proxy generation.

The one deliberate exception: `Service<'service>.resolve()` can look up registrations from an
`IServiceProvider` at .NET host edges. That is host integration you opt into at the boundary, not a core mechanism.

## Verified In CI

Every push publishes and runs a NativeAOT probe (`bash scripts/run-aot-probe.sh`), which compiles an application
exercising flows, schemas, parsing, and services with `PublishAot=true` and executes the native binary. If a change
introduced reflection the trimmer could not prove safe, CI fails.

## Fable

The same explicitness is what makes Fable compilation work: `Reified.Result`, `Reified.Constraint`, `Reified.Refinements`, and
`Reified.Schema` all compile to JavaScript, so a browser front end can parse
and redisplay through the same schema declaration the server uses. `Reified.Schema.Json` compiles too, so a
codec is available on both sides of the wire. CI compiles the Fable JavaScript surface and runs it on Node
(`bash scripts/check-fable-js-surface.sh`), asserting the same results the same checks produce on .NET —
constraint behaviour, operand descriptions, localized rendering, a codec round-trip, and JSON boundaries.
.NET-only conveniences — such as `Data.ofJsonDocument` and the
`DateOnly` field type — are compile-time gated so the Fable surface never references them.

### Derived wire names under Fable

The `field _.Email` form reads a quotation of the getter to recover the property name. It works on .NET and on the
Fable targets that support quotations:

| Fable target | Derived `field _.Email` | Minimum Fable |
| --- | --- | --- |
| JavaScript, TypeScript, Python, BEAM | Yes | 5.10 |
| Dart | Yes | 5.13 |
| Rust, PHP | No — use `fieldAs` | — |

No compiler flag or define is needed, and a schema means the same thing on every target:

```fsharp
// Derives the wire name "email" from the property
field _.Email

// Declares it, and works everywhere including Rust and PHP
fieldAs "email_address" _.Email
```

Reading the name happens **once, while the schema value is built** — never per parsed or encoded value, so the
guarantee at the top of this page holds on both targets. The Fable probe declares its schema with `field _.Name`
and asserts the derived wire names from the compiled JavaScript, so this stays proven rather than claimed.

## What This Buys You

- `PublishAot=true` and `PublishTrimmed=true` work without `DynamicDependency` annotations, trimmer XML, or source
  generators.
- Startup does not pay for converter caches or expression-tree compilation; codecs and parsers are compiled once from
  explicit declarations.
- The same domain model and boundary declaration can serve .NET services, native binaries, and Fable clients.
