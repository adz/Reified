# Decision Summary

High-level durable decisions only: what was decided, and the reason that would otherwise be re-litigated.
Detailed specs are deleted once their rules land in `AGENTS.md`, `dev-docs/PLAN.md`, or here.

Nothing here restates what the code already says. If a rule is visible in a signature, a project file, or a test
name, it does not belong on this page — a decision earns a place only when the reasoning is invisible, when the
alternative looks obviously better than it is, or when something was tried and rejected.

Flow decisions are not kept here. The effect system, its service and hosting satellites, and the host HTTP
adapters live in the [Axial repository](https://github.com/adz/Axial) along with the reasoning behind them.

## 2026-08-17: Documentation tooling migrated from Hugo/Docsy to FsLiveDocs

- **`site/` (Hugo/Docsy) and `scripts/docgen/` are gone.** Replaced by FsLiveDocs 0.3.x, driven by
  `.livedocs/config.json` (site name, repo URL, nav, project list) and the `docs/` tree, built via
  `dotnet livedocs audit|build|capture`. CI (`ci.yml`, `release.yml`) and the old
  `scripts/build-docs-site.sh`/`generate-api-docs.sh`/`populate-hugo-content.sh`/`preview-docs.sh`/
  `validate-*-docs.sh` are replaced with the equivalent `livedocs` subcommands; nothing hand-rolled
  remains.
- **`./docs` reorganised into the task-folder tree** from the (now deleted)
  `docs-information-architecture.md`: `01-getting-started` … `10-notes`, numeric prefixes stripped
  from URLs by FsLiveDocs itself. The landing page (`docs/index.md`) routes by symptom per that
  doc's §4. `Reified.Schema.Contracts.Build` and the no-code `Reified` umbrella package are excluded
  from the documented-projects list in `.livedocs/config.json` — the umbrella caused FsLiveDocs to
  attribute every referenced package's entities to it too, and it has no API of its own to document.
- **Deep reference stays generated, not authored.** No `docs/api/{EntityId}.md` enrichment was added
  in this pass — the existing hand-written guide pages (constraint, refined values, schema, etc.)
  already covered the guide/reference split adequately; authored API enrichment is future work, not
  blocking.
- **~40% of existing `fsharp` code fences are marked `no-check`** with a shared reason ("not yet
  re-verified after the FsLiveDocs migration") rather than fixed line-by-line. The old docgen
  pipeline verified them under different rules (Hugo relref resolution, no per-block fence modes),
  so most failures were migration artifacts (duplicate `let` bindings once pages became one
  Page-mode compilation unit, stale cross-links) rather than wrong prose. 192 of 456 blocks are
  genuinely re-verified; the rest need per-block attention as a follow-up, not a blanket unblock.
- **Two real FsLiveDocs 0.3.1 bugs were found and both are now fixed upstream**, while getting this
  migration's `audit`/`build`/`capture` to run for real (not a demo — see `dev-docs/PLAN.md`):
  - Fixed and shipped as **FsLiveDocs 0.3.2** (commit `6322f9b`, published to NuGet): `SymbolLister`
    double-counted every doubly-nested module (`module M = module Advanced = ...`), because
    `ApiDocModel.EntityInfos` already lists nested entities as their own root-level entries and the
    old code additionally recursed into `NestedEntities`. Broke `audit`/`build` with `Duplicate
    documentation block id` for any XML `<example>` on such a member, and inflated the "N unnamed
    parameter" warning counts. Verified against FsLiveDocs' own test suite and self-dogfooded
    `audit`/`build`/`capture`.
  - Fixed as **FsLiveDocs 0.3.3** (commit `f6897d4`, packed locally pending NuGet publish):
    `capture` — not `audit`, not `build`, since only `capture`'s `ReleaseCapsule.validateApi`
    flattens every entity's members across the whole tree — raised `Release API artifact contains
    duplicate member ID <Id>` for `Reified.Refinements.Interval.Lower`/`.Upper`. Root cause: the
    type abbreviation `type DateRange = Interval<System.DateTimeOffset>` got its own
    `ApiDocEntity`, and FSharp.Formatting resolves an abbreviation's `AllMembers` through to the
    aliased type, so `DateRange`'s entity re-reported `Interval`'s own members under the same
    `Symbol.FullName`. Fixed in `SymbolLister.mapEntity`: an entity with
    `Symbol.IsFSharpAbbreviation = true` now maps with an empty member list. Verified end to end
    against this repo: `dotnet livedocs capture --version 0.7.0` (dry-run and real) now succeeds,
    and `dotnet livedocs inspect` confirms the resulting capsule.
- **A real 0.7.0 release capsule exists**: `artifacts/livedocs/Reified-0.7.0-livedocs.zip` (222
  entities, 1,497 members, 2,042 documentation nodes, 184 examples; SHA-256
  `d761e028c2e5a39d93906fdd4ac4068c6cdcacaf6b9d9682d3c126aac238e66a`), captured and inspected clean
  against the **published** FsLiveDocs 0.3.3 (see below). Not published anywhere itself — this is
  local pipeline verification, not a cut GitHub release.
- **`.config/dotnet-tools.json` pins `FsLiveDocs` 0.3.3 from NuGet.** Both 0.3.2 and 0.3.3 are
  published (0.3.3 built by FsLiveDocs' own GitHub CI from commit `f6897d4`). An earlier local pack
  of 0.3.3 (same version number, different content — different SHA-256 nupkg) was used briefly
  before the real release landed; it has been discarded from the local NuGet cache so restore
  always resolves the published package, not a stray local build. `dotnet tool restore` works from
  a clean checkout with no private feed or `--add-source` needed.

## 2026-08-08: Data patterns stay separate from Constraint, and Result gained an accumulating traversal

- **`Data`'s match patterns do not accept `Constraint<'value>`.** They look like a second rule catalogue and are
  not one: a pattern asks whether a `Data` node has a shape, a constraint asks whether an extracted typed value
  obeys a rule. Wiring them together would also make `Reified.Data` — currently a dependency-free leaf that test
  projects can take on its own — depend on `Reified.Constraint` for a convenience only test code wants. `satisfying`
  covers the local structural rule; anything typed belongs to a schema, which already reports paths.
- **`Result.traverseAll` / `Result.sequenceAll` were added** as the accumulating counterparts to `traverse` and
  `sequence`, returning `Result<'output list, 'error list>`. Every mapping runs, in input order, and errors come
  back in input order. Nothing is flattened — a mapping that already returns `'error list` produces a list of
  lists — which is what keeps them different from `NonEmptyList.traverseResult` rather than a second spelling of it.
  `traverse` was not overloaded with the new semantics; a silent change from fail-fast to run-everything would
  break side-effecting mappings without a compile error.
- **The unsupported-operand default messages lost their internal vocabulary.** "failed an equality rule whose
  operand has no portable representation" was a sentence about Reified's data model shown to a user. They now read
  "must equal the required value" and so on: the relation, without the operand Reified cannot print. The technical
  detail is still available through inspection.

## 2026-08-07: Reified is the description side, with one umbrella package

- **The repository split on description versus execution.** Constraint, Refinements, Parse, Result, Data, Schema,
  the JSON codec, the host-neutral HTTP contracts, contract generation, and schema-derived testing are here. The
  dependency runs one way — Axial's adapters execute Reified contracts — and only through published packages.
- **No execution concept enters this repository to close that gap.** No workflow type, no service contract, no
  ambient runtime. A caller that needs one uses Axial or an ordinary function. `Reified.Schema.Http` assembles
  boundary input, problem details, and OpenAPI documents as *values*; it never opens a socket.
- **`Axial.Refined` became `Reified.Refinements` at the package and namespace level only.** The internal `Refined`
  type and module vocabulary reads correctly in the singular. Pluralizing it would have renamed a domain concept to
  match a package id.
- **`Reified` is an umbrella package, and `Reified.ErrorHandling` is not coming back.** The distinction that killed
  the old meta-packages still holds: a grouping that is not a capability does not earn a package id. `Reified` earns
  one because "install the whole library" is a real thing a reader wants and cannot otherwise express. It carries no
  sources and no assembly, so a type never gains a second place it could come from.
- **`Reified.Schema.Contracts.Build` stays outside the umbrella.** This is a NuGet fact, not a preference: MSBuild
  `build/` assets do not flow through a transitive package reference, so an umbrella dependency would install the
  targets without running them — worse than not shipping it, because the failure is silent. Packing to
  `buildTransitive/` would work and was rejected: it would run schemagen on every build of every consumer who
  wanted only a JSON codec.
- **`Reified.Schema.Http` is .NET-only, so the umbrella multi-targets.** It joins the `net8.0` dependency group
  only; netstandard2.1 consumers (Fable, older hosts) get the rest of the umbrella rather than no umbrella at all.
- **The three checks the extraction dropped were rebuilt, not waived.** The Schema CE compile-fail fixtures, the
  API-shape suite, and the Fable surface check were recovered from Axial's history and rewritten Reified-only. The
  Fable benchmarks stayed with Flow: a benchmark comparing `Flow` to manual composition is not this repository's
  claim. `examples/Reified.FableProbe` multi-targets so the *same* assertions run on .NET and on Node, which is the
  only arrangement in which a divergence between the two is visible.
- **A package on the Fable list must be compiled by the probe, or the list is a claim nothing checks.**
  `JsonSchema.generate` used `Type.GetTypeCode`, which Fable does not support. It was invisible while
  `Axial.Schema.JsonSchema` was a separate package the Fable project did not reference, and stayed broken from the
  day that package was folded into `Reified.Schema` until the surface check was restored.

## 2026-08-03: Result and Values are separate documentation areas

- **Result is a peer product, not a member of Values.** They answer different questions: Result composes failures,
  the Values packages admit values. Presenting Result inside Values was tried and rejected — it reproduced the
  meta-package framing the split removed.
- **Values is navigation only.** No `Reified.Values` package or namespace will be created.
  `validate-values-docs.sh` fails if any rendered page tells a reader to install one.
- `Reified.Constraint.Violation` is not advertised on the Result reference page. Result is a standalone leaf with
  no Constraint dependency, so naming a type from another product there was simply wrong.
- Search vocabulary is unaffected: package tags and descriptions keep "error-handling" and "validation" where they
  name a user problem. Only the navigation category was retired.

## 2026-08-02: Constraint unification

- **One public value-rule concept, `Constraint<'value>`:** a reusable description of valid values that `check`
  executes. `Check<'value>`, `CheckFailure`, `CheckDSL`, the public `Predicate` catalogue, `SchemaConstraint`,
  `ConstraintDescriptor`, and the `Schema.Constraint` facade were removed outright, with no aliases.
- **A constraint retains both `Test` and `Check` deliberately.** `test` over a conjunction may fail fast, while
  `check` must run every child to accumulate. Interpreted atoms and `custom` predicates keep a Boolean path that
  does no violation work; `customWith` derives `test` from its callback and pays that cost, which is inherent in
  the information the author chose to supply.
- **Two tiers, and the split is load-bearing.** **Interpreted** constructors build one `ConstraintAtom` and place
  that same value in both the description and any violation, so identity and failure cannot drift; the algebra is
  closed and grows only by release. **Opaque** constraints (`custom`, `customWith`, `notWith`, `contramap`) run
  normally and are honestly invisible to export and proof.
- **There is no interpreted `not`.** Several families have no complement, and float comparisons are not
  complementable under NaN. An operation that is *sometimes* interpreted is worse than one that is honestly opaque.
- **`Violation` is plain comparable data** with no closure or description reachable from it, so a failure survives
  its constraint going out of scope. A leaf carries the failing atom itself. That removed a string round-trip which
  reconstructed constraint identity by code — and returned the wrong message when two constraints on one field
  shared a code. There is no `Violation.code`; keys exist only through `Violation.toMessageTree`, computed at the edge.
- **Removed with reasons:** per-constraint message overrides (`withMessage`) and diagnostic rewriting
  (`mapViolation`/`withViolation`), because they let the reported failure diverge from the published description;
  `Refinement.defineAll`/`defineWithCheck`, because `Constraint.all` and `Constraint.custom` already compose;
  `Constraint.supplied`/`omittable`, because supply is decided before a typed value exists and is Schema's concern.
- **Interpreters divide by what they *claim*, not by whether they produce a value.** The earlier "value producers
  fail closed" rule contradicted the trusted codec's documented contract. Admission and constraint-satisfying
  generation fail closed; trusted structural codecs make no constraint claim; documentation and export degrade
  honestly via `x-reified-runtime-constraints`.
- **Semantics were corrected wherever runtime and export disagreed.** Text cardinality counts code points, not
  UTF-16 units. `Constraint.numeric` is ASCII `^[0-9]+$` rather than `\d`, so the runtime rule and its lowering
  agree by construction. Text `present` emits only `minLength: 1` and keeps the non-blank rule as runtime metadata,
  because .NET whitespace and ECMA-262 `\s` differ in both directions (U+0085, U+FEFF).
- **Operand conversion happens at construction and never throws.** The old projection sent every `float` through
  `decimal`, so `Constraint.lessThan infinity` raised `OverflowException`. Floats keep their own case, with
  equality that treats NaN as self-equal and separates signed zero, using only arithmetic proven on Fable and
  NativeAOT rather than `BitConverter`.
- **Constructors taking an operand are `inline` so the operand resolves on its static type.** A boxed type test
  cannot do this: Fable erases a `Guid` to a plain string and a `TimeSpan` to a number, so `:? Guid` there labels
  the operand `Text` while .NET labels it `Guid` — one constraint meaning two different things per platform. The
  Fable surface check asserts both platforms describe the same constraint identically.
- The term language, `FieldReference`, and `Origin` are **out of scope** and are not present as placeholder cases.
  They become additive when a real consumer establishes field identity, nesting, and proof semantics.

## 2026-08-03: Localization lives at the rendering edge, in a Renderer

- **A `Violation` never carries a culture, resource manager, renderer, Schema `Path`, or application context.** A
  `Renderer` supplies all of that when a message is produced. That is what keeps a violation retainable,
  comparable, and portable across a package or a wire boundary.
- `Renderer.attribute` **replaces** rather than appends, so a form-scoped renderer is reusable across sibling
  fields. A demonstrated nested-attribute case gets an explicitly named API (`Renderer.Advanced.attributePath`),
  not implicit append.
- **Catalogue entries are bare predicates.** The attribute noun, the actual-value clause, and group/list joining
  are separate composition entries. That keeps `{actual}` optional without an optional-placeholder rule, and lets a
  locale reorder the sentence without touching the twenty-five predicates.
- **`MessageDescriptor` and `MessageFormatSpec` are split** so `Reified.Schema` can push its own `schema.*` entries
  through every renderer mechanic without `Reified.Constraint` learning a Schema identity or acquiring a reverse
  dependency.
- **Plural support is `.one`/`.other` on entries declaring exactly one operand**, tried before the bare key at the
  same contextual level. Full CLDR selection, and any language whose group joining cannot be expressed as
  pair/start/middle/end, belong to `Renderer.Advanced.ofResolver` and `Violation.toMessageTree` — a stated limit,
  not a gap.
- **Reified never invents a message key for `Constraint.custom` prose.** An invented key names a catalogue entry
  that does not exist and fails in the language nobody tested.
- The .NET resource-manager constructors are **conditionally absent** under Fable rather than compiled as a silent
  no-op.

## 2026-07-31: Numeric ranges are constraints, because F# cannot carry them through arithmetic

- `PositiveInt`, `NonNegativeInt`, `NonZeroInt` and their `Int64`/`Decimal` variants were removed. A
  refinement-typed language infers that `a + b` is positive when both operands are; F# cannot, so every arithmetic
  step re-establishes the fact by hand. Integer arithmetic is unchecked, so an addition returning `PositiveInt`
  would be unsound — which leaves returning `Result`, and `((a + b) * c) + d` then costs two binds and a map.
  Callers unwrap instead, so the types add bulk at every use site while catching nothing.
- Express the ranges as constraints on the primitive. Where a nominal type is still wanted for a numeric
  *identifier* — identity rather than arithmetic — define it over the same constraint with `Refinement.define`.
- `FiniteFloat`/`FiniteFloat32` are kept, because their guarantee is that **aggregation means something**: one
  `NaN` or infinity silently destroys a whole sum or average. That needs no arithmetic on the type itself, so they
  keep `sum` and `average`, failing once at the end rather than per step.
- `UnitInterval` is unaffected: it is genuinely closed under multiplication, `complement`, and `lerp`.

## 2026-07-30: Refined types earn their place by removing branches, not by wrapping validation

- **A type ships only if it makes a partial operation total, guarantees an algebraic property, encodes a
  relationship between values, preserves an invariant across a family of operations, or removes branches from
  *consumers*** rather than only from construction. `TrimmedString`, `Slug`, `BoundedString`, `BoundedList`,
  `BoundedArray`, `NegativeInt`, `NonPositiveInt`, `DateTimeOffsetRange`, `DateOnlyRange`, `Collection.exactlyOne`,
  and `Collection.atMostOne` failed that test and were removed. Every one maps onto constraints that already
  shipped, and **no new machinery was added for the removals** — an earlier design introducing a `ConstraintGroup`
  type was dropped once it became clear the composition already existed.
- **`NonEmptyList` is structurally non-empty with a public case; `NonEmptyArray` stays smart-constructed.** A
  structural head/tail would forfeit contiguous storage and indexed access, which are the reasons to pick an array.
  The asymmetry is intentional.
- **One generic, always-inhabited, inclusive `Interval<'value>`** replaces both hand-rolled range types. Emptiness
  is `Interval option`, which is what `intersect` returns; a second "possibly empty" type would double every
  operation and make none of them total. `Bounded<'value>` carries bounds at run time, not as phantom type
  parameters: F# has no type-level naturals, and Peano encoding has no Fable story.
- **Numeric modules ship both forms.** `add`/`multiply`/`sum` use checked arithmetic and return `Result`;
  `saturatingAdd`/`saturatingMultiply` are total and clamp. `NonZero` is justified by branch removal, not a total
  `divide`: `DivideByZeroException` becomes unreachable, but `Int32.MinValue / -1` still overflows.
- **What `NaN` actually breaks is aggregation, not ordering.** F# generic comparison orders `NaN` consistently, so
  `List.sort`, `Map`, and `Set` all work on plain `float`. `NaN` silently destroys an average, and breaks
  `List.contains`/`List.distinct`, which use IEEE equality. A comparison hand-written with `<` and `>` is also
  intransitive under `NaN` and makes `sortWith` return unsorted output without raising.
- **Numeric genericity uses `inline internal` SRTP with a monomorphic public surface.** `INumber<'T>` is not an
  option: netstandard2.1 predates it and Fable does not support static abstract interface members.
- **64-bit integers are not mapped onto `decimal`.** It changes the type's meaning; a test parses
  `9007199254740993` (beyond 2^53) to prove the value never round-trips through a float. JSON has no literal for
  `NaN` or the infinities, so schemas needing them must refine to `FiniteFloat`.
- **A built-in constraint gets its own case, not `Custom "finite"`.** The ripple through emitter, parser,
  validator, and generator is the point: a built-in should be inspectable by every interpreter, and the
  reserved-code guard then correctly rejects an application redefining `finite`.
- **Writing real code against the API found four defects a source review had missed, all the same shape:** a doc
  comment claiming a guarantee the code did not deliver (`NonEmptyList.zip` documented truncation but called
  `List.zip`, which raises; `DistinctList.toMap` documented losslessness but silently dropped entries;
  `PositiveDecimal.average` let an `OverflowException` escape; `Interval.between` assumes a total order, which
  `float` is not). Reviewing the source is not a substitute for using the API.

## 2026-07-17: Two schema tiers — permissive wire, strict hand-written domain

- A **wire schema** is shaped per format and permissive, and its result is a plain public DTO record. A **domain
  schema** is strict hand-written F# — invariants, smart constructors, DUs. The wire result maps to the domain
  through an ordinary function returning `Result`; that mapping function is where strictness lives.
- **Versioning applies to the wire tier only.** When stored payloads must keep parsing after the wire changes, the
  `Contract` engine chains frozen wire versions with typed migrations, and the domain map runs after the chain.
- **Contracts are never the domain authoring surface.** Making them universal was explored and rejected: IDL-first
  domain modeling is a pattern .NET has consistently abandoned in favour of IDL-at-the-edge (protobuf, TypeSpec),
  F# adopters chose F#'s type language, and universal scope turns every F# type feature and every format's
  semantics into a grammar feature request. Multi-format serialization would enter as additional Schema
  interpreters, never as grammar features.

## 2026-07-17: Records are the primary wire declaration; `.contract` is parked

- `[<DeriveSchema>]`-marked plain F# records are the primary way to declare the wire tier, through an FCS
  syntax-only frontend into the same AST, resolver, and emitter as `.contract` files. A schema module only is
  emitted, so the F# compiler catches record/schema drift.
- `.contract` files stay shipped and compiling but receive no further investment. **The bespoke-LSP plan is
  superseded**: records get the entire F# IDE experience — highlighting, rename, find-references, hover — for free,
  which was most of what the LSP would have built.
- Whether `.contract` is removed before 1.0 is decided by the config-system dogfood. If records cover it, the
  grammar goes and the pipeline it funded remains as the record frontend's machinery.

## 2026-07-16: schemagen generates version chains; migrations are builder parameters

- One `.contract` file may declare several versions, oldest first with no gaps. The resolver enforces contiguity,
  single-file chains, and that superseded generated names do not collide with declared contracts.
- The latest version keeps the bare generated name; superseded versions emit version-suffixed frozen types.
- **The grammar never names F# symbols for migrations.** The latest module gains a generated `contract` builder
  whose parameters are each typed n-1 → n migration; migrations stay hand-written functions the compiler checks
  against the generated version types, so cutting a new version breaks every construction site until its migration
  exists.
- The earlier gate ("multi-version generation only after dogfooding a hand-written chain") was resolved by keeping
  the engine unchanged and the generated surface minimal: the builder function is the only new emission.

## 2026-07-13: Contract parsing preserves trust and diagnostics

- `Contract.parse`/`parseVersion` return the ordinary `'model`. A successful contract parse has passed the head
  schema's field and constructor gates.
- Errors carry `SchemaErrors`, not a single `SchemaError`. An earlier sketch used one; parsing can report several
  path-bearing failures, and selecting one would discard boundary information.
- Version labels are positive and contiguous, and `supersedes` registers only the immediately preceding version.
  That matches the promised n-1 → n migration model and prevents accidental gaps.

## 2026-07-13: Recursive schemas use one memoized deferred node

- `Schema.defer`'s thunk is memoized. Parsing forces it at each finite data node, while codec compilation installs
  a delayed plan so compiling a cyclic schema graph terminates.
- **Inspection assigns traversal-local integer identities**, so the public inspection tree stays finite without
  runtime reflection or global identity state.
- **Recursive authoring must use a delayed schema holder** so the thunk returns the same built schema. Calling a
  schema factory afresh from the thunk creates an endless sequence of distinct deferred nodes and defeats cycle
  identity.
- Internally-tagged union payloads stay immediate nested model schemas, because their fields must be known while
  validating discriminator collisions. A recursive model can contain unions, but an inline-union case cannot itself
  be the deferred edge.

## 2026-07-13: Schema test data is a non-packable FsCheck adapter

- FsCheck was chosen because this repository already uses xUnit and FsCheck has the established F# generator API.
  There was no evidence for the adoption target's actual choice. This is not a commitment against a later Hedgehog
  adapter, and the test-framework dependency never moves into a public package.
- **Generation produces constraint-satisfying `Data`, and `SchemaGen.model` then parses it** and filters
  constructor rejections — so constructor invariants are not duplicated in the generator.
- **Pattern reversal, custom constraints, `notEqualTo`, `contains`, and `distinct` are not guessed.** Derivation
  returns `UnsupportedConstraint(path, code)` unless a generator is supplied for that exact field path.

## 2026-07-12: What building a real application on Reified actually proved

Recorded from the reference-app exercise. The application itself is now `examples/Axial.ReferenceApp` in the
Axial repository, where it consumes Reified as a package; these are the conclusions about Reified that outlived it.

- **The application needed far less of the catalogue than the repository's size suggests.** Schema, diagnostics,
  contracts, codecs, and a handful of refined types covered nearly everything. That is the argument for judging
  every further abstraction by application demand rather than by parity with an inspiration ecosystem — and for
  not presenting each helper subsystem as a peer entry point.
- **`Data` earned its place for an unexpected reason.** It was written for fixtures, but its real value was that
  form redisplay and JSON parsing genuinely shared one representation.
- **Contract migration revalidation caught a real category of trust hole**, and was stronger than expected. It
  stays.
- **The explicit builder stayed readable at nested and versioned records.** It needed neither reflection nor a
  different authoring surface to feel usable, which is the evidence behind keeping declaration explicit.
- **Refined values do preserve their invariant; the friction is primitive interop and wrapper stacking.** Say that
  honestly rather than claiming refined wrappers behave transparently like primitives.
- **A total refined-schema bridge over fallible smart constructors duplicates the invariant.** The domain's natural
  constructor shape is fallible, so `Schema.refine` became fallible and calls the same constructor the rest of the
  application calls.
- **Do not imply a compiled codec fully validates untrusted input.** The reference app's boundaries made the split
  between the trusted codec lane and the diagnostic parse lane concrete, and the docs must keep it.

## Current Invariants

Rules that hold across the codebase and are not obvious from any single file.

- **Reflection is not the foundation for schema construction, constructor binding, validation, or codec
  execution.** Reflection may be an optional tooling path on .NET, but the authored path stays AOT- and
  trimming-safe with a Fable fallback. If boilerplate becomes painful, prefer build-time generation over
  reflection-heavy runtime discovery.
- **Codecs reference `Reified.Schema`; `Reified.Schema` never references a codec package.** Metadata comes from
  `Inspect`, and hot-path plans from the record-plan compiler protocol. The direction is what makes a dependency
  cycle impossible.
- **The codec is the trusted hot path.** It enforces wire shape and required fields but does not run constraint
  metadata. Untrusted input keeps going through `Data` + `Schema.parse` for complete path-aware diagnostics.
  Codecs must not be folded into `Reified.Schema`: that would pull diagnostics into the schema package and a wire
  runtime into the schema core.
- **There is no "checked codec" compile option.** A consumer wanting constraint enforcement on trusted-lane decode
  composes `Json.deserialize` then `Schema.check`. If that proves too slow for a real consumer, the pre-chosen
  answer is a `Json.deserializeValidated` helper in `Reified.Schema` — never duplicated constraint lowering inside
  the codec package.
- **Inspection never parses input, runs checks, or constructs models.** Non-validation interpreters start from
  `Inspect`, which describes a built schema as plain metadata trees.
- **Unions require discriminators.** Three wire shapes are supported — externally wrapped, internally tagged
  (valid only when every payload is an object whose field names do not collide with the discriminator, checked at
  construction), and bare-string enums for payload-less cases. There are no untagged unions.
- **Constructor errors are a second stage after field parsing.** If any field has intrinsic diagnostics, the model
  constructor must not run; constructor errors are reported only when every argument is already trusted.
- **Boundary supply is Schema-owned** (`Schema.mustSupply`/`mayOmit`). It is decided before a typed value exists,
  so it is not a value constraint.
- **`Schema.check` re-runs field constraints and then re-invokes the constructor.** Its predecessor only re-checked
  per-field constraints and silently skipped the model's own invariant — a `DateRange` with `Start` after `End`
  passed it. The constructor re-check is the point, not a bolt-on.
- **`Reified.Schema.Json` is part of the supported Fable surface.** The `FABLE_COMPILER` gates are load-bearing and
  every codec optimization must keep the JS branch working. This is what completes the zod comparison: one
  declaration shared between server and browser covers serialization as well as parsing.
- **`Data.ofJsonElement`/`ofJsonDocument` stay gated to `net8.0 && !FABLE_COMPILER`.** If a netstandard2.1 consumer
  asks, the pre-chosen answer is a TFM-conditional `System.Text.Json` reference on netstandard2.1 only — not a
  split adapter package, which would force a different module name.
- **No fused fast boundary path.** The boundary-lane cost is not a reported problem, and `Schema.parse` keeps its
  raw-retaining redisplay contract. If demand appears, the pre-chosen shape is a separate entry point
  (`Schema.parseUtf8`), prototyped in the benchmarks project first — never an optimization flag on `Schema.parse`.
- **Codec decode allocation work is deferred** until performance becomes a pitch line. If pursued, the pre-chosen
  approach is fixed-arity typed decoders for arities 1..8 with the slot decoder as fallback, dispatching on field
  count from the compiled record plan — no reflection.
- **`$defs` hoisting for non-recursive nested reuse is deferred** until a sample needs it. Recursive schemas lower
  to deterministic `recursiveN` entries with `$ref` edges, so inlining terminates.
- **The UI-metadata interpreter stays a prototype.** Promotion waits for an external consumer, and if promoted, a
  sample must consume the shipped module — otherwise the duplication just moves. Scope stays field list plus
  control kinds; layout, localization, and widget options are application concerns.
- **Generated reference docs come from XML comments and generator inputs.** Do not hand-edit a generated page as
  the primary fix.

## Settled Rejections

Recorded so they are not casually re-opened. Pre-ideas live in [`../current-ideas/`](../current-ideas/).

- **`Model.construct`** (typed field values in, schema-checked model out). RESOLVED by reduction: there is no
  positional construction API. `Schema<'model>` cannot carry per-field types, so a typed positional
  `Model.construct` is impossible without source generation; every runtime shape tried — builder ceremony,
  tuple-returning `buildWithConstruct`, reflection off a draft record, a `(string * obj) list` — was rejected. The
  type-erasure wall is structural, not a missing-effort gap.
- **`Trusted<'model>`.** REJECTED after reference-app review. A universal wrapper made parse, contract, and
  ordinary domain construction carry proof ceremony without establishing durable F# invariants. Construction has
  two deliberate strengths instead: public wire/draft records assembled with named fields and admitted through
  `Schema.check` (a successful-flow guarantee), and domain types with private representations and authoritative
  smart constructors (a durable one).
- **Context-dependent model rules.** REMOVED: the schema-specific helper API duplicated application functions while
  requiring a parallel field-path identity system. Operation-specific admission stays an ordinary result-returning
  function; intrinsic admission stays in `Schema.parse` and `Schema.check`.
- **Automatic structural migrations, advisory validation, and multi-format codecs before a consumer asks.**
  Rejected from the ZIO comparison; automatic migration in particular is shown silently deleting fields in its own
  documentation.
- **A `dotnet new` template.** Deferred until the public surface stabilizes at or near 1.0, and gated on an
  external user asking for a scaffold. Packaging a sample that still changes with the pre-1.0 API buys nothing.
