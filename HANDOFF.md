# Reified rename handoff

Temporary session handoff. Delete this file after the rename is complete and its durable decisions have been folded into the repository documentation.

## Objective

Rename the extracted description-side repository from its transitional `Axial.*` identity to `Reified.*`, and add the `Reified` umbrella NuGet package that installs all public runtime packages.

The repository is `/home/adam/projects/Reified`; `main` tracks `git@github.com:adz/Reified.git`. The extraction and standalone build/test work is already complete. Start from commit `6f10fe2` or later.

Axial is now the Flow product. Reified owns Constraint, Refined, Parse, Result, Data, Schema, JSON, host-neutral HTTP contracts, contract generation, and schema-derived testing. Cross-product server adapters and the integration reference application remain in Axial for now.

## Read first

- `AGENTS.md`
- `dev-docs/AGENT_INDEX.md`
- `dev-docs/PLAN.md`
- `dev-docs/TASKS.md`
- `dev-docs/decisions/README.md`
- `dev-docs/DOCS.md` before changing user-facing or generated documentation

These files were copied during extraction and still describe Axial's old combined product in places. Correct them as part of the rename; do not treat stale statements such as “there are no meta-packages” as current Reified direction.

## Settled package identity

Public runtime packages:

- `Reified` — umbrella package referencing all public runtime packages below
- `Reified.Constraint`
- `Reified.Refinements`
- `Reified.Parse`
- `Reified.Result`
- `Reified.Data`
- `Reified.Schema`
- `Reified.Schema.Json`
- `Reified.Schema.Http`
- `Reified.Schema.Contracts.Build`

Repository tooling remains non-runtime:

- the contract compiler library/executable
- the `Schema.Testing` FsCheck adapter

The README already presents this package model. Preserve the public concept name `Constraint<'value>` and the architecture invariants in `AGENTS.md` while changing their namespace/package prefixes.

Teach declaration blocks with `open Reified.Constraint.ConstraintDSL`, so common constructors read as `constraints [ present; email ]` and `constrain (atLeast 13)`. Keep `Constraint.` for execution, composition operators deliberately omitted from the DSL, or places where qualification improves clarity.

## Rename scope

Rename coherently rather than applying a blind global replacement:

1. Rename source, test, example, benchmark, and script project directories and `.fsproj` files.
2. Rename F# namespaces/modules and all `open` statements from `Axial.*` to their intended `Reified.*` names.
3. Update project references, assembly/package metadata, root solution entries, scripts, build targets, generated-code templates, golden files, package-consumer fixtures, and CI workflows.
4. Rename `Axial.Refined` to `Reified.Refinements` everywhere the package/namespace identity is public. Check whether internal type/module names such as `Refined` remain the clearest domain vocabulary; do not mechanically pluralize those.
5. Create the packable `Reified` umbrella project. It should contain no competing API surface and should reference all public runtime packages. Add package-graph/API-shape tests that prove its contents.
6. Rename contract attributes, MSBuild item/property/target names, generated filenames, and schemagen output deliberately. Because the project is pre-1.0, remove old aliases rather than preserving compatibility shims.
7. Update maintainer docs, user guides, hand-written `llms.txt` entry points, source XML comments, and generator inputs. Regenerate reference documentation; do not commit generated site output.
8. Search tracked files for remaining product-identity uses of `Axial`, excluding historical references that intentionally discuss the split and links to the separate Axial repository.

Do not rename ordinary English uses or the external Axial integration link blindly. Do not move Flow code back into this repository.

## Generated and ignored output

Generated reference pages under `docs/*/reference/**`, site content/public output, and build artifacts were removed from history and should remain untracked. Follow the generated-path rules in `dev-docs/AGENT_INDEX.md` and `.gitignore`. Update source comments and generator inputs, then regenerate only as required by the repository workflow.

## Verification

At minimum:

- `bash scripts/check-source-inventory.sh`
- build the renamed solution
- run every retained test project
- run package-consumer tests against packed local artifacts
- run AOT probes where supported
- run `bash scripts/validate-docs.sh` at this rename/release boundary
- run `npm run build` in `site`
- `git diff --check`
- verify no unintended tracked `Axial.*` project, package, namespace, or generated path remains

Before the rename, the retained focused suites passed with these counts: Constraint 106, Data 25, Refined 93, Result 6, Contracts 59, Schema.Http 8, Schema.Json 28, Schema.Testing 6, and Schema 333.

## Suggested skills

- `improve-codebase-architecture` — use if package boundaries or the umbrella dependency graph need reconsideration during the rename.
- `diagnose` — use only if the renamed solution produces failures that are not straightforward reference/name errors.
- `edit-article` — use for the final README and user-documentation terminology pass.

## Completion

Commit the rename in reviewable stages, push `main` only after the complete standalone verification passes, fold durable decisions into `dev-docs/PLAN.md` and `dev-docs/decisions/README.md`, then delete this handoff.
