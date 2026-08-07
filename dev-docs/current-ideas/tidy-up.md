# Documentation tidy-up

Status: worked through on 2026-08-08. Each section below is marked **Done** or **Open**; the open ones are the
items that need a decision or a consumer, not the ones nobody got to.

## Aim

Make the shortest useful path obvious, describe adapter and error behaviour precisely, and keep the documentation aligned
with Reified's current two-door direction:

- use plain `Result` for small, application-owned failures;
- use `Schema` when structured input must become a domain model with accumulated, path-aware diagnostics.

`Data`, `Constraint`, `Parse`, and `Refinements` remain focused capabilities used by those paths. Their pages should still
stand alone, but should point to the domain-model path instead of leaving the reader at an intermediate representation.

## Notes from the review

### Data conversion adapters need executable input/output examples

**Done.** `docs/data/converting-data.md` now documents the CLI, configuration, and name/value grammars with worked
input and output, and ends by handing off to `Schema.parse`.

`docs/data/converting-data.md` lists `Data.ofCliArgs`, `Data.ofConfiguration`, and related adapters, but the table does not
explain their accepted grammar or resulting `Data` shape.

Add examples and explicit rules for:

- `Data.ofCliArgs` takes `seq<string>` and returns a `Data.Object`.
- It recognizes `--name value`, `--name=value`, `-n value`, `-n=value`, flags, `--no-name`, repeated options, positional
  arguments, and the `--` terminator.
- A following token is consumed as an option value only when it does not start with `-`; otherwise the option becomes the
  text value `"true"`. Positional arguments are collected under `_`. Repeated options become a `Data.List`.
- All CLI leaves are text. `Data.ofCliArgs` identifies structure; the eventual `Schema` parses `"42"`, `"true"`, and other
  serialized primitives into typed values.
- `Data.ofConfiguration` takes flattened `seq<string * string>`, splits colon-delimited paths, treats numeric segments as
  list indexes, keeps leaves as text or null, and applies pairs in order with later values winning. Repeated keys do not
  form lists; indexed path segments do.
- `Data.ofConfigurationPairs` has the same semantics but accepts the `KeyValuePair<string,string>` shape returned by
  `.NET IConfiguration.AsEnumerable()`.
- `Data.ofNameValues` has different repetition semantics: repeated names become lists.

End the conversion section with the useful destination: adapters produce source-neutral `Data`; `Schema.parse` maps that
tree into a domain type and reports field paths. Link directly to the smallest relevant Schema example. Data should not
own domain mapping, but its guide should not leave the reader wondering how to get there.

### Data matching should be checked against the Constraint vocabulary

**Done — settled as "keep them separate".** `Reified.Data` is a dependency-free leaf, and accepting `Constraint<'value>`
would make it depend on `Reified.Constraint` for something only test code wants. A pattern asks about a `Data` shape;
a constraint asks about an extracted typed value. Recorded in `dev-docs/decisions/README.md` and explained on
`docs/data/how-to-test-produced-json.md`. `anyText`, `anyNumber`, and `satisfying` stay.

The matching API has parallel shape predicates (`anyText`, `anyNumber`, `satisfying description predicate`) even though
Reified already has a reusable `Constraint<'value>` vocabulary. Explore the most ergonomic way to reuse constraints in
matches without confusing a `Data` shape check with a typed value rule.

Questions to settle:

- Should matching accept `Constraint<Data>`, or should adapters extract a typed scalar and then apply
  `Constraint<string>`, `Constraint<int>`, and so on?
- Is the useful form a general pattern constructor, typed constructors such as `textSatisfying constraint`, or implicit
  conversion from a constraint where the type is unambiguous?
- What mismatch should be reported when extraction fails, and how should a structured `Violation` appear beside the
  existing `DataMismatch` description?
- Can `anyText` and `anyNumber` remain convenient shape patterns while constraint-backed patterns cover value rules?

Do not add a second constraint catalogue to Data. Reuse `Constraint<'value>` or keep the APIs deliberately separate.

### Teach the lightweight Constraint-to-application-error path first

**Done.** The Constraint landing page, the comparison page, and the tutorial all now show `Constraint.guard` +
`Result.orError` before the structured path, and say the choice is per call site.

Constraint introductions currently lead with `Violation` rendering and the comparison page concedes that a predicate is
simpler for one-off checks. Show `orError` early so the small-code path is compared fairly:

```fsharp
raw
|> Constraint.guard Constraint.email
|> Result.orError InvalidEmail
```

This deliberately discards the `Violation` when the application only needs its own error case. The wording is direct,
the amount of code is comparable to Validus, and the same constraint remains reusable if the application later needs a
schema or refinement. Follow with `mapError InvalidEmail` as the structured-diagnostic path, rather than making it the
only introductory style.

Update the Constraint landing page, comparison page, and tutorial together so they do not teach different answers to
"I only need a function returning my error type."

### Result error accumulation should hand off to Schema

**Done.** `docs/result/collecting-errors.md` names Schema as the default next step for structured input, keeps the
manual option for callers staying at the Result layer, and states the `result.list` / `Schema.parse` split.

`docs/result/collecting-errors.md` ends by telling readers to carry field-name/error pairs themselves. It should instead
name `Schema` as the default next step when the independent results are fields of structured input and the caller needs
paths, raw-value redisplay, localization, or a domain constructor. Retain the manual-pair option for callers intentionally
staying at the Result layer.

The page should preserve the distinction:

- `result.list { ... and! ... }` accumulates one independent binding group;
- `Schema.parse` owns structured boundary accumulation and path-aware diagnostics.

### Decide whether Result needs an accumulating traversal

**Done — added.** `Result.traverseAll` and `Result.sequenceAll` return `Result<'output list, 'error list>`. Every
mapping runs, in input order; errors come back in input order; the sequence is enumerated once; nothing is flattened;
`traverse` was not overloaded. Documented in `collections.md` and linked from `collecting-errors.md`.

`Result.traverse` and `Result.sequence` stop at the first error. There is no ordinary-sequence counterpart that runs every
mapping and collects every failure, although `NonEmptyList.traverseResult` and `NonEmptyArray.traverseResult` already do
accumulate error lists.

Consider `traverseAll` / `sequenceAll` (names to confirm against common F# vocabulary), returning something like
`Result<'output list, 'error list>`. Specify evaluation order, error order, treatment of lazy sequences, and whether an
input mapping that already returns `'error list` is flattened. Avoid overloading `traverse` with different semantics.
If the API is added, connect it from both `collections.md` and `collecting-errors.md`.

### Parse should lead with `ParseError`

**Done.** The page opens on the three cases with a table, then the parser catalogue, then missing/malformed/
out-of-range examples, then the hand-off to application errors and to Schema.

The Parse page currently starts with four successful examples and only describes its errors in a sentence. Make
`ParseError` the central contract:

- preamble: Parse changes serialized text into primitive typed values without losing why conversion failed;
- a table of every `ParseError` case, its meaning, and representative input;
- a compact parser catalogue showing result type and lexical rules;
- examples for missing, malformed, and out-of-range input before optional helpers;
- a clear hand-off to application errors with `Result.mapError` / `orError`, and to Schema when the text is one field of
  structured input.

### Correct the trimmed-concatenation claim

**Done.** The false closure claim is gone from both pages. The real reason is stated: no later operation becomes total
or loses a branch once a string's ends are known to be clean, so the wrapper would only ever be unwrapped. Slug is
called out separately.

`docs/values/refined/_index.md` and `docs/values/refined/catalog.md` say that concatenating two trimmed strings is not
trimmed. That is false: if neither input starts or ends with whitespace, their concatenation also has no leading or
trailing whitespace.

Replace this rationale with the actual design reason for keeping trimmed text as a constraint rather than a refined type.
If there is no stronger reason, reconsider whether the catalogue decision follows the documented admission test. Slug is
a separate case: concatenation can violate its pattern, depending on the grammar.

### Replace the “Where the invariant pays” example

**Done.** The section now opens on `List.max` versus `NonEmptyList.max` and keeps the average as the second, deeper
example.

The refined landing page's average example is visually heavy because it combines `map`, `reduce`, length conversion, and
division. Use a smaller partial-to-total contrast first, such as `List.max` returning an option versus
`NonEmptyList.max` returning a value. Keep order totals as the deeper linked example.

### Link “portable constraint” at first use

**Done.** "Portable" as an unexplained quality adjective is gone. Pages now say "interpreted" and link to
`values/constraint/constraints.md`, or say plainly what Reified can and cannot represent. "Portable" survives only
where it means cross-platform.

Several pages use “portable constraint” without context. At first use, link to the interpreted/opaque explanation and
state the operational meaning: Reified can inspect the built-in atom, so an interpreter may export or generate only what
it can enforce honestly. Avoid using “portable” as an unexplained quality adjective.

### Split localization into a short guide and reference material

**Done.** `docs/values/constraint/localization/` is now a section: a short `_index.md` (English default, a four-key
map, `message` vs `fullMessage`, and the recipes, including why translating the rendered English string is the worse
option), plus `context-and-fallback.md`, `custom-rules.md`, `advanced-rendering.md`, and `catalogue.md`. The `schema.*`
catalogue moved to `docs/schema/redisplay-and-field-errors.md`. No culture or translation state entered `Violation`.
The existing recipes were judged sufficient; no new edge API was added.

`docs/values/constraint/localization.md` combines the ordinary workflow, Schema-specific rendering, advanced resolver
internals, group algorithms, the complete Constraint catalogue, and the Schema catalogue. It is too long and makes a
Constraint reader absorb Schema details.

Create a localization section under Constraint/Values:

- **Start here:** English rendering, a four-key/small-map example, `message` versus `fullMessage`, custom constraints, and
  simple edge customization.
- **Context and fallback:** context, attribute nouns, resource lookup, and field overrides.
- **Custom rules:** `customLocalized` / `customLocalizedWith`, including the required English fallback.
- **Advanced rendering:** resolvers, formatting, group traversal, plural behavior, and Fable differences.
- **Catalogue reference:** generated or table-focused Constraint keys and arguments.

Move Schema error rendering and the `schema.*` catalogue to Schema documentation, linking back to the generic Renderer
mechanics.

Add simple recipes for applications that do not need the full tree:

- omit actual values by overriding `constraint.actual` with `{message}`;
- translate only four known constraint identities with `Renderer.ofLookup` and let missing entries fall back to English;
- translate an authored custom rule with `Constraint.customLocalized`;
- explain the trade-off in rendering an English string and then translating that whole string: it is possible as an
  application edge transform, but loses stable identity and operand structure, so identity-based lookup is safer.

Decide whether those recipes are sufficient or whether a small edge API is missing. Do not put culture or translation
state into `Violation`.

### Explain unsupported operands in user language

**Done.** `constraints.md` now has "Operands Reified cannot describe": a worked custom comparable type, and the three
consequences stated separately. The default messages no longer leak internal vocabulary — "must be at least the
required value" rather than "failed an at-least rule whose operand has no portable representation". Authored fallback
prose was not made mandatory; `Constraint.custom` already covers it and the page points there.

The phrase “operand has no portable representation” is undefined at the point it appears, gives no concrete examples,
and produces technical default messages such as “failed an equality rule whose operand has no portable representation.”

Explain that a typed rule can still execute while Reified cannot convert its comparison value into the closed
`ConstraintValue` data model used for inspection and rendering. Show at least a custom comparable type and state the
consequences separately: checking works; export cannot name the value; a built-in derived message cannot safely print it.

Review the default user-facing wording. Prefer plain fallback prose that does not expose internal portability language,
while keeping technical detail available through inspection. Alternatively require authored fallback prose when an
operand cannot be represented; evaluate the ergonomic and compatibility cost.

### Clarify Schema refinement staging

**Done.** `docs/schema/syntax.md` opens the field block with the pipeline diagram and a preserve/change table, says
why raw constraints precede `refine` and how the getter fixes the final type, fixes the duplicate numbering, and links
to the refined-schema walkthrough.

`docs/schema/syntax.md` says “refinement changes the stage” but does not establish the stage model before using it, and its
field-block list has duplicate numbering. Explain a field block as a typed pipeline:

```text
Data field -> Schema<string> -> raw constraints -> Schema<Email> -> domain validation
```

State which operation preserves or changes the current value type, why raw constraints must precede `refine`, and how the
getter fixes the required final type. Link the syntax page to the expanded refined-schema walkthrough and keep the shorter
page focused on syntax.

### “Values” references are mostly legitimate, but distinguish navigation from a package

**Done.** Audited: no install snippet, namespace example, or prose implies a `Reified.Values` package or namespace, and
the three places that could be read that way already say so explicitly.

The folder and getting-started references to Values are acceptable when they clearly mean the documentation grouping.
Continue auditing install snippets, namespace examples, and prose so none implies a `Reified.Values` package or namespace.

## Other similar issues found in the repository-wide audit

These are candidates to add to the tidy-up, pending scope agreement.

### Stale Flow references and broken ownership boundaries

**Done.** Every user-facing Flow reference is either rewritten as an external hand-off to Axial or removed where
effects were irrelevant. The unreferenced `flow-graphic.png` was deleted.

Several user-facing pages still present Flow as part of Reified or link to the removed `/flow/` documentation:

- `docs/result/fstoolkit-comparison.md` says Reified separates five roles and maps async Result builders to `flow { }`.
- `docs/result/llms.txt` calls Flow a peer product.
- `docs/schema/_index.md` and `docs/schema/tutorials/_index.md` link to `/flow/` paths that no longer exist.
- `docs/schema/llms.txt`, `docs/values/reference-app.md`, and schema pattern pages describe adding or using Flow without
  consistently identifying it as an Axial capability in another repository.

Rewrite these as an explicit external hand-off to Axial, or remove them where effects are irrelevant. Check the old Flow
logo assets under `docs/content/img/` before deleting them; they may now be unreferenced.

### The landing page conflicts with the “two doors” direction

**Done.** The effects tagline is gone, the page now states the two doors above the route list, and the routes lead with
Schema and Result. Data and Values stay below them as product navigation.

`docs/index.md` presents four equal doors and its rotating taglines still include effects. This conflicts with
`dev-docs/TASKS.md`, which says the newcomer story has exactly two front doors: plain Result and Schema. Decide whether Data
and Values remain product navigation below those doors or whether the architecture direction should be revised. Remove the
effects tagline either way because Reified no longer owns effects.

### Introductory snippets drift from current APIs

**Done.** `docs/values/reference-app.md` was rewritten from `examples/Reified.ReferenceApp.Intro/Program.fs`, which
compiles and runs. `Result.guard` is gone in favour of `Constraint.guard`, and the construction example matches the
source.

`docs/values/reference-app.md` says `Result.guard` keeps the original value after a constraint and shows a pipeline shaped
as `Constraint.minLength 3 |> Result.guard`; the current public vocabulary and nearby pages teach `Constraint.guard`.
The same page's final construction example appears internally inconsistent (`id` is bound, then `positiveId` is used, and
already-refined values appear to be wrapped again). Verify it against the runnable example and regenerate or rewrite from
source rather than preserving a stale hand-written approximation.

### Schema testing is advertised like a package but is not packable

**Done.** `Reified.Schema.Testing` is out of the package table and labelled repository tooling on the landing page,
the platforms page, and above the code on the testing-patterns page.

The Schema landing page includes `Reified.Schema.Testing` in a package table, while the guide later calls it a
repository-only, non-packable adapter. Readers cannot install the advertised package. Label repository tooling separately
from published packages and make the “copy/adapt this pattern” status visible before code examples.

### Schema.JsonSchema is presented as a package but is a module

**Done.** The row is gone; `JsonSchema` is identified as a module of `Reified.Schema` on the landing page and in
`overview.md`. The table now follows the real package graph.

The Schema landing page lists `Reified.Schema.JsonSchema` in its package table, but there is no corresponding project or
package: `JsonSchema.fs` is compiled into `Reified.Schema`. Change the row to the real package and identify `JsonSchema` as
its module, or move module capabilities out of a package table. Package tables must follow the actual package graph in
`dev-docs/AGENT_INDEX.md` and the umbrella-package tests.

### Generated and hand-written examples need one source of truth

**Partly done.** The reference-app page was regenerated from its runnable example, and `docs/getting-started.md` was
checked line by line against `examples/Reified.GettingStarted/Program.fs`. **Open:** there is still no automated check
that a hand-written snippet compiles. A focused compile-check harness for landing pages, `agent.md`, and `llms.txt`
would close it properly.

There are places where guide snippets make concrete API claims that appear to have drifted from runnable examples or
source comments. Audit introductory snippets first and either source them from executable examples or add focused compile
checks. Prioritize landing pages, getting-started pages, `agent.md`, and `llms.txt`, because readers and coding agents copy
those before reaching generated reference pages.

### Terminology alternates between validation and admission without a local explanation

**Done.** `docs/getting-started.md` now carries one shared rule — parse / check / refine / parse-structured — and says
where "validation" is still the right broad word.

Some pages say validation, some say checking, and Schema says parse-don't-validate. Add a small shared terminology rule to
the relevant introductions: Parse changes representation; Constraint checks a typed value; Refinement admits it into an
invariant-carrying type; Schema parses structured input and accumulates paths. Use “validation” only as the familiar broad
category or for the specifically named Schema operation.

### Adapter pages describe sameness too broadly

**Done.** `docs/data/with-reified.md` qualifies the claim and links to the adapter rules, naming the specific
differences: typed JSON leaves versus text, repetition semantics, CLI flag text, and colon paths.

`docs/data/with-reified.md` says the same schema parses the same logical input regardless of source. That is directionally
right, but adapter policies differ: repeated name/value pairs become lists, configuration repetition overwrites, CLI flags
become text booleans, native JSON has typed booleans/numbers, and configuration nesting uses colon paths. Qualify the claim
and link to the adapter-policy examples so “same logical input” does not imply identical lexical or collision semantics.

### Error-shape hand-offs are scattered

**Done.** One cross-product table — layer against map / keep / accumulate / render — lives in
`docs/getting-started.md` and is linked from the Parse and Constraint pages.

Result, Parse, Constraint, Refinement, and Schema pages each explain their own failure type, but the decision about when to
map, retain, accumulate, or render is repeated unevenly. Add one compact cross-product table to the Values or top-level
getting-started path and link to it instead of rebuilding partial explanations on every page.

### Navigation depth hides the common path

**Partly done.** "Next practical step" links were added at the points that came up in this pass: Data → Schema after
conversion, Result → Schema after accumulation, Parse → Schema for one field, Refined → Schema after the type exists,
and the localization index → its sub-pages. **Open:** no systematic sweep of every page that leaves a reader holding
an intermediate value.

Constraint localization, Schema refinement, and Data-to-domain conversion all require hopping between product trees. Add
“next practical step” links at the point a reader has an intermediate value or error, rather than only in long “read next”
lists at the bottom.

## What is left

Two items, both narrower than they were:

1. **A compile check for hand-written snippets.** Landing pages, `agent.md`, and `llms.txt` still carry snippets that
   nothing verifies. The pattern to copy is `examples/Reified.GettingStarted`, which the getting-started page is
   written from.
2. **A systematic "next step" sweep.** The obvious hand-offs are linked. Nobody has walked every page asking where a
   reader is left holding an intermediate value with no onward link.

Everything else in this document was applied. The decisions that came out of it — Data patterns staying separate from
`Constraint`, and the shape of `Result.traverseAll` — are recorded in `dev-docs/decisions/README.md`.
