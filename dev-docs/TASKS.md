# Reified Tasks

This is the active development queue. Keep completed work out of this file because loop scripts consume it directly.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep speculative design sketches in `dev-docs/current-ideas/` and high-level durable decisions in
`dev-docs/decisions/`.

Work this queue from top to bottom, with one caveat: the schema surface has just been through heavy churn
(`Schema.check`, the contract generator and versioning engine — see `dev-docs/decisions/README.md`,
2026-07-11..13 entries) and the shape is settling, not settled. Phase 30 below is current thinking with enough
detail to pick up cold; re-read the decisions file and sanity-check the ordering before starting any of them.

Reified has one job: parse-don't-validate. `Schema` is the front door for domain models — parsing, validation,
redisplay, and metadata fall out of one declaration. Plain `Result` with the user's own error DU is the blessed
lane for simple code. `Constraint`, `Refinements`, and the interpreter error types are machinery behind those two
doors, not peer entry points. Effects are not in scope here; they live in Axial.

Phases 19–28-prelude are complete and recorded in `dev-docs/decisions/README.md` and git history; the most recent
completions (2026-07-09..13): the Schema value/model catalog consolidation, `Reified.Refinements` moved into
the error-handling family, `Schema.check` for already assembled typed values,
the `.contract` grammar/generator as wire-tier tooling
(`src/Reified.Schema.Contracts`, `tools/Reified.SchemaGen`, golden corpus in `tests/Reified.Schema.Tests/contracts/`),
the `Contract<'model>` versioning engine (`Contract.parse`/`Contract.parseVersion`, typed contiguous n-1 → n
migrations), `Schema.defer` recursion with finite inspection and `$defs`-based JSON Schema output, the
non-packable `Reified.Schema.Testing` FsCheck adapter (`SchemaGen`), (2026-07-16) multi-version `schemagen`
generation with the user-facing `docs/02-schema/65-versioned-contracts.md` guide, and (2026-07-17) record-first wire schema
generation (`[<DeriveSchema>]` records through an FCS syntax-only frontend into the shared AST/resolver/emitter,
`Reified.DerivedSchema` attributes, `.contract` parked as the secondary declaration form).

## Phase 30: Contracts milestone bundle (gated on a real consumer)

From the same ZIO comparison; these belong *with* the remote-config milestone, not before it:

- **Schema-as-data** (their `MetaSchema`): a stable serialized form of `Inspect`'s `ModelDescription` tree, so the
  browser editor receives the schema as *data* and drives forms dynamically instead of compiling every schema into
  the Fable bundle; also the substrate for contract version-diff tooling (the LSP's planned version-gap warnings).
  Note `Inspect` output is already a plain data tree — most of this is choosing a stable wire format + a codec for
  descriptions, not a new representation. Constraint arguments are `obj`-boxed (`SchemaConstraint.tryFindArgument`),
  which is where the serialization design effort actually lives.
- **Diff/Patch**: schema-derived structural diff of two values ("what changed between desired and reported
  config"), rendered over the same `Path` vocabulary as diagnostics so display infrastructure is shared. Read-only
  walk over erased getters suffices for diff; patch application should be designed once a real consumer establishes
  whether it needs typed field lenses or a schema-directed patch representation. A create schema and a full
  persisted schema are not automatically a good PATCH schema — building the reference app raised this and it was
  deliberately left unanswered. Whatever ships should be an explicit application-authored patch schema, not magical
  optionalization of every field.
- **Deliberately rejected** from the ZIO list (recorded so nobody re-litigates casually): automatic structural
  migrations (conflicts with manual-typed-migrations; their own docs show it silently deleting fields), advisory
  validation, multi-format codecs before a consumer asks, `DynamicValue` as a public surface (at most internal
  plumbing for the two items above).

## Smaller queue items

- **Re-run the Across .NET versions table in `docs/95-notes/80-benchmarks.md` on .NET 11 GA.** It currently uses
  an RC toolchain (2026-09-29), which the page itself says is not representative of shipped performance. Follow
  [`dev-docs/BenchmarkProcess.md`](BenchmarkProcess.md) for the multi-runtime setup and the two BenchmarkDotNet
  bugs it works around, and check the "competitive"/percentage wording still matches wherever it is repeated
  (`docs/index.md`, `docs/01-getting-started/_index.md`).
- **A head-version codec recipe or helper for contracts.** `Contract` parses old versions and exposes the head
  schema, but the "always write the latest version" workflow is assembled by hand from `Contract.headSchema`,
  conversion, and `Json.compile`. Building the reference app got this wrong more than once. Writing an old version
  should stay opt-in and rare, so a documented recipe may be the whole answer.

## Why 1.0 is held back

These checks passed as of 2026-08-14 (see below); 1.0 is deliberately not cut anyway. Reified is in use at work,
and that use has surfaced rough edges the checklist does not catch because it checks *presence* of a capability,
not *discoverability* of it. The concrete one on record: the word "Contract" names four different things across
two packages, and nothing walks a newcomer from one to the next. See the queued item below. Do not cut 1.0 while
a dogfooding user is actively hitting naming/discoverability edges like this one; each one found this way is
cheaper to fix pre-1.0, where renaming is free, than post-1.0, where it needs a deprecation cycle.

## Acceptance Checks

Re-verified 2026-09-29 against the current repo, not the original 2026-08-14 pass. The two-group framing itself
has since changed (see below); items are otherwise still checked against what they actually test for.

- ~~the README and getting started teach exactly two doors: Schema for domain models, plain `Result` for simple
  code~~ — **superseded, not failing.** `docs/index.md` and `docs/01-getting-started/_index.md` were deliberately
  rewritten (2026-09-29) to lead with five capabilities in priority order (JSON codec, inspectable schema, refined
  types, Parse/Result, Data) instead of two doors, because "two doors" undersold the AOT/reflection-free JSON
  story. The two-group *architecture* (`Result`/`Constraint`/`Parse`/`Refinements` as focused values vs. `Schema`
  as the structured-boundary front door) is unchanged in the code; only the homepage's framing of it changed. This
  bullet should read: *the README and getting started state, in the first screen, which capability to reach for
  first, without requiring a reader to already know Reified's internal package split.* `README.md` does not
  literally say "two doors," but its own bullet list (`## Why Reified`) orders capabilities differently than the
  homepage now does, AOT/Fable and the JSON codec are its last two bullets, not its first, so it does not carry
  the "not giving up performance, gaining it" framing either. Reorder README's bullets to match the homepage's
  priority order so the two don't teach a different first-impression.
- **PASSES.** A newcomer handles one error shape at the boundary (`SchemaErrors`, `src/Reified.Schema/SchemaErrors.fs`),
  with one default renderer (`SchemaError.render`) used consistently by `RetainedParseResult`, `SchemaErrors`, and
  `SchemaApi.admit`.
- **PASSES.** Domain value types have one catalog page (`docs/04-refined/20-catalog.md`) and the same refined
  types are schema fields with no separate declaration (`docs/02-schema/40-refined-values.md`).
- **PASSES.** `docs/02-schema/45-union-schemas.md` and `46-advanced-union-handling.md` cover `[<DeriveUnion>]` and
  `Schema.unionWith` with path-aware diagnostics.
- **PASSES.** `Json.compile`/`Json.serialize`/`Json.deserialize` are the trusted-lane codec;
  `docs/95-notes/80-benchmarks.md` carries a benchmarked, dated, multi-runtime comparison against `System.Text.Json`
  (2026-09-29).
- **PASSES.** `src/Reified.Schema.Http/Reified.Schema.Http.fsproj` references only `Reified.Data` and
  `Reified.Schema`; no Axial dependency anywhere in this repo.
- **PASSES.** `docs/90-comparisons/` answers FluentValidation (`20-`, plus DataAnnotations in `05-`), zod (`30-`),
  and FsToolkit.ErrorHandling (`90-`) by name.
- **Not independently re-verified this pass.** FsLiveDocs' audit ran clean (474 blocks, 0 failed, 2026-09-29),
  which confirms generated docs build from current source comments; it does not confirm the *prose* of each
  comment is still accurate to current behavior. Spot-check when touching a package's public API, not on a fixed
  schedule.

### Concrete rough edge: "Contract" naming and discoverability

Filed 2026-09-29 from real usage, not a docs pass. "Contract" currently names, with no naming relationship a
reader can follow from one to the next:

1. `Contract<'model>` (`src/Reified.Schema/Contract.fs`) — the runtime versioned wire-migration type:
   `Contract.create`, `Contract.supersedes`, `Contract.build`.
2. `Reified.Schema.Contracts` (`src/Reified.Schema.Contracts/`) — a *different* thing: the `.contract`-file and
   `[<DeriveSchema>]`-record compiler/AST (`ContractDecl`, `ContractFile`, `ContractRef`, `ContractDiagnostic`).
3. `Reified.Schema.Contracts.Build` — the MSBuild package a project actually references to turn on generation.
4. `[<DeriveSchema(Contract = "Name")>]` — the attribute argument that assigns a record to a versioned contract
   (sense 1), declared and read inside package sense 2/3.

A newcomer installs a package named `...Contracts.Build` (sense 3) to generate code that uses an attribute
argument `Contract` (sense 4) to opt into a runtime type `Contract<'model>` (sense 1) that lives in a package with
a different name (`Reified.Schema`, not `Reified.Schema.Contracts`) than the one they installed. Nothing in the
docs currently states this map explicitly; `docs/02-schema/47-schema-or-generated-dto.md` and
`65-versioned-contracts.md` each explain one piece from inside its own vocabulary.

Fix directions to weigh, not yet decided:

- Rename the compiler package/AST (sense 2/3) away from "Contracts," since it is a code generator for schemas, not
  the versioning concept, e.g. `Reified.Schema.Derivation`/`Reified.Schema.Derivation.Build`, leaving "Contract"
  meaning only sense 1 (the runtime versioning type) project-wide. This is the rename with the best payoff and the
  highest cost: it is a public package rename, needs a deprecation window if done post-1.0, and is free to do now.
- Short of a rename, add one paragraph, probably at the top of `docs/02-schema/65-versioned-contracts.md` or a new
  page it's the canonical link target for, that states the four senses above explicitly and links each to where it
  actually lives, so at least the *map* exists even if the names stay.
- Either way, this is 1.0-gating: renaming `Reified.Schema.Contracts` after 1.0 costs a deprecation cycle;
  renaming it now costs a version bump.
