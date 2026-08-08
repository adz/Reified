# Namespace Flatten

Goal: `open Reified` puts the core surface in scope. Each further `open` buys a capability
rather than paying a namespace tax.

| Task | Preamble |
|---|---|
| Read schemas, inspect, contracts | `open Reified` |
| Define schemas | `+ open Reified.SchemaSyntax` |
| Constraints unqualified | `+ open Reified.ConstraintSyntax` |
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

- **Keep `Syntax` in the name.** `SchemaSyntax` / `ConstraintSyntax`, not `DSL` — renaming
  the concept buys nothing and adds call sites to the sweep.
- **`field` stays at namespace level.** `ApiShapeTests.fs` pins it present on purpose:
  `field _.Email` must resolve from the same open that supplies the vocabulary.
- **`Reified.Refinements` is untouched.** `Refine` and `Refinement` stay there rather than
  moving up — 10 call sites do not justify splitting `Refine.fs`, and the split would
  disturb the modules just nested under `Refine`.
- **No shipped alias module.** A user whose domain owns a catalogue name writes their own
  local `type`/`module` abbreviation. A shipped alias would duplicate generic constraints
  across files and drift silently.
- **`Reified.Data` and `Reified.Result` keep their namespaces.** Neither is referenced by
  `Reified.Schema.fsproj`; they are genuinely separable.

## Phases

Ordered so each phase builds and tests green on its own, and so the riskiest rename
(Schema) lands after its dependency (Constraint) has proven the pattern.

- [x] **Phase 0** — Land the prerequisite renames (`SchemaPath`, `Refine.*` nesting).
- [ ] **Phase 1** — `Reified.Schema.Derive` -> `Reified.DerivedSchema`. Mandatory before
      phase 4: it is the only sub-namespace sharing `Reified.Schema.dll`, so FS0247 fires
      otherwise. Standalone and small, so it goes first.
- [ ] **Phase 2** — `Reified.Parse` -> `Reified`. Two files, two public names. The canary
      that proves the flatten end to end at minimum cost.
- [ ] **Phase 3** — `Reified.Constraint` -> `Reified`, `module Syntax` -> `ConstraintSyntax`.
      Before Schema because Schema depends on it.
- [ ] **Phase 4** — `Reified.Schema` -> `Reified`, `module Syntax` -> `SchemaSyntax`.
      The large one: 21 source files plus the satellite packages' opens.
- [ ] **Phase 5** — CE machinery into file-level `*Internals` modules with
      `EditorBrowsable(Never)` and `CompilerMessage(..., 42, IsHidden = true)`.
      `field` excluded.
- [ ] **Phase 6** — Umbrella `.fsproj` comment, docs, `llms.txt`, docgen inputs, generated
      reference pages.
- [ ] **Phase 7** — Full validation: build, tests, source inventory, Fable surface, docs.

## Sweep bill

- ~220 `open Reified.Schema|Constraint|Parse` call sites move to `open Reified`.
- `ApiShapeTests.fs` asserts on compiled type names throughout; every `Reified.Schema.X`
  and `Reified.Constraint.X` string changes.
- `scripts/docgen/Program.fs` member IDs are all `T:Reified.Schema.*` / `M:Reified.Constraint.*`.
- Generated reference pages re-slug; they are gitignored, so regenerate rather than edit.
