# Namespace Flatten

Goal: `open Reified` puts the core surface in scope. Each further `open` buys a capability
rather than paying a namespace tax.

| Task | Preamble |
|---|---|
| Read schemas, inspect, contracts | `open Reified` |
| Define schemas | `+ open Reified.SchemaDSL` |
| Constraints unqualified | `+ open Reified.ConstraintDSL` |
| Use refined types | `+ open Reified.Refinements` |
| Derive from attributes | `+ open Reified.DerivedSchema` |

Down from four lines to two for the common case.

## Mechanism notes

These were established by spike; they constrain the design and are not negotiable.

- A namespace spans files and assemblies. A module spans neither (FS0248 within an assembly).
- FS0247 is per-assembly: `module Reified.Schema` in `Reified.Schema.dll` coexists with
  `namespace Reified.Schema.Json` in another assembly. It does **not** coexist with
  `namespace Reified.Schema.Derive` in the same assembly — hence phase 1.
- Modules cannot be re-exported: no abbreviation, no `AutoOpen`, no alias. Only types can
  (`type X = A.B.C`), and type abbreviations carry DU cases and generics but restate their
  own constraints.
- `open Reified.Schema` stops compiling once `Schema` is a `RequireQualifiedAccess` module
  (FS0892). Call sites move to `open Reified`.
- Machinery cannot be `internal` if a public signature returns it (FS0410). File-level
  modules plus `EditorBrowsable` and `CompilerMessage` are the practical equivalent.

## Decisions taken

- **The vocabularies are `<Pkg>DSL`.** `SchemaDSL`, `ConstraintDSL`, `DataDSL`, `ResultDSL`.
  The `<Pkg>` prefix is forced — three modules named `Syntax` cannot share `Reified` — and
  `DSL` over `Syntax` is the house name for them. `ResultDSL` carries only computation
  expressions rather than a vocabulary of constructors, so the name fits it least; it sits
  under the shared convention anyway, and moved to `namespace Reified` so all four are
  reached the same way rather than as `Reified.Result.ResultDSL`.
- **`field` stays at namespace level.** `ApiShapeTests.fs` pins it present on purpose:
  `field _.Email` must resolve from the same open that supplies the vocabulary.
- **`Reified.Refinements` is untouched.** `Refine` and `Refinement` stay there rather than
  moving up — 10 call sites do not justify splitting `Refine.fs`, and the split would
  disturb the modules just nested under `Refine`.
- **No shipped alias module.** A user whose domain owns a catalogue name writes their own
  local `type`/`module` abbreviation. A shipped alias would duplicate generic constraints
  across files and drift silently.
- **`Reified.Result` keeps its namespace.** Not referenced by `Reified.Schema.fsproj`, and
  verified safe: a child namespace named `Result` shadows neither FSharp.Core's `Result`
  module nor its type.
- **`Reified.Data` flattens too — forced, not chosen.** A child namespace shadows a type of
  the same name for any file declaring or opening the parent, so `namespace Reified` plus
  `namespace Reified.Data` makes `Data.Text` unresolvable. Confirmed cross-assembly by
  spike. Type annotations (`: Data`) and module functions (`Data.ofMap`) still resolve —
  only RequireQualifiedAccess *union cases* break, which is exactly how `Data` is consumed.
  Qualifying every case site was the alternative: 1 source file, 29 test files, and every
  user who writes `open Reified` alongside `open Reified.Data`. Flattening removes the
  child namespace and the shadow with it. `Data`'s 20 public names were already verified
  collision-free in the merged namespace.

## Phases

Ordered so each phase builds and tests green on its own, and so the riskiest rename
(Schema) lands after its dependency (Constraint) has proven the pattern.

- [x] **Phase 0** — Land the prerequisite renames (`SchemaPath`, `Refine.*` nesting).
- [x] **Phase 1** — `Reified.Schema.Derive` -> `Reified.DerivedSchema`. Mandatory before
      phase 5: it is the only sub-namespace sharing `Reified.Schema.dll`, so FS0247 fires
      otherwise. Standalone and small, so it goes first.
- [x] **Phase 2** — `Reified.Parse` -> `Reified`. Two files, two public names. The canary
      that proves the flatten end to end at minimum cost.
- [x] **Phase 3** — `Reified.Data` -> `Reified`. Before Constraint and Schema because both
      consume `Data`, and because leaving it a child namespace breaks every
      `Data.<UnionCase>` reference in a file that declares or opens `Reified`.
- [x] **Phase 4** — `Reified.Constraint` -> `Reified`, `module Syntax` -> `ConstraintDSL`.
      Before Schema because Schema depends on it.
- [x] **Phase 5** — `Reified.Schema` -> `Reified`, `module Syntax` -> `SchemaDSL`.
      The large one: 21 source files plus the satellite packages' opens.
- [x] **Phase 6** — CE machinery into `SchemaFieldSteps` and `SchemaCeBuilder` modules with
      `EditorBrowsable(Never)` and `CompilerMessage(..., 42, IsHidden = true)`. Two modules
      rather than one because `field` sits between them in dependency order and stays at
      namespace level. `IRecordPlanCompiler` and `IRecordPlanState` deliberately stay out:
      `Reified.Schema.Json` implements them, so they are an extension point rather than
      machinery, and hiding them would warn legitimate implementers.
- [x] **Phase 7** — Umbrella `.fsproj` comment, docs, `llms.txt`, docgen inputs, generated
      reference pages.
- [x] **Phase 8** — Full validation: build, tests, source inventory, Fable surface, docs.

## Sweep bill

- ~220 `open Reified.Schema|Constraint|Parse` call sites move to `open Reified`.
- `ApiShapeTests.fs` asserts on compiled type names throughout; every `Reified.Schema.X`
  and `Reified.Constraint.X` string changes.
- `scripts/docgen/Program.fs` member IDs are all `T:Reified.Schema.*` / `M:Reified.Constraint.*`.
- Generated reference pages re-slug; they are gitignored, so regenerate rather than edit. Run `dotnet livedocs build`
  after namespace changes so generated API links and guide links are checked together.

## Surprises worth keeping

- **MSBuild caches `UsingTask` assemblies in reused nodes.** After changing the contract emitter, the
  checked-in generated output regenerates from the *old* task assembly until the build server is shut
  down. `MSBUILDDISABLENODEREUSE=1` or `dotnet build-server shutdown` first, or you will chase a
  compile error that the source no longer explains.
- **Merging namespaces made two union cases ambiguous.** `Data`'s `DataPatternNode` has `Exact` and
  `OneOf`; so do `Cardinality` and `Membership`. The .NET compiler picked the intended one silently;
  Fable did not. Both are now written qualified. Fable earns its place in the validation set here —
  it was the only check that caught this.
