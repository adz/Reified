---
weight: 82
title: Compiler-Directed, AOT, and Fable
description: How Reified supports NativeAOT, aggressive trimming, and Fable.
targetFramework: net8.0
---

# Compiler-Directed: AOT and Fable

Reified performs no runtime reflection in hot paths. Schemas, constructors, getters, checks, codecs, and service
access use explicit declarations the compiler can see, allowing trimming and ahead-of-time compilation without
runtime discovery. The bare `field _.Name` form reads a property name from the
getter expression once during schema construction, never per parsed or encoded value. The AOT probe executes this
path natively.

## Why It Holds By Construction

- **Schemas declare construction explicitly.** `schema<Customer> { field ...; construct ctor }`
  captures the real constructor and typed getters as values. There is no property discovery, no attribute scanning,
  and no `Activator.CreateInstance`.
- **Codecs compile from the typed shape.** `Json.compile` turns the schema's retained typed constructor and fields
  into encode/decode plans, cached wire-name bytes and typed field decoders. `System.Text.Json`'s default path
  builds converters through reflection and requires source generators for AOT. Reified uses the explicit path.
- **Refined values are functions, not conventions.** `Schema.convert construct inspect` carries the conversion in both
  directions as ordinary closures.
- **Services are explicit.** Reified has no service-resolution API. Dependencies live in your own `'env` records;
  resolving them from a container is application-owned code at the host edge (see
  [Keep service-provider lookup at the host](/schema/patterns/project-structure.html#keep-service-provider-lookup-at-the-host)),
  not a Reified mechanism.

## Verified In CI

Pushes to `main` and pull requests publish and run the Result, Constraint, Refinements, and Schema NativeAOT probes
(`dotnet run --project tools/Reified.Build -- --target NativeAot`). Each probe is published for `linux-x64` and its
native binary is executed. Reified's library assemblies are compiled with the F# compiler's `--reflectionfree`
option, which omits reflection-backed generated structural formatting and rejects `%A` printf formats in those
assemblies.

A consumer does not inherit compiler flags from a NuGet dependency. To apply the same check to an application, add:

```xml
<PropertyGroup>
  <OtherFlags>$(OtherFlags) --reflectionfree</OtherFlags>
</PropertyGroup>
```

With that option, give application records and unions an explicit `ToString` only when their text is part of the
application contract, and replace `%A` formatting with a domain formatter. Reified's NativeAOT probes contain no
printf-family calls and treat IL2104 and IL3053 like every other warning: a summary warning from `FSharp.Core` fails
the publish. The Schema probe currently exposes such summaries. Expanded diagnostics identify FSharp quotation
reflection used by field-name extraction and FSharp printf retained by `JsonSchema`'s string assembly; those paths
must be removed or annotated before that probe is clean. The probes catch failures in the exercised paths, not every
possible reflection or trimming issue in a consumer application.

### Records with many fields

A typed record plan applies the curried `construct` function one field at a time. NativeAOT generates the F#
closure types for that application only while their generic nesting stays within the ILC compiler's generic-cycle
limits, and the defaults stop at about nine fields: a larger record compiles, then throws `TypeLoadException` when
it is decoded or parsed.

The `Reified.Schema` package raises the limits for NativeAOT publishes through a transitive build props file
(`--maxgenericcycle:128` and `--maxgenericcyclebreadth:256`), which covers records of at least 100 fields and needs no
setup. The NativeAOT probe decodes and parses a 100-field record to keep this verified.

The limits apply to the whole application, not only Reified's code. They cost nothing for code whose generic
instantiations are finite; code with unbounded generic recursion is expanded further before the compiler stops. For
a larger record, raise `ReifiedIlcGenericCycle` and `ReifiedIlcGenericCycleBreadth`; to manage the limits yourself,
set `ReifiedRaiseIlcGenericCycleLimits` to `false` and pass your own `IlcArg` items.

An installed application gets these limits from the published NuGet package automatically and needs no setup.
Only a project that references the Reified sources directly through a `ProjectReference` — the repository's own
tests and examples — does not receive package build assets; that repo-local case imports
`src/Reified.Schema/buildTransitive/Reified.Schema.props` directly, a relative path that exists only inside this
repository. Do not copy that import into an application that installed the package.

## Fable

`Reified.Result`, `Reified.Constraint`, `Reified.Refinements`, and `Reified.Schema` compile to JavaScript. A browser
front end can parse
and redisplay through the same schema declaration the server uses. The `Json` codec compiles too, so a
codec is available on both sides of the wire. CI compiles the Fable JavaScript surface and runs it on Node
(`dotnet run --project tools/Reified.Build -- --target Fable`). It checks constraint behaviour, operand descriptions,
localized rendering, a codec round-trip, and JSON boundaries against the same checks on .NET.
.NET-only conveniences, such as `Data.ofJsonDocument` and the
`DateOnly` field type, are compile-time gated so the Fable surface never references them.

### Browser bundle size

The compiled modules are declaration-only, imports, exports, functions, classes, and pure `const`
bindings, with no top-level statements, so every `IsFableLibrary` package ships a
`fable/package.json` marking its output `"sideEffects": false`. A browser bundler then drops whatever
a front end does not reference. A schema that resolves only primitive, nested, and collection fields
keeps nothing from `Reified.Constraint` or `Reified.Refinements`; naming a refined field type, or
attaching a constraint, links the constraint engine on demand. The `Fable` target scans the generated
JavaScript and fails if a module grows a top-level side effect, so the flag stays honest.

Approximate gzipped cost of the Reified modules for a front end bundled with esbuild, measured on the
probe surface:

| Front end uses | Reified modules, gzipped |
| --- | --- |
| Schema authoring only, primitive and nested fields | ~4 KB |
| Schema plus the `Json` codec, no constraints or refined types | ~20 KB |
| Codec plus attached constraints | ~26 KB |
| Codec plus a refined field type | ~30 KB |

These are order-of-magnitude figures, not a guarantee. They were measured on the repository's Fable probe surface
(`examples/Reified.FableProbe`), which pins Fable 5.13.0 in `.config/dotnet-tools.json`; the exact esbuild version
and minify flags were not recorded, so reproduce the surface rather than matching a specific bundle byte count:

```sh
dotnet tool restore
dotnet run --project tools/Reified.Build -- --target Fable
# compiled ESM surface: artifacts/fable-js-surface
# then bundle that directory with esbuild, minify, and gzip to compare
```

The decision that made the flag honest is recorded in the 2026-09-01 entry of
[`dev-docs/decisions/README.md`](https://github.com/adz/Reified/blob/main/dev-docs/decisions/README.md).

fable-library-js is additional and shared across everything on the page. The codec links its `Date`,
`Guid`, and decimal support unconditionally; avoid `decimal` and `DateOnly`/`DateTimeOffset` fields in
a browser model where payload size matters.

### Derived wire names under Fable

The `field _.Email` form reads a quotation of the getter to recover the property name. It works on .NET and on the
Fable targets that support quotations:

| Fable target | Derived `field _.Email` | Minimum Fable |
| --- | --- | --- |
| JavaScript, TypeScript, Python, BEAM | Yes | 5.10 |
| Dart | Yes | 5.13 |
| Rust, PHP | No, use `fieldAs` | n/a |

No compiler flag or define is needed, and a schema means the same thing on every target:

```fsharp no-check reason="Field declarations shown outside their enclosing `schema<T>` block."
// Derives the wire name "email" from the property
field _.Email

// Declares it, and works everywhere including Rust and PHP
fieldAs "email_address" _.Email
```


Reading the name happens **once, while the schema value is built**, never per parsed or encoded value, so the
guarantee at the top of this page holds on both targets. The Fable probe declares its schema with `field _.Name`
and asserts the derived wire names from the compiled JavaScript.

## Runtime implications

- `PublishAot=true` and `PublishTrimmed=true` work without `DynamicDependency` annotations, trimmer XML, or source
  generators.
- Startup does not pay for converter caches or expression-tree compilation; codecs and parsers are compiled once from
  explicit declarations.
- The same domain model and boundary declaration can serve .NET services, native binaries, and Fable clients.
