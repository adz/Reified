# Repository And Package Split

Status: planned work. Companion to `docs-information-architecture.md`, which owns the documentation plan.

Everything below is work to do or current state. Superseded directions are not repeated.

## Decision Summary

**Two independent projects, split on the one seam that is real — description versus execution.**

1. **Reified** (this repository, keeps its name and history) — the effect system and its satellites.
2. **Reified** (new repository, extracted) — constraints, values, schema, and data. Packages:
   `Reified.Result`, `Reified.Parse`, `Reified.Constraint`, `Reified.Refinements`, `Reified.Data`,
   `Reified.Schema` and its satellites.

There is **no third repository** and no shared documentation site. Each project has one repository, one
site, one release train. The shared thesis — Reified encodes invariants about values, boundaries, and
models; Reified encodes invariants about computation — is stated in prose on each site, not encoded in
package IDs.

Nothing has been published under either name that has users, so package boundaries and identities are free
to change today and breaking after first publish. That window governs the sequencing below.

### Why this allocation, and why it is not the earlier one

The earlier plan had Reified keeping the description side and the effect system returning to `FsFlow`. Three
things overturned it.

**`Reified` never meant anything for constraints and schema.** "Along an axis" describes nothing about
declaring a rule once and deriving a parser, a codec, a contract, and a fixture from it. The name survived
because it was there. It reads better against directed, controlled execution, which is where it now sits.

**`Reified` names the load-bearing decision on the description side.** A `Constraint<'value>` is not a
`'value -> bool`; it carries a `ConstraintDescription`, and `AtomicViolation.Expected` carries the
`ConstraintAtom` and the offending `ConstraintValue` as data rather than as rendered prose. The constructor
comment states the intent directly — *"so a primitive's identity and its failure are the same value rather
than two hand-maintained copies."* Rendering, localization, JSON Schema emission, OpenAPI, and derived
fixtures are all only possible because the rule is an inspectable value. That is reification in the strict
sense, and it is the exact claim the comparison pages make against FluentValidation and DataAnnotations,
which maintain rules and messages separately.

**The effect system is this repository's trunk.** The first commit, `d3b9617a` (2026-03-30, "Initial
Effect.FS baseline"), is the effect system. The description packages do not appear until `3a2a13f2`
(2026-06-21, "Split FsFlow into Reified packages"). So this repository *is* the Reified repository, by name and
by history, and the extraction inverts — see "Repository Extraction". That is worth real money: it removes
the risk of truncating the effect system's history across its five historical path names.

`FsFlow` was considered and rejected: 0.6 has no users beyond CI, so there is no continuity to protect, and
"Flow" reads as workflow engine (Airflow, Prefect, Camunda) for a library that is not one.

### Positioning, per product — deliberately asymmetric

The two concepts land differently when explained, so the doors are different.

**Reified leads with the concept.** People hear "the rule and its message are the same object, so they
cannot drift" and immediately understand why that is better. Say it.

**Reified does not lead with "effect system".** Most .NET developers, and many F# developers, have never used
one, and the phrase produces blank stares. Lead with *easier async and Result* — failures visible in the
signature, dependencies visible in the signature, swap them in a test — and let the reader discover they
have been using an effect system afterwards. This is the Polly precedent: an unfamiliar concept carried by
an opaque name and a concrete pitch.

## Current State

Landed in the combined repository:

- `Reified.Result` holds only general Result composition and `result { }`, plus `Accumulate.fs` (accumulating
  `result.list` / `result.array`), `Result.traverse`/`sequence`, `tap`/`tapError`, and `BindReturn`.
- `Reified.Constraint` holds the reusable path-free constraint surface and returns the standard F# `Result`,
  with no dependency on `Reified.Result`.
- `Reified.Refinements` depends only on `Reified.Constraint`.
- `Reified.Parse` is a separate leaf depending on nothing.
- `Reified.Schema` depends on Data, Constraint, Refined, and Parse directly, never on `Reified.Result`.
- `Axial.Flow` depends on no other Reified package.
- Both meta-packages are gone: `src/Reified.ErrorHandling/` and `src/Reified/`, deleted with their solution,
  pack, and docs-build entries. `Reified.ApiShape.Tests` asserts no meta-package remains in the graph.
- Per-package test projects, AOT probes, source inventory checks, and doc generator inputs track the
  focused package set.

- The two products have separate version properties. `Directory.Build.props` declares `ReifiedVersion` and
  `FsFlowVersion` — both 0.7.0 for now, which is convenience, not a rule — and selects between them with
  `IsFsFlowProject`, which is true for `Axial.Flow*` and for the two HTTP adapters. `scripts/pack.sh` takes
  `-v` for one train and `-f` for the other; it no longer has a single `-p:Version` override that would
  silently re-couple them. **The property names now point the wrong way** and are renamed with the rest:
  `FsFlowVersion` → `ReifiedVersion` (the effect system), `ReifiedVersion` → `ReifiedVersion`,
  `IsFsFlowProject` → `IsReifiedProject`. Mechanical, but do it in the rename pass rather than piecemeal.
- Package-consumer fixtures exist at `tests/package-consumers/`, run by
  `scripts/run-package-consumers.sh`: Result alone, Parse alone, Refined alone, Schema alone, and
  FsToolkit + Refined/Schema. They restore from `artifacts/package` through their own `nuget.config`
  with `<clear />`, deliberately do not inherit the repository `Directory.Build.props`, and evict the
  matching version from the global cache first, so they see the packages as an outside consumer does.
  They are not in `Reified.slnx` — they only build after a pack.

Open follow-ups, still pre-extraction:

- Repository topics and release-notes vocabulary not audited against the current package names.

## Why Split

The repository asks maintainers and coding agents to hold two unrelated vocabularies at once. Schema work
uses structured data, schema, constraint, diagnostic, refined value, parse, wire contract, codec, migration.
Flow work uses environment, effect, scope, layer, fiber, cancellation, service, host.

Separate repositories give smaller trees, one architectural plan each, shorter agent instructions,
product-specific examples and benchmarks, independent release timing, less chance of a dependency appearing
between the two cores, and clearer issue ownership.

The split is not meant to prevent integration. Integration happens through released package dependencies
and two explicit adapters.

## Terms

A **repository** is a source-control and release-workflow boundary; several packages may live in one.
A **package** is a separately referenced NuGet unit with its own public dependency graph.
A **product** is a top-level package and documentation identity presented to users.

## Package Shape Before First Publish

Free now, breaking later. Do these in the combined repository.

### Deferred: merging `Reified.Constraint` into `Reified.Refinements`

**Not decided. Constraint stays a separate package for now.**

The case for merging: `Constraint` alone is clunky in practice — check, then map or render — and anyone
going that far will likely take Schema too, so `Constraint` + `Refined` may be the real standalone unit
(domain invariants with no boundary and no serialization involved).

The case against acting yet: it is a one-way door for the vocabulary, and the clunkiness is an API
ergonomics problem that merging does not fix. Better to improve the standalone `Constraint` path first and
see whether the premise survives.

Worth knowing either way: package boundaries do not control payload. Trimming and tree-shaking work on
reachability, not package identity. What blocks shaking is module-level data — `Catalogue.entries` is one
such table — and that is unaffected by which package the code ships in. So there is no payload argument
pushing in either direction.

Revisit before first publish, since it is free now and breaking afterwards.

### Fold `Reified.Schema.JsonSchema` into `Reified.Schema`

Done. 635 lines in one file, depending only on Schema, and already declared in `namespace Reified.Schema` —
so the fold was a file move plus package deletion, with no call-site changes. JSON Schema emission is part
of "declare once, derive everything" rather than an optional extra.

`Reified.Parse` stays as it is: zero dependencies, a crisp standalone story, a self-explanatory name.

### Resulting package list — eleven, in three tiers

| Tier | Package | Depends on | API surface |
| --- | --- | --- | --- |
| Core | `Reified.Result` | — | yes |
| Core | `Reified.Parse` | — | yes |
| Core | `Reified.Constraint` | — | yes |
| Core | `Reified.Refinements` | Constraint | yes |
| Core | `Reified.Data` | — | yes |
| Core | `Reified.Schema` | Constraint, Data, Parse, Refinements | yes |
| Extension | `Reified.Schema.Json` | Schema | yes |
| Extension | `Reified.Schema.Contracts` | Schema | yes |
| Extension | `Reified.Schema.Http` | Schema | yes |
| Extension | `Reified.Schema.Testing` | Schema | yes |
| Build tooling | `Reified.Schema.Contracts.Build` | Contracts | **none** |

Down from 26 packages to 11. `Schema.JsonSchema` folded into `Schema`; `Constraint` stays separate pending
the deferred decision above.

**`Refined` becomes `Refinements`.** Plural noun rather than past participle, so `Reified.Refinements` reads
as adjective-plus-noun instead of two participles in a row, and the phrase is true — reified refinements are
what `Refinement.define` produces. It also agrees with the code, which already has `Refinement.fs` and
`Refinement<'input,'output>`, and it is no further from the refinement-types literature than `Refined` was.
The `Re-fi` stem still repeats visually; that is the accepted residual cost of the name.

The tiers are not presentational convenience. Core packages are what a reader chooses between; extensions
are added when a specific need arises; `Reified.Schema.Contracts.Build` is `DevelopmentDependency=true`,
`IncludeBuildOutput=false`, and compiles nothing — it ships an MSBuild targets file and a generator, so it
has no API and must be excluded from the reference entirely.

```bash
dotnet add package Reified.Result
dotnet add package Reified.Parse
dotnet add package Reified.Constraint
dotnet add package Reified.Refinements
dotnet add package Reified.Data
dotnet add package Reified.Schema
```

### Reified's package list

The effect system's satellites drop the `.Flow` infix, since the product is now the flow: `Reified` (core),
`Reified.Console`, `Reified.FileSystem`, `Reified.Process`, `Reified.PlatformService`, `Reified.HttpClient`,
`Reified.Hosting`, `Reified.Hosting.Browser`, `Reified.Hosting.Node`, `Reified.Telemetry`,
`Reified.Telemetry.JavaScript`, `Reified.Telemetry.Shared`, plus the two adapters below.

This **reverses the "nothing declares into `namespace Reified`" rule**, which existed to stop `open Reified`
becoming an unscoped catch-all across many unrelated value packages. Reified is now one product with one root
type, so `namespace Reified` holding `Flow<'env,'error,'value>` and the `flow { }` builder is correct:
`open Reified` gives you exactly what you installed. The rule survives in its new form on the other side —
nothing declares into `namespace Reified`.

Whether the core package is `Reified` or stays `Axial.Flow` is listed under "Choices To Resolve".

### Namespace convention — unified on A

Every package now declares its own namespace equal to its package id, and `namespace Reified` is empty.

`Reified.Data` was the last holdout. It declared `namespace Reified` with `module Data` inside, so the module
path already equalled the package id — deliberate, but it meant `open Reified` was the way to reach Data, and
`[<AutoOpen>] module DataErgonomicsHelpers` leaked from the root namespace on any such open.

What made the move work, after one failed attempt:

- `namespace Reified.Data`, keeping `module Data`. The resulting `Reified.Data.Data` stutter is invisible at
  call sites — consumers write `open Reified.Data` then `Data.assoc` — and matches `Reified.Result.Result`.
- **`module Syntax` promoted out of `module Data` to namespace level**, so it stays `Reified.Data.Syntax` and
  every `open Reified.Data.Syntax` is unchanged. Its body needed `Data.` qualification on eight helpers it
  had been reaching unqualified from inside the enclosing module.
- **`module Json` deliberately left nested**, as `Reified.Data.Data.Json`. Promoting it would put a `Json`
  module in `Reified.Data` that shadows `Reified.Schema.Json`'s `module rec Json` in the twelve files that open
  both, and `Data.Json.render` is used directly in tests and examples. It is referenced as `Data.Json`, so
  nesting costs nothing.

Consumer impact was one line each: `open Reified` → `open Reified.Data`. Ten fully-qualified `Reified.Data.X`
references became `Reified.Data.Data.X`; the CLR module types moved from `Reified.DataModule` to
`Reified.Data.DataModule`, which two ApiShape assertions name directly.

Also fixed: `tests/Reified.Schema.Tests/SchemaTestSupport.fs` declared `namespace Reified`, squatting the root
namespace from a test project. Moved to `Reified.Tests`.

**Rule going forward: nothing declares into the product root namespace.** After the rename this attaches to
`namespace Reified`, not `namespace Reified` — Reified is one product with one root type and *should* populate
its root. The mechanics above (package id equals namespace, `Syntax` promoted, `Json` left nested) carry
over unchanged under the new names; only the prefix differs.

Required regardless: the reference must state each type's package, since namespace alone cannot imply it
for satellites. See `docs-information-architecture.md` §6 item 5.

### Reserve NuGet prefixes

`Reified.*` and `Reified.*`. Two applications; one per project.

Both are verified free as of 2026-08-06: no package holds `Reified`, `Reified.*`, `Reified`, or `Reified.*`.
`Reified` is fully coined for this purpose and should reserve without argument. `Reified` is a common
technical adjective, so expect the reservation to be scrutinised more; it has no same-prefix squatters,
which is the main thing NuGet weighs.

**Trademark note, not legal advice.** `reified.net` is an active US software business, which is the highest
same-class exposure of any name considered. No practical risk to an unfunded OSS library, but run a real
clearance search before commercialising the packages that will sit on top of both products.

## The HTTP Adapters Move To Reified

`Axial.Schema.Http.AspNetCore` → **`Reified.AspNetCore`**, `Axial.Schema.Http.GenHttp` → **`Reified.GenHttp`**.

These are the only two packages that depend on both products, so they determine how expensive the split is.
The code is unambiguous about where they belong:

- `Reified.Schema.Http` — the boundary abstraction itself, 406 lines across `BoundaryInput.fs`, `Endpoint.fs`,
  and `ProblemDetails.fs` — has **no Flow dependency at all**. It describes endpoints and emits an OpenAPI
  document via `OpenApi.document : OpenApiInfo -> EndpointSpec list -> string`. Contract-first use needs no
  server.
- The Flow coupling lives entirely in the two host adapters, and there it is structural rather than
  incidental. Flow is in the public API, not an implementation detail:

  ```fsharp
  let json (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model>
  (workflow: Flow<HttpEndpointEnv<'app>, EndpointError<'error>, IResult>)
  ```

  An endpoint *is* a Flow with a request environment.

So they are Flow integrations that speak Schema, not Schema packages that happen to host. The dependency
direction inverts:

```text
released Reified.Schema.Http / Reified.Schema.Json / Reified.Data
            ↓
Reified.AspNetCore / Reified.GenHttp          (stay in this repository)
```

This points the cross-repository version range at the **stable** side instead of at Flow's moving
environment surface, and leaves Reified with **zero** cross-product edges — it builds, tests, packs, and
releases with no Reified package present.

`Reified.Schema.Http` extracts with Reified. It is already Flow-free and describes boundaries for any host.

**Naming.** `Reified.AspNetCore` and `Reified.GenHttp` sit at top level as siblings of `Reified.HttpClient` — the
client-side counterpart — rather than under `Reified.Hosting.*`, which is about application lifecycle and
platform host. Worth a second look during implementation; both readings are defensible.

**What the move touches** — note that with the inverted extraction these two projects **stay put** and it is
`Schema.Http` that leaves, so this is a rename rather than a move:

- `src/Reified.Schema.Http.{AspNetCore,GenHttp}/` — project rename, `PackageId`, `RootNamespace`,
  `AssemblyName`, and the module namespaces in `AspNetCore.fs` / `GenHttp.fs`.
- `tests/Reified.Schema.Http.Tests/` — holds `AspNetCoreAdapterTests.fs` and `GenHttpAdapterTests.fs`
  alongside the Schema.Http tests. Split; the Schema.Http tests go to Reified, the adapter tests stay.
- `docs/schema/http-servers.md` and the generated reference trees under
  `docs/schema/reference/schema/http/{aspnetcore,genhttp}/` (~35 pages) — the adapter pages stay, the
  contract-declaration pages go to Reified.
- `examples/Reified.Api` and `examples/Reified.Api.GenHttp` stay with the adapters.
- `scripts/pack.sh`, `scripts/docs-build.proj`, `scripts/docgen/Program.fs` symbol ids, and `Reified.slnx`.

Do the rename **before** extraction, while it is a single-repository refactor with the compiler checking
every call site.

## Repository Extraction

### Method — inverted, because the effect system is this repository's trunk

Use `git filter-repo` — not `filter-branch`, which is deprecated, and not a fork, which carries every
unrelated file's history forever and is marked as a fork by GitHub. Not currently installed:
`sudo dnf install git-filter-repo` (Fedora packages it; no `pip` needed).

**Which side gets filtered is decided by history, not by preference.** Verified:

```
flow lineage    first commit  d3b9617a  2026-03-30  "Initial Effect.FS baseline"     171 commits
values lineage  first commit  3a2a13f2  2026-06-21  "Split FsFlow into Reified..."     189 commits
```

The effect system *is* the trunk — it is the initial commit. The description packages do not exist until
2026-06-21. So:

- **Reified** — keeps this repository, its name, its full history, its tags, and its GitHub identity. Removing
  the description paths is an ordinary `git rm` commit. **Do not `filter-repo` this repository**: it rewrites
  every SHA, breaks existing clones and branches, and orphans the v0.6.0 objects that published FsFlow 0.6
  sourcelink still resolves into via the `adz/FsFlow` → `adz/Reified` redirect.
- **Reified** — fresh `git clone --no-local` into scratch, filter down to the description paths, push to a
  new empty repository.

**This inversion is the single biggest risk reduction in the plan.** The earlier direction filtered the
effect system, whose paths have been renamed four times — `src/EffectFs` → `src/EffectfulFlow` →
`src/FlowKit` → `src/FsFlow*` → `src/Axial.Flow*`. Since `filter-repo` matches path strings per commit and
**does not follow renames**, filtering on the `Axial.Flow*` names alone would have produced a repository
whose history began 2026-06-21 and **omitted the v0.6.0 tag entirely** (`git ls-tree v0.6.0 src/` is all
`src/FsFlow*`). That loss is unrecoverable once the original is gone. Filtering the description side has no
such exposure: its whole history is six weeks under one `Reified.*` family.

### Paths to extract into Reified

```
src/Reified.Result                    src/Reified.Schema
src/Reified.Parse                     src/Reified.Schema.Json
src/Reified.Constraint                src/Reified.Schema.Contracts
src/Reified.Refinements                   src/Reified.Schema.Contracts.Build
src/Reified.Data                      src/Reified.Schema.Http
                                    src/Reified.Schema.Testing

tests/Reified.Result.Tests            tests/Reified.Schema.Tests
tests/Reified.Constraint.Tests        tests/Reified.Schema.Json.Tests
tests/Reified.Refinements.Tests           tests/Reified.Schema.Contracts.Tests
tests/Reified.Data.Tests              tests/Reified.Schema.Testing.Tests
                                    tests/Reified.Schema.Http.Tests  ← split; adapter tests stay

docs/schema  docs/values  docs/result  docs/data
benchmarks/Reified.Schema.Benchmarks
```

**Historical names on this side too — still use `--path-glob`.** The description lineage is shorter but not
flat: `src/Reified.Validation`, `src/Reified.Validation.Schema`, `src/Reified.Check`, `src/Reified.Codec`,
`src/Reified.Diagnostics`, `src/Reified.ErrorHandling`, `src/Reified` and the matching `tests/Reified.*.Tests` all
appear before the current names settle. They are all under `Reified.*`, so one glob family covers them, but
enumerate rather than assume. Verify with `git log --oneline | wc -l` on the filtered result against the
189 commits that touch the description lineage before pushing anywhere.

**Needs individual inspection before assignment — resolved.** Every example, benchmark, and cross-cutting
test project classified by its actual project references:

| Destination | Projects |
| --- | --- |
| **Reified** — stays, Flow-only | `Reified.App.Example`, `Axial.Flow.AotProbe`, `Axial.Flow.Comparisons`, `Axial.Flow.PlatformService.Examples`, `Reified.Hosting.Browser`, `Reified.Hosting.Desktop`, `Reified.Hosting.GenericHost`, `Reified.Hosting.Node`, `benchmarks/Axial.Flow.Benchmarks` |
| **Reified** — stays, adapter examples | `Reified.Api`, `Reified.Api.GenHttp` (no direct Flow reference, but they consume the AspNetCore/GenHttp adapters, which stay) |
| **Reified** — stays, with released Reified deps | `Reified.Hosting.DotNet` (Flow.Hosting plus incidental Parse and Refinements) |
| **Reified** — extracts | `Reified.Constraint.AotProbe`, `Reified.Refinements.AotProbe`, `Reified.Result.AotProbe`, `Reified.Schema.AotProbe`, `Reified.ReferenceApp.Intro`, `Reified.ReferenceApp.Wire`, `benchmarks/Reified.Schema.Benchmarks` |
| **Must split in two** | `Reified.Examples`, `Reified.MaintenanceExamples`, `Reified.Playground`, `Reified.ReadmeExample`, `benchmarks/Reified.Benchmarks.Fable`, `tests/Reified.ApiShape.Tests` |
| **Integration app** | `Reified.ReferenceApp` — consumes both sides plus both adapters. Becomes the integration application against released packages, per "Examples And Reference Applications". |

The six in "must split" each hold content from both products in one project. `Reified.ApiShape.Tests` is
the most consequential: it asserts package layout across both products, so each repository needs its own
copy asserting only its own packages.

**A seventh: `tests/Axial.Flow.Tests`.** Found by the phase 6 verification — see "On phase 6" below. Its
runnable-example-docs test asserted both products' docs pages. Already split; listed here so the count
is right.

### Order

Keep history extraction separate from semantic refactoring. Do not combine filtering, renaming, API changes,
and optimization into one opaque migration.

1. Establish a final commit in the combined repository.
2. Create Reified from filtered history; verify tags, authors, and retained files.
3. Confirm the extracted repository builds and its tests pass standalone.
4. Make path and package renames in normal commits, **one per repository against a tree containing nothing
   else**: `Reified.*` description packages → `Reified.*` (with `Refined` → `Refinements`) in the new
   repository; `Axial.Flow*` → `Reified*` and the two adapters → `Reified.AspNetCore` / `Reified.GenHttp` here.
5. Update build, CI, docs, and release configuration; install maintainer files.
6. Publish prerelease packages from each repository.
7. Run consumer and integration tests.
8. Remove the description paths from Reified only after Reified is usable.

## Versioning After The Split

Each repository has an independent release train. Packages within a repository may share one version while
coordinated releases remain convenient. Do not require the two repositories' versions to match — a tag such
as `v0.8.0` in Reified says nothing about Reified. Both sit at 0.7.0 today by convenience, not by rule.

The Reified HTTP adapters should float within Reified's current pre-1.0 minor — `0.7.*` on
`Reified.Schema.Http`, `Reified.Schema.Json`, and `Reified.Data` — rather than pinning a closed range. While
those shapes are still moving, a float surfaces breaks in the adapter's own CI instead of at release time.

Two mechanical consequences to handle rather than inherit:

- NuGet resolves a float at **pack** time and writes it into the nuspec as an open-ended minimum, so
  consumers would see `>= 0.7.3` with no upper bound. Set an explicit upper bound in the packed metadata;
  the float is for the adapter's build, not for downstream constraints.
- Floating restores are not reproducible. Enable `RestorePackagesWithLockFile` so CI pins what it built.

Release notes describe only the packages in that repository.

## Testing Across Repositories

Each repository owns complete tests for its own public behavior.

Reified must test Result, Parse, Constraint, Refinements, Data, Schema, formats, and contract tooling —
with no Reified package anywhere in the tree.

Reified must test Flow, its services and hosts, and the two HTTP adapters. Only the adapter tests may
reference Reified, and only as released packages.

Package-consumer tests already exist at `tests/package-consumers/` — see "Current State". They extract with
Reified; Reified needs its own equivalents for the effect system's packages.

Cross-product CI lives in Reified and should cover the current released Reified packages against the adapters,
the lowest Reified version the adapters claim to support, and the integration reference application against
released packages. Do not make either core repository's ordinary pull-request build depend on the other
repository's main branch.

## Examples And Reference Applications

Move product-specific examples with their repository. Reified examples cover input sources, private domain
construction, diagnostics, JSON, contracts, migrations, HTTP contract declaration, and property testing.
Reified examples cover dependencies, errors, cancellation, scopes, layers, concurrency, services, and hosts.

The combined reference application becomes either a separate integration repository consuming published
packages, or a small integration application in one repository consuming the other's published packages. A
separate repository gives the cleanest consumer test. It must not become a source dependency of either
product, and must not require unreleased source from both for routine builds.

## Repository-Specific Maintainer Files

Each repository needs its own `AGENTS.md`, `dev-docs/AGENT_INDEX.md`, `PLAN.md`, `TASKS.md`, `DOCS.md`,
`decisions/README.md`, `README.md`, release notes, source inventory, CI workflows, and package and
documentation scripts.

Remove instructions for the other product; do not copy the whole plan into both. Reified's agent index should
explain generated contract paths and documentation generation. Reified's should explain runtime, service
packages, hosts, platform targets, and effect-boundary rules.

Reified inherits this repository's files and must have the description-side instructions **removed**; Reified
starts from the extracted copy and must have the effect-system instructions removed. Neither should be
written from scratch.

## Release And CI

Each repository owns its version source, packing and signing, release tags and notes, source-link repository
URLs, NuGet publishing, documentation deployment, API compatibility checks, and the NativeAOT, trimming, and
Fable checks relevant to its packages.

Update repository URLs and source-link metadata before publishing from the new location.

## Implementation Sequence

| Phase | Work | Status |
| --- | --- | --- |
| 1 | Fold `Schema.JsonSchema` into `Schema` | **done** — 548a7b84 |
| 2 | Unify the namespace convention; empty the product root namespace | **done** — f3ab2d46, then completed here |
| 3 | Resolve the extraction path list and every ambiguous project | **done** — f3ab2d46, revised here for the inverted direction |
| 4 | Stop committing generated reference and `site/content` | **done** — 8d574579 |
| 5 | Split the version property in two; create package-consumer fixtures | **done** — ddcd9640 |
| 6 | Verify the effect system builds and tests with no description source present | **done** — with one caveat below |
| 7 | Split the six "must split in two" projects, plus `Axial.Flow.Tests` | not started — **now a prerequisite for phase 8**, see the caveat |
| 8 | `filter-repo` into Reified; install maintainer files and CI; confirm green | blocked — needs `git-filter-repo` installed and the target repository created |
| 9 | Rename in each repository: `Reified.*` → `Reified.*` there, `Axial.Flow*` → `Reified*` and the adapters here | not started — see below |
| 10 | Publish prerelease Reified packages; run adapters against them | not started |
| 11 | Remove the description paths from Reified | not started |
| 12 | Documentation work — see `docs-information-architecture.md` | in progress |
| 13 | Prefix reservations for `Reified.*` and `Reified.*`; publish Reified, then Reified | not started |

Phases 1–4 were much cheaper in the combined repository, with the compiler checking every call site.

**On phase 6.** Verified by copying only `src/Axial.Flow*`, `tests/Axial.Flow*`,
`examples/Axial.Flow.Comparisons`, and `benchmarks/Axial.Flow.Benchmarks` into a scratch tree with no
description source and building it standalone: **build succeeded, 258 of 259 tests passed.** No Flow project
holds a `ProjectReference` to a description project, and no Flow source file opens `Reified.Result`,
`Reified.Parse`, `Reified.Constraint`, `Reified.Refinements`, `Reified.Data`, or `Reified.Schema`. The core seam is
genuinely clean.

The single failure was `Axial.Flow.Tests` asserting that **both** `docs/schema/examples.md` and
`docs/flow/examples.md` regenerate from `scripts/generate-example-docs.sh` — a cross-product test in a
Flow project, and the only coverage the schema page had. Now split: the Flow test passes `flow` and
asserts only the flow page, and `tests/Reified.Schema.Tests/ExampleDocsTests.fs` passes `schema` and
asserts only the schema page. `runBashScript` grew a `runBashScriptWithArguments` form to carry the
product argument. So **`tests/Axial.Flow.Tests` belongs on the "must split in two" list**, which it was
not on.

**Caveat, now promoted to phase 7.** The flow half of that generator renders its page from
`examples/Reified.Examples`, `examples/Reified.Playground`, and `examples/Reified.MaintenanceExamples` — all three
on the "must split in two" list, and between them they reference `Reified.Result`, `Reified.Constraint`,
`Reified.Refinements`, `Reified.Parse`, `Reified.Schema`, and `Reified.Data`. Under the inverted extraction those
examples **stay here**, so it is now *Reified's* docs test that cannot go green until they are split or
repointed at released Reified packages. Do it before extraction, while both halves are still in one tree and
the compiler can check the split.

**On phase 9.** The renames are deliberately *after* extraction, and there are now two of them, one per
repository, each against a tree containing nothing else. Doing either before the split would leave
half-renamed packages sitting beside the other product's docs, sidebars, and validation scripts — the
confusing intermediate state the ordering exists to avoid. Nothing is blocked by deferring: phases 5–7 do
not depend on either name.

Rough size, measured before the naming decision: the effect-system rename alone touched 1,011 occurrences
across 212 files, down from 2,330 across 1,416 before phase 4 removed the generated trees. Expect the
description-side rename to be comparable.

## Acceptance Criteria

- Reified builds, tests, packs, and releases with no Reified package anywhere in the tree.
- Reified's core builds, tests, packs, and releases without checking out Reified.
- `Reified.AspNetCore` and `Reified.GenHttp` consume released Reified packages and are the only packages in
  either repository depending on both products.
- `Reified.Result`, `Reified.Parse`, `Reified.Refinements`, and `Reified.Data` are independently installable.
- `Reified.Schema` depends on Data, Refinements, and Parse directly; Reified's core depends on none of them.
- A clean consumer can install and run each product from published packages.
- The integration reference application works against published versions.
- Repository instructions contain no stale paths or rules from the other product.
- Release tags and notes no longer imply synchronized versions.
- `git log` in each repository reads as that product's history; Reified's still reaches `d3b9617a`.

## Risks And Mitigations

**Cross-repository changes become slower.** Keep the core seam small, use package dependency ranges, test
adapters against released versions.

**The adapters lag behind Reified.** Float `0.7.*` so Reified's CI builds them against current Reified on every
run, and treat an adapter break as a signal about the Reified surface rather than only as adapter maintenance.

**History extraction obscures changes.** Extract first; make semantic changes in later ordinary commits.

**`filter-repo` silently truncating the extracted history.** It does not follow renames. Enumerate the
historical `Reified.*` description path names, and check the filtered commit count against 189 before pushing.
Keep the unfiltered scratch clone until the new repository is verified.

**FsLiveDocs is on the critical path for documentation.** It is not on the critical path for the split.
Phases 1–11 need nothing from it.

## Choices To Resolve During Implementation

- Whether Reified's core package is `Reified` or stays `Axial.Flow`. `Reified` is the natural reading now that the
  product is the flow, and it makes `open Reified` give exactly what was installed — but it is a larger change
  and interacts with the namespace rule above.
- Final GitHub repository name and documentation URL for Reified. Reified keeps this repository.
- Whether the integration reference application gets its own repository.
- Whether packages within a repository continue sharing a version after 1.0.
- The minimum Reified version supported by the Reified HTTP adapters.
- Whether the adapters sit at `Reified.*` or under `Reified.Hosting.*`.
- Where `benchmarks/Reified.Benchmarks.Fable` lives.
- Whether to request a transfer of the unlisted `Flow` package ID. Low value now that the product is named
  Reified; previously listed as an FsFlow-era option.

None requires returning to one combined repository.
