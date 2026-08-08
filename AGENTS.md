# Repository Instructions

This file is for agent instructions, not user-facing documentation.

Keep a strict split between:

- agent instructions for contributors and coding agents
- user-facing documentation for library users

Do not put agent guidance in `README.md` or under `docs/`.

When writing or editing user-facing docs, follow the documentation guide in [`dev-docs/DOCS.md`](dev-docs/DOCS.md).

Before broad repository search, read [`dev-docs/AGENT_INDEX.md`](dev-docs/AGENT_INDEX.md) for the compact maintainer
map, generated-path rules, and task routing.

Refer to [`dev-docs/PLAN.md`](dev-docs/PLAN.md) for architectural direction and
[`dev-docs/TASKS.md`](dev-docs/TASKS.md) for the active queue.

## Architecture Invariants

### NO EFFECTS — REIFIED DESCRIBES, IT DOES NOT RUN

- Reified is the description side. No package here performs I/O, and none may take a dependency on the effect
  system: `Axial.Flow` and its satellites live in the [Axial repository](https://github.com/adz/Axial), and the
  dependency never points this way either. `Reified.Schema.Http` is host-neutral by construction — it assembles
  contracts, problem details, and OpenAPI documents as values, and never opens a socket or reads a request.
- Never call `DateTimeOffset.UtcNow`, `DateTime.Now`, another ambient clock, randomness, GUID generation,
  environment variables, the filesystem, or the console from a Reified package. A schema that parses differently
  on Tuesday is not a description.

- Keep `Constraint<'value>` as the one public value-rule concept. A constraint is a reusable description of valid values; `check` is the operation that runs it. There is no separate `Check` type and no second constructor catalogue: `Reified.Refinements` and `Reified.Schema` consume the same `Constraint` value a caller checks directly.
- Constraints have two tiers, and the split is load-bearing. **Interpreted** constraints are a closed algebra: each built-in constructor builds one `ConstraintAtom` and places that same value in both its description and any violation, so execution, export, and future proof cannot drift. The algebra grows only by Reified release — there is no registration API, and no authored code or string may claim inspectable logic. **Opaque** constraints (`custom`, `customWith`, `notWith`, `contramap`) run normally, report author-supplied prose, and are honestly invisible to export and proof.
- `Violation` is a diagnostic contract, not an application error union. It is plain comparable data: no closure and no `ConstraintDescription` is reachable from one. Never lower a constraint failure into a parse-shaped `SchemaError` case, and never attach a string code to a violation — identity comes from the atom, and message keys are a rendering projection computed at the edge.
- Localization is a rendering-edge concern owned by `Renderer`. A `Violation` never carries a culture, resource manager, renderer, Schema `SchemaPath`, or application context; `Renderer.context`/`attribute` supply those when a message is produced. Catalogue entries are bare predicates, and the attribute noun, the actual-value clause, and group/list joining are separate composition entries. `Reified.Constraint` must never learn a Schema message identity — Schema pushes its own `MessageFormatSpec` values through the generic renderer instead. The .NET resource-manager constructors are conditionally absent under Fable and must never compile to a silent no-op.
- Interpreters divide by what they *claim*, not by whether they produce a value. Admission and constraint-satisfying generation fail closed; trusted structural codecs make no constraint claim and stay outside constraint interpretation; documentation and export degrade honestly, emitting what the target enforces and retaining the rest as prose and `x-reified-runtime-constraints`. No interpreter may silently claim enforcement it did not perform.
- Before lowering an atom to a wire keyword, check that the *path between them* — parse, round, canonicalize — preserves the relation the atom asserts. Portable in storage is not portable in meaning: a regex dialect, a text length, and a decoded GUID each survive the trip as data while changing what they mean at the other end.
- Boundary supply stays Schema-owned (`Schema.mustSupply`/`mayOmit`). It is decided before a typed value exists, so it is not a value constraint.
- Value-preserving guards and extraction helpers belong in `Result`, parsing belongs in `Reified.Parse`, and refined value construction belongs in `Reified.Refinements`.
- Prefer AOT- and trimming-safe designs. Do not introduce runtime reflection as the foundation for core workflow, validation, schema, or service-access APIs; use explicit definitions first and consider build-time generation only after the API shape stabilizes.

## Dev Doc Organization

- Keep active architecture in `dev-docs/PLAN.md`, active work in `dev-docs/TASKS.md`, and high-level durable
  decisions in `dev-docs/decisions/README.md`.
- Keep completed work out of `dev-docs/TASKS.md`; keep the remaining active queue there for loop scripts.
- Keep speculative or pre-idea work in `dev-docs/current-ideas/`.
- Do not retain detailed historical specs after their useful decisions have been folded into current instructions. Delete stale specs instead of archiving large files that no longer match the codebase.

## Writing

- Write concrete prose that names the API, behavior, tradeoff, or decision directly. Remove generic AI filler,
  promotional adjectives, grandiose claims, repetitive summaries, fake quotations, and throat-clearing such as
  "In today's landscape", "It's important to note", "powerful", "robust", "seamless", and "comprehensive" when the
  sentence does not prove a specific claim. Do not use slogans such as "not just X, but Y" in place of an explanation.
- In documentation and code comments, explain facts a reader cannot already see from the signature or implementation.
  Prefer a short example or a precise constraint over restating the member name in prose.

## Test Authoring

- Tests that demonstrate public APIs should use the expected end-user pipeline form, not a lower-level or transitional shape, unless the test is explicitly covering that lower-level API. Public API tests are examples readers copy from; keep their formatting aligned with the authoring style the library intends to teach.
- Do not define shared fixtures as module-level `let` values in xUnit test modules (schemas, refs, prebuilt inputs). Module-level bindings in test modules can be observed as null before file-level initialization runs, which surfaces as confusing `NullReferenceException`/`ArgumentNullException` failures. Build fixtures inside each test or expose them as functions (`let private mySchema () = ...`).

## Doc Workflow

- Treat `docs/*/reference/**`, `docs/examples/README.md`, and versioned docs as generated outputs or generator-backed outputs. The root `llms.txt` and the four product ones (`docs/result/`, `docs/values/`, `docs/data/`, `docs/schema/`) are hand-written entry points.
- When changing an API, update the source comments and the doc generator inputs first, then regenerate the docs. Do not hand-edit generated reference pages as the primary fix.
- When a user-facing guide needs to cite a new or renamed API, update the source comments and reference pages in the same pass, then run the generators immediately.
- For small checkbox tasks, regenerate directly affected docs as needed but defer `bash scripts/validate-docs.sh` until the phase end or a release/deploy checkpoint. `dev-docs/**` idea/planning notes do not require validation. For release/deploy checks, also run `npm run build` in `site`.

## Versioning and Compatibility

- **Before 1.0:** Bravely iterate. Remove old APIs and "old ways" immediately when a better alternative is established. Do not maintain compatibility aliases or stale patterns.
- **Post 1.0:** Standard semantic versioning applies. Maintain compatibility and use deprecation cycles for breaking changes.
- Packable projects inherit the shared version from `Directory.Build.props`; do not declare project-specific `<Version>` values.
- A release tag such as `v0.7.0` produces all public Reified NuGet packages at version `0.7.0`.
- Revisit independent package versioning after the package boundaries stabilize, likely at or after 1.0.

## Documentation Integrity

- **Validate At Phase Or Release Boundaries:** For small checkbox tasks, defer `bash scripts/validate-docs.sh` until phase end or a release/deploy checkpoint, even after changes to user-facing docs, generated docs, public API signatures, XML comments, doc generator inputs, docs examples, reference docs, `llms.txt`, or site content. Regenerate affected generated docs during the task. `dev-docs/**` idea/planning notes and code-only changes with no public-doc impact do not require validation. Use `bash scripts/preview-docs.sh` only when a live server is needed for browser review or screenshots.
- **Preview Lifecycle:** `bash scripts/preview-docs.sh` stops cleanly on `SIGHUP`, `TERM`, or `INT`. It can also be stopped by creating `$REIFIED_DOCS_PREVIEW_STOP_FILE`, which defaults to `/tmp/reified-docs-preview.stop`.
- **Link Integrity:** Ensure that all cross-references between guides and reference pages are valid. Broken links degrade the experience for both humans and AI agents.
- **Code Highlighting:** Ensure all code examples are wrapped in triple-backticks with the `fsharp` language hint for proper syntax highlighting.
