# Reified Agent Index

Open this file after `AGENTS.md` and before broad repository search.

## Current Slice

The active queue is `dev-docs/TASKS.md`. Keep completed work out of that file, but keep the remaining active queue
there because loop scripts consume it directly.

Current product direction is in `dev-docs/PLAN.md`. Durable high-level decisions are in
`dev-docs/decisions/README.md`. Speculative sketches are in `dev-docs/current-ideas/` and should be opened only when the
task is about promoting, rejecting, or implementing that sketch.

Working on `src/Reified.Schema`? Read `dev-docs/schema/internals.md` first (implementation map), and
`dev-docs/schema/constructor-last.md` for the current authoring-surface direction.

## Package Graph

- `Axial.Flow`: independent workflow package (`src/Axial.Flow/`). Must not depend on the ErrorHandling packages or
  `Reified.Schema`.
- `Reified.Result` (`src/Reified.Result/`): generic Result combinators, conversions/extraction helpers, and `result { }`
  in the `Reified.Result` namespace. Independent leaf.
- `Reified.Constraint` (`src/Reified.Constraint/`): `Constraint<'value>`, `Violation`, the `ConstraintDescription` read model, and `ConstraintDSL`, all in the `Reified.Constraint` namespace. One value-rule vocabulary; there is no `Check` type and no second catalogue.
  Returns the standard F# `Result` type; does not depend on `Reified.Result`. Independent leaf.
- `Reified.Parse` (`src/Reified.Parse/`): `ParseError` and primitive `Parse.*` functions. Independent leaf.
- `Reified.Refinements` (`src/Reified.Refinements/`): invariant-carrying types and the operations that justify them. Depends
  only on `Reified.Constraint`. A type ships only if it makes a partial operation total or removes a branch from consumers;
  validation-shaped concepts are constraints in `Reified.Constraint` instead.
- `Reified.Schema` (`src/Reified.Schema/`): schema declaration (`Schema` module), parsing and checking (`Schema.parse`,
  `Schema.parseRetainingInput`, `Schema.check`), inspection (`Inspect`), contracts,
  and refined schema adapters (`RefinedSchemas`) in one package. Depends on `Reified.Data`, `Reified.Constraint`, and
  `Reified.Refinements` (never `Reified.Result`). Schema owns path-aware accumulated errors.
- `Reified.Schema.JsonSchema` (`src/Reified.Schema.JsonSchema/`): JSON Schema generation (`JsonSchema.generate`) in the
  `Reified.Schema` namespace. Depends on `Reified.Schema`.
- `Reified.Schema.Json` (`src/Reified.Schema.Json/`): compiled JSON codecs. Depends on `Reified.Schema`.
- `Reified.Schema.Http` (`src/Reified.Schema.Http/`): host-neutral HTTP boundary support — query/form structured data
  (`BoundaryInput`), RFC 9457 problem details from parse diagnostics, and OpenAPI 3.1 documents assembled from
  `EndpointSpec` values. Depends on `Reified.Schema` only; never on `Axial.Flow`.
- `Axial.Schema.Http.AspNetCore` / `Axial.Schema.Http.GenHttp` (`src/Reified.Schema.Http.*/`): host boundaries over
  `Reified.Schema.Http` and `Axial.Flow`. The default API lowers an ordinary endpoint Flow from schema-trusted request
  input through explicit application services to a native response; lower-level `RetainedParseResult` adapters remain for
  redisplay and custom boundaries. Routing and app wiring remain the host's idiom.
- `Reified.Schema.Testing` (`src/Reified.Schema.Testing/`): non-packable FsCheck adapter deriving test data from Schema.
  Depends on `Reified.Schema` and FsCheck; never move the test-library dependency into a public package.
- `Reified.Schema.Contracts` (`src/Reified.Schema.Contracts/`): non-packable wire-tier generation library — the
  `[<DeriveSchema>]` record frontend (`Records.fs`, FCS syntax-only), the `.contract` parser, and the shared
  resolver/emitter. The `Reified.Schema.Derive` attribute namespace lives in `Reified.Schema` itself (inert metadata).
  FCS stays tool-tier only: never referenced from a packable library.
- `Reified.Schema.Contracts.Build` (`src/Reified.Schema.Contracts.Build/`): packable targets-only MSBuild package
  running `scripts/schemagen` before compile over `<ReifiedDeriveSchema>`/`<ReifiedContract>` items.
- `Axial.Flow.*` add-on packages depend on `Axial.Flow`.
- There are no meta-packages. `Reified.ErrorHandling` and the `Reified` umbrella were both deleted; every install is a
  focused package. **Values** — Constraint, Refined, and Parse — is a documentation grouping only: no package, no
  namespace. `Reified.ApiShape.Tests` pins this with `no meta-package remains in the graph`.

## Open These First

- Flow/runtime/layers/services: `src/Axial.Flow/**`, relevant `src/Axial.Flow.*/*`, `tests/Axial.Flow.Tests/*Workflow*`,
  `tests/Axial.Flow.PlatformService.Tests/**`, and `dev-docs/PLAN.md`.
- Check/Result: `src/Reified.Constraint/Check.fs`, `src/Reified.Result/Result.fs`,
  `tests/Reified.Constraint.Tests/CheckTests.fs`, `tests/Reified.Result.Tests/ResultTests.fs`,
  `tests/Reified.ApiShape.Tests/ApiShapeTests.fs`, and `dev-docs/PLAN.md`.
- Parsing and refined values: `src/Reified.Parse/{Errors,Parse}.fs`, and in `src/Reified.Refinements/` (compile order)
  `Refinement.fs` -> `NonEmpty.fs` -> `Interval.fs` -> `Bounded.fs` -> `Finite.fs` -> `UnitInterval.fs` ->
  `Refine.fs`. Tests are one file per area under `tests/Reified.Refinements.Tests/`. Adding or removing a refined type
  also means editing the `SchemaDefaults` witnesses in `src/Reified.Schema/Shape.fs` and
  `src/Reified.Schema/RefinedSchemas.fs`. There are no refined numeric types: a numeric range is a constraint, because
  F# cannot carry it through arithmetic.
- Schema metadata/builder: `src/Reified.Schema/Schema.fs`, `tests/Reified.Schema.Tests/Schema*Tests.fs`, and the schema section in
  `dev-docs/PLAN.md`.
- Schema input/rules/interpreters: `src/Reified.Schema/{Model,Data,SchemaValidation,RetainedParseResult,Rules}.fs` and
  `tests/Reified.Schema.Tests/*ParseTests.fs`.
- User-facing docs: one area per top-nav product — `docs/result/`, `docs/values/`, `docs/data/`, `docs/schema/`,
  `docs/flow/`. Values covers Constraint, Refined, and Parse and is navigation only. Read `dev-docs/DOCS.md`
  before editing `docs/**`, source comments, generated reference pages, `llms.txt`, or site content.
- Agent process/docs: `AGENTS.md`, this file, `dev-docs/TASKS.md`, and `dev-docs/PLAN.md`.

## Generated Or Noisy Paths

Default `rg` ignores generated/vendor-heavy paths through `.rgignore`:

- `docs/result/reference/**`
- `docs/values/reference/**`
- `docs/data/reference/**`
- `docs/schema/reference/**`
- `docs/flow/reference/**`
- `site/content/reference/**`
- `site/_vendor/**`
- `site/public/**`
- `BenchmarkDotNet.Artifacts/**`
- `.fsdocs/**`
- `output/**`

Search these with `rg -u` or an explicit target only when the task is about generated output, reference docs, site
artifacts, or build artifacts.

## Validation Commands

- Source/package moves: `bash scripts/check-source-inventory.sh`.
- Schema CE type-state changes: `bash scripts/check-schema-ce-errors.sh`.
- Focused .NET tests: `dotnet test <project> --nologo -v minimal`.
- Public API/doc generator impact: update source comments or generator inputs first, regenerate affected docs, and defer
  `bash scripts/validate-docs.sh` until a phase or release checkpoint unless the task asks for full validation.
- Release/deploy doc checkpoint: `bash scripts/validate-docs.sh`, then `npm run build` in `site`.
- Live docs preview only when browser review is needed: `bash scripts/preview-docs.sh`.
