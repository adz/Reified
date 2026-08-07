# Documentation Information Architecture

Status: outstanding work. The repository split, the `Reified.*` rename, the package shape, and the umbrella
package are done and recorded in `dev-docs/decisions/README.md`. What remains is the documentation
reorganisation below, plus the FsLiveDocs changes it depends on.

**Done so far (no FsLiveDocs dependency):**

- `docs/getting-started.md` — the repository-wide getting started of §3, at `/getting-started/` and first in
  the top nav. One complete transaction (model → declaration → realistic input → typed value → accumulated
  path-aware failures → derived JSON codec), then the widening sequence, then routing by next task. Its code
  is compiled and run in CI from `examples/Reified.GettingStarted`, and the failure text on the page is that
  program's real output.
- `docs/index.md` — landing page reworked per §2 rule 2 and §4: hero, one compact example, a single primary
  `Get started` action, then symptom routes. The package-card door grid is gone; package inventory moved to
  the getting-started page's *Installing* section.
- `site/data/sidebars/gettingstarted.yaml` — the getting started is not a product area, so its sidebar routes
  onward to the four areas rather than listing pages beneath itself.

Still outstanding here: the per-package `getting-started.md` pages (§6) have not been demoted to quickstarts,
so `/getting-started/` and `/schema/getting-started/` both carry that title. The task-folder tree of §2 is
still blocked on FsLiveDocs items 1–4.

Folder name is the section name, so the IA is expressed by naming folders after reader tasks. Numeric
prefixes order them and are stripped from URLs.

The first five minutes are governed separately from the full information architecture. Flame's NuGet
README is the useful reference: it states the job in one sentence, installs, defines an ordinary F# record,
builds one schema, parses realistic JSON, uses the typed success value, and shows the failure shape before
expanding into rules or a catalogue. Its later README mixes tutorial, reference, internals, and benchmarks;
that is not the part to copy. Copy the speed and concreteness of the opening transaction.

## 1. Positioning rules

1. **Complete one realistic transaction before explaining the architecture.** The reader must see familiar
   input become a useful typed result before meeting the full vocabulary behind it.
2. **One route is visually dominant.** The landing page has one primary `Get started` action. Package
   matrices, overview tours, reference-app walkthroughs, and API reference must not compete with it.
3. **Show the payoff beside the declaration.** Do not ask the reader to carry boilerplate for several
   sections before showing what that declaration replaces or derives.
4. **Name concepts after the reader has observed them.** Terms such as interpreter and refinement explain
   behaviour already seen; they do not precede the first working example.
5. **Move catalogues out of the opening path.** Complete rule lists, parser tables, package inventories,
   performance details, AOT notes, and implementation paths belong in task guides, Reference, or Notes.
6. **Route by symptom, not by category.** Every landing-page row states a problem the reader has, not a
   feature the library has. That is the positioning rule, not a stylistic preference.

## 2. Task folders — `./docs`

```
01-getting-started/
02-how-it-compares/          FluentValidation, DataAnnotations, Validus, Thoth,
                             FsToolkit, System.Text.Json
03-validating-values/        rules about a value
04-domain-types/             types that carry their rule
05-parsing-input/            Parse — decoding serialized primitives
06-modelling/                Schema — declaring a model
07-json/                     Schema.Json — codecs
08-http-contracts/           Schema.Http — endpoint declarations, OpenAPI output,
                             and how to serve them on plain ASP.NET
09-testing/                  Schema.Testing, Data — fixtures and test cases
10-notes/                    benchmarks, AOT and trimming detail
```

## 3. Getting started

Declaration shaped: declare once, then derive. Lead with the concept; it sells when explained.

The page opens with one complete boundary transaction, not a definition of `Schema<'model>` and not the
package graph:

```text
ordinary F# model
    → explicit schema declaration
    → realistic untrusted input
    → typed success or path-aware accumulated errors
    → one derived output from the same declaration
```

Use an ordinary application shape such as signup, checkout, or configuration. Show the model, declaration,
input, `Schema.parse`, the successful value being used, and two failures together. Then derive exactly one
second artefact — preferably the JSON codec or JSON Schema — so the declaration pays for itself on the same
screen. Reified deliberately uses an explicit, reflection-free declaration rather than Flame's
`Schema.fromType`; the prose and example sequence must therefore make the return on that declaration
immediate. The first impression must not be "repeat the record fields and constructor now; learn why later."

After that completed transaction, widen from one value rule to the whole story:

1. State the problem: the same rule is restated in a parser, a validator, a form, and a test.
2. Install only the package used by the example; defer the package matrix.
3. Declare one constraint and check a value with it.
4. Attach the declaration to a type so downstream code does not re-check it.
5. Return to the opening model and show how its fields consume the same declarations.
6. Derive the remaining codec, contract, and test fixtures from the model declaration.
7. Explain failures: an ordinary F# `Result` carrying a `Violation` or `ParseError` — types derived from
   the rule, carrying context worth keeping. This is where the name earns itself: a `Constraint` carries
   its `ConstraintDescription`, and a violation carries the atom and the offending value as data, so the
   rule and its message cannot drift apart.
8. Route by the reader's next task, not by package.

Do not enumerate every interpreter before the first transaction finishes. `Data`, `SchemaErrors`, paths,
codec compilation, inspection, contracts, forms, OpenAPI, AOT, trimming, and Fable are all legitimate, but
introducing them before the reader has parsed one useful model turns the getting-started into an architecture
tour. The current material can be retained by moving each explanation after the complete example or into its
task guide.

## 4. Landing page: route by symptom

Plain problem statements the destination page genuinely solves. Claim only what the library does.

These symptom routes sit below the primary getting-started action. They are for a reader who already knows
which pain brought them here, not a replacement for the single newcomer path. Do not lead the landing page
with a package-card grid: that asks the reader to understand the product decomposition before seeing the
product work. Package inventory belongs in the Packages area.

| Problem | Goes to |
| --- | --- |
| Validation boilerplate is everywhere, and invalid values still get through | `03-validating-values/` |
| The same rule is repeated in a parser, a validator, a form, and a test | `06-modelling/` |
| Decoding and validation are separate steps that drift apart | `07-json/` |
| Client and server disagree about the shape of a request | `08-http-contracts/` |
| Constructing test data by hand is slow and repetitive | `09-testing/` |
| You want one small library, not a framework | Packages index |

---

## 5. Two pages to write first

Highest leverage, and neither depends on any tooling change.

- **`03-validating-values/`** opens with the constraint-derived failure: declare the rule once, get the
  check *and* its explanation, with `Violation` carrying identities and operands rather than prose. Today
  this is stranded at `weight: 40` inside a page titled "Localization", which reads as a niche concern for
  teams shipping multiple languages, when the primary benefit — no parallel catalogue of error messages to
  keep in step — applies to everyone shipping one. Localization becomes the proof, not the headline.
  *(Started: `docs/values/constraint/_index.md` was rewritten around this claim before the split.)*
- **`02-how-it-compares/`** leads with the same claim, because it is where FluentValidation and
  DataAnnotations are weakest: both maintain rules and messages separately, so drift is guaranteed.
  *(Started: `docs/values/constraint/comparison.md` exists.)*

## 6. Also outstanding

- **141 dead cross-links.** `[text]({{< relref … >}})` renders as plain text with no anchor — verified
  against five variants: the `{{% … %}}` form, absolute links, relative links, and `relref` inside a raw
  HTML `href` all work. 141 occurrences across 53 files. Mechanical substitution, but may be moot if the
  FsLiveDocs migration lands first.
- ~~**Stop committing generated reference.**~~ Done at the split: `docs/*/reference/` is generated and
  ignored, so `./docs` holds hand-written prose only and `git log docs/` records what was written.
- **Demote per-package `getting-started.md`** to quickstarts, or delete where they duplicate.
- **Move meta pages** (`packages-and-platforms`, `benchmarks`, `aot-trimming-fable`, comparisons) out of
  the learning path into notes.

---

## 7. Site structure: three areas, and three package tiers

Guides and reference are organised on orthogonal axes — guides by reader task, reference by code structure —
so they cannot be interleaved. They are separate top-level areas, as in Effect (Docs + API Reference), Rust
(the Book + docs.rs), and Django (topics + reference).

**Top nav: Docs · Reference · Packages · GitHub.**

- **Docs** — the task folders of §2.
- **Reference** — the generated entity tree, enriched per entity by hand-written prose (below).
- **Packages** — install matrix, dependency graph, standalone badges. Kept separate from Reference because
  "what do I install" is asked far more often than "what is the signature of X", and independent
  installability is the pitch.

FsLiveDocs already separates these: generated pages live at `/api/{entityId}.html`, `collectGuideOutputs`
excludes anything under `/api/`, `xref:` resolves to `api/{id}.html`, and `View.fs` already models
`overview` / `guides` / `api-docs` areas with labels and ordering. They are hardcoded and rendered as
sidebar groups rather than a top bar; a `navItem title url` helper already exists.

**Deep reference is authored, not just generated.** `ContentProvider.applyApiDocs` reads
`docs/api/{EntityId}.md` and substitutes it for that entity's generated summary:

```fsharp
let summary = docs |> Map.tryFind e.Id |> Option.defaultValue e.SummaryHtml
```

So any namespace, module, or type can carry a full authored page keyed by its entity id. With `<example>`
blocks verified against the real assembly and `{{< snippet >}}` transclusion, reference depth lives next to
the code and cannot drift.

**Three package tiers**, distinguished by what they are rather than by convenience:

| Tier | Packages | API surface | Appears in |
| --- | --- | --- | --- |
| Core | `Result`, `Parse`, `Constraint`, `Refinements`, `Data`, `Schema` | yes | Reference, Packages |
| Schema extensions | `Schema.Json`, `Schema.Contracts`, `Schema.Http`, `Schema.Testing` | yes | Reference, Packages |
| Build tooling | `Schema.Contracts.Build` | **none** | Packages only |

`Schema.Contracts.Build` is `DevelopmentDependency=true`, `IncludeBuildOutput=false`, and compiles nothing —
it ships an MSBuild targets file and a generator. It must be excluded from Reference or it renders as an
empty entity.

Reference groups Core and Schema extensions separately. Docs does not tier at all: task folders cut across
the tiers, which is the point.

## 8. Namespace convention — settled

**Every package declares its own namespace equal to its package id.** You `open` the package you installed.
The cost is a stutter in the qualified name — `Reified.Result.Result`, `Reified.Data.Data` — which is
invisible at call sites, since consumers write `open Reified.Data` then `Data.assoc`. Accepted.

**Nothing declares into the bare `Reified` root namespace.** The root would become an unscoped catch-all
across unrelated value packages, so `open Reified` would stop telling a reader which package a name came
from. The umbrella package holds no code for the same reason.

**Package identity in the reference model (§9 item 5) is still required.** Satellites like
`Reified.Schema.Json` and `Reified.Schema.Testing` share the `Reified.Schema.*` namespace prefix, so a
namespace tree can never say which NuGet a type ships in. The reference must state it explicitly.

---

---

## 9. FsLiveDocs prerequisites

Used as an ordinary consumer. Mounts, artifact packaging, and merged symbol tables are not needed, since
there is no merged site.

1. **Preserve folder structure in output paths.** The real prerequisite. `ContentProvider.fs:272` flattens
   every page — `Path.GetFileNameWithoutExtension(f).ToLowerInvariant() + ".html"` — so files discovered
   recursively via `SearchOption.AllDirectories` all collapse to the site root. `docs/guides/foo.md` becomes
   `/foo.html`, and same-named files in different folders collide silently. `collectGuideOutputs` (:148)
   flattens identically, and `validateLinks` builds its allowed-set from those names, so link validation
   changes with this.
2. **Folder-derived sections.** `View.fs:66-90` hardcodes a `guides` mapping for section id, display name,
   and order. Derive from folder name instead.
3. **Numeric prefix stripping** for ordering, in URLs and titles.
4. **Optional `_index.md` title override** per folder, for irregular casing ("JSON", "HTTP", "F#").
5. **Package identity in the model.** `PackageModel` is `{ Version; Entities; Scenarios }` — no package
   name — and `SymbolLister.merge` flattens N packages into one entity list, rebuilding the tree from
   namespace ids alone. With ten packages in one build, the reference cannot tell a reader which NuGet a
   type ships in, and §8 shows namespace is not a reliable proxy. Carry a package name and tier through
   the merge, and display the package on every reference page.
6. **Areas as top nav, derived rather than hardcoded.** `View.fs:63-90` fixes `overview` / `guides` /
   `api-docs` with labels and ordering; derive them and render in the top bar (§7). A package with no API
   surface must be excludable from Reference entirely.

Items 1–4 are needed for the docs reorganisation. Items 5–6 are needed for the reference to be honest about
packaging, which matters more here than in a single-package project.

Do items 1–4 before migrating this repository's docs tree. Afterwards the same change churns every page.

---

---

## 10. Open Questions

1. **Is `Data` a Foundation or a Schema satellite?** It exists because building maps of lists by hand in
   tests and docs was miserable, which is a testing story. But it may be the easiest package to adopt
   first, which argues for prominence.
2. **Does a plain-ASP.NET serving path need a package?** Reified declares contracts and emits OpenAPI but
   ships no server. Write the "serve it on plain ASP.NET" page first (§2, folder 08); if the manual wiring
   turns out to be boilerplate people copy every time, it earns a package. If not, a page is the whole
   answer.
