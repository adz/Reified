# Reified v1 Baseline and API Surface Policy

This file records the current v1 stabilization baseline. It is for maintainers and coding agents, not user-facing
documentation.

## Current Baseline

- Recorded: `2026-08-07`
- Baseline commit: the `Reified` rename and umbrella-package commits on `main`
- .NET SDK: `10.0.300`
- Hugo: `0.161.1` extended

Validated commands for this refresh:

```text
bash scripts/check-source-inventory.sh
=> Source inventory covers src/tests .fs and .fsproj files.

dotnet build Reified.slnx --nologo -v minimal
=> 0 Error(s), 34 Warning(s).

dotnet test Reified.slnx --nologo -v minimal
=> Reified.Constraint.Tests           106 passed
   Reified.Data.Tests                  25 passed
   Reified.Package.Tests                6 passed
   Reified.Refinements.Tests           93 passed
   Reified.Result.Tests                 6 passed
   Reified.Schema.Contracts.Tests      59 passed
   Reified.Schema.Http.Tests            8 passed
   Reified.Schema.Json.Tests           28 passed
   Reified.Schema.Testing.Tests         6 passed
   Reified.Schema.Tests               333 passed
   => 670 passed, 0 failed, 0 skipped.

bash scripts/run-aot-probe.sh
=> Exit code 0 for the Result, Constraint, Refinements, and Schema probes.

bash scripts/run-package-consumers.sh
=> Consumer.FsToolkit, Consumer.Parse, Consumer.Refinements, Consumer.Result, Consumer.Schema,
   and Consumer.Umbrella all passed against the packed .nupkg files.

bash scripts/validate-docs.sh
=> Docs validation build succeeded; Hugo rendered 621 pages.

npm run build --prefix site
=> Succeeded; Hugo rendered 621 pages.

git diff --check
=> Clean.
```

Every suite that existed before the rename reports the same count afterwards, which is the evidence that the rename
changed names and not behaviour. `Reified.Package.Tests` is new.

Known validation gaps, tracked in `dev-docs/TASKS.md`:

- `scripts/check-schema-ce-errors.sh` expects `tests/compile-fail/schema-ce`, which was never extracted into this
  repository.
- there is no Fable JavaScript surface check here; its benchmark project stayed cross-product.
- there is no API-shape test project (see below).

The full solution build, generated API docs, docs preview, production site build, and an unrestricted `dotnet test` run
are required before committing any release/API-surface update. Record their result in the commit summary when they are
run.

## Package-Boundary Test Projects

One test project per package boundary, all listed in `Reified.slnx`:

- `tests/Reified.Constraint.Tests/Reified.Constraint.Tests.fsproj`
- `tests/Reified.Data.Tests/Reified.Data.Tests.fsproj`
- `tests/Reified.Package.Tests/Reified.Package.Tests.fsproj`
- `tests/Reified.Refinements.Tests/Reified.Refinements.Tests.fsproj`
- `tests/Reified.Result.Tests/Reified.Result.Tests.fsproj`
- `tests/Reified.Schema.Contracts.Tests/Reified.Schema.Contracts.Tests.fsproj`
- `tests/Reified.Schema.Http.Tests/Reified.Schema.Http.Tests.fsproj`
- `tests/Reified.Schema.Json.Tests/Reified.Schema.Json.Tests.fsproj`
- `tests/Reified.Schema.Testing.Tests/Reified.Schema.Testing.Tests.fsproj`
- `tests/Reified.Schema.Tests/Reified.Schema.Tests.fsproj`

`tests/package-consumers/**` is deliberately outside the solution. Those fixtures restore the packed `.nupkg` files
the way an outside consumer does, so a project reference into `src/` would defeat their purpose;
`check-source-inventory.sh` skips that directory for the same reason.

## CI Baseline Gates

CI currently proves:

- every `src/**/*.fsproj` and `tests/**/*.fsproj` project is listed by `Reified.slnx`
- every `src/**/*.fs` and `tests/**/*.fs` file is explicitly compiled by a `src` or `tests` project
- the package-boundary test projects run
- the umbrella package references exactly the packable runtime packages, and `scripts/pack.sh` packs all of them
- the schema examples and both reference applications run
- the NativeAOT probe publishes and runs
- the packages pack, and the package-consumer fixtures install and run them
- generated API docs and the docs site build

## API Surface Policy Before 1.0

Reified is still pre-1.0, so breaking changes are allowed when they improve coherence. However, every public API change
must be deliberate.

Required checks for public API changes:

1. Update XML docs on the changed public members.
2. Regenerate API docs with `bash scripts/generate-api-docs.sh`.
3. Build the docs site with `npm run build` in `site`.
4. Update `dev-docs/TASKS.md`, `dev-docs/PLAN.md`, and `RELEASE_NOTES.md` when a change affects v1 scope or release
   notes.

Public API removals and renames are acceptable before v1 only when they are intentional and reflected in the generated
reference docs and the relevant `dev-docs` plan or spec.

After v1, compatibility aliases and deprecation windows should replace immediate removals unless a security or
correctness issue requires a hard break.

## API-Shape Coverage

There is no API-shape test project in this repository: `Axial.ApiShape.Tests` covered the combined product and stayed
with Axial. Rebuilding one is queued in `dev-docs/TASKS.md`. The shape guarantees that do exist are:

- `tests/Reified.Package.Tests` pins the package graph: the umbrella's contents, that it compiles no sources, that
  repository tooling never reaches a consumer through it, and that `scripts/pack.sh` covers every packable package.
- `tests/package-consumers/**` pins the installed surface: each fixture opens the namespaces a single
  `PackageReference` is supposed to deliver, so a missing or wrong dependency is a compile error rather than a
  silent regression. `Consumer.FsToolkit` additionally pins that `Reified.Refinements` and `Reified.Schema` do not
  drag in `Reified.Result`, which would make `result { }` ambiguous for a consumer using FsToolkit's builder.

A rebuilt shape suite should cover the named modules, types, and members users and examples depend on:

- `Constraint`, `Violation`, `Renderer`, `Catalogue`
- `Schema`, `Field`, `Inspect` and its description types, `JsonSchema`
- `Reified.Schema.Json`'s `Json` module and `JsonCodec`
- `Data`, `Schema.parse`/`Schema.check`, `RetainedParseResult`, `SchemaError`, `Contract`
- `Reified.Schema.Http`'s `BoundaryInput`, `ProblemDetails`, `EndpointSpec`, and `OpenApi`
- `Result`, `Parse`, `Refine`, and the refined types
- the leaf-package dependency graph (leaf packages stay independent of each other)

If a public module is added, add it to the shape tests unless it is explicitly experimental and documented as such.
