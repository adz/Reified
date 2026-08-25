# Reified Documentation Guide

This file is the source of truth for the FsLiveDocs documentation structure and writing style.

## Audience and voice

Write for pragmatic F# developers working with typed failures, value rules, structured input, and wire formats.

- Prefer concise, code-first examples.
- Start with the problem or operation, not category theory or product promotion.
- Name the API, behavior, and trade-off directly.
- Keep paragraphs short and use examples where prose would otherwise become abstract.
- Do not mention unpublished status. Use versionless `dotnet add package` commands.

## Documentation structure

The handwritten guides live under `docs/`. Numeric source prefixes control navigation order but do not appear in the
published URL.

| Source | Published route | Subject |
| --- | --- | --- |
| `docs/01-getting-started/` | `/getting-started/` | End-to-end introduction and routing |
| `docs/03-result-handling/` | `/result-handling/` | Standard F# `Result` composition |
| `docs/04-constraints/` | `/constraints/` | Reusable value rules and violations |
| `docs/05-parsing/` | `/parsing/` | Serialized primitive decoding |
| `docs/06-refined/` | `/refined/` | Invariant-carrying types |
| `docs/07-data/` | `/data/` | Structured data, fixtures, and comparison |
| `docs/08-schema/` | `/schema/` | Structured parsing, JSON codecs, derivation, and versioned contracts |
| `docs/90-comparisons/` | `/comparisons/` | Comparisons with adjacent libraries |
| `docs/95-notes/` | `/notes/` | Packages, platforms, benchmarks, and implementation notes |

There is no Values documentation grouping. Result handling, Constraints, Parsing, and Refined are separate top-level
sections. JSON Codecs belongs inside Schema. HTTP server documentation is not published.

`docs/index.md` is the homepage. `docs/content/reified-theme.css` styles the generated FsLiveDocs site.
`.livedocs/config.json` controls the logo, navigation, stylesheet, prelude, and API projects.

## Source and generated output

Handwritten Markdown, source XML comments, and runnable examples are sources. `output/`, `.livedocs/cache/`, and the
rendered API reference are generated.

Long-form API introductions live under `docs/api/`. Name each Markdown file after the generated entity ID, for
example `docs/api/Reified.Schema.md` for the `Schema` module and ``docs/api/Reified.Schema`1.md`` for
`Schema<'model>`. FsLiveDocs replaces that entity's short summary with the Markdown before rendering its members.

When a public API changes:

1. Update its XML comment and example in `src/`.
2. Update handwritten guides and the relevant section-local `llms.txt`.
3. Run `dotnet livedocs build --interactive false --banner false`.
4. Review the generated page in `output/` when layout or navigation changed.

Do not hand-edit `output/` as the primary fix.

## Validation and preview

- `dotnet livedocs audit --warn-as-error` checks F# blocks and documentation coverage.
- `dotnet livedocs build` audits and renders the static site to `output/`.
- `dotnet livedocs watch --port 5000` serves a rebuilding preview at `http://localhost:5000`.
- Release workflows pass the release tag through `--version`; installation examples remain versionless.

Run a full build after cross-section moves, route changes, stylesheet changes, or a phase/release checkpoint. A prose-only
edit can use the audit unless it changes links or layout.

## Authoring rules

- Use fenced code blocks with the `fsharp` language hint for F# examples.
- Let FsLiveDocs verify executable examples. Add `no-check` only with a precise reason.
- Use root-relative published links such as `/schema/quickstart.html`, never numeric source paths in site links.
- Keep source filenames numerically prefixed except `_index.md`, `llms.txt`, and generated/reference material.
- Make section indexes ordinary documentation pages with standard sidebars. Only the repository homepage uses the
  full-bleed `.reified-landing` layout.
- Add an XML doc comment with an example to every public function.
- Avoid FAQ-style rhetorical questions, filler, and promises about future features.

## LLM and agent entry points

The root `llms.txt` routes agents to focused context. Section-local files live at:

- `docs/03-result-handling/llms.txt`
- `docs/04-constraints/llms.txt`
- `docs/07-data/llms.txt`
- `docs/08-schema/llms.txt`

Update these files when their public surface or recommended authoring path changes. Do not add agent-specific pages to
the user-facing documentation tree.
