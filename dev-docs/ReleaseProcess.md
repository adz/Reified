# Release Process

This project uses a coordinated pre-1.0 release train.

## Versioning policy

- Every package in this repository shares one version before 1.0.
- The shared version is declared once in `Directory.Build.props`.
- Packable project files must not declare their own `<Version>`.
- A tag such as `v0.7.0` produces every public Reified package at version `0.7.0`.
- Empty version bumps are acceptable before 1.0 because the package boundaries are still settling and a single documented version is simpler for users.
- Independent package versioning can be reconsidered once the split is stable, likely at or after 1.0.
- The Axial repository has its own train. A tag here says nothing about a version there.

## Public package set

`scripts/pack.sh` is the authority; `tests/Reified.Package.Tests` fails if a packable runtime package is missing
from it. The set is:

- `Reified.Constraint`
- `Reified.Refinements`
- `Reified.Parse`
- `Reified.Result`
- `Reified.Data`
- `Reified.Schema`
- `Reified.Schema.Json`
- `Reified.Schema.Http`
- `Reified.Schema.Contracts.Build`
- `Reified` — the umbrella, which carries no assembly and only depends on the runtime packages above

`Reified.Schema.Contracts.Build` is not in the umbrella. MSBuild `build/` assets are not transitive, so an
umbrella dependency would install its targets without ever running them; consumers that derive schemas at build
time reference it directly.

The contract compiler (`Reified.Schema.Contracts`) and the FsCheck adapter (`Reified.Schema.Testing`) are
repository tooling and are never packed.

## Preparing a release

1. Update the shared `<Version>` in `Directory.Build.props`.
2. Update `RELEASE_NOTES.md`, `NEXT_VERSION`, and add `dev-docs/releases/<version>.md` with that version's notes.
   `scripts/check-release-notes.sh` fails the release build if the notes file for `NEXT_VERSION` is missing.
3. Run the local verification commands:

```bash
dotnet build Reified.slnx --configuration Release --nologo -v minimal
dotnet test Reified.slnx --configuration Release --no-build --nologo -v minimal
bash scripts/check-source-inventory.sh
bash scripts/check-schema-ce-errors.sh
bash scripts/check-fable-js-surface.sh
bash scripts/run-aot-probe.sh
bash scripts/run-package-consumers.sh
dotnet livedocs build --version <release-version> --interactive false --banner false
```

4. Commit the release-prep changes.
5. Push the release commit to `main`.
6. Create and push the tag:

```bash
git tag v0.7.0
git push origin v0.7.0
```

## CI release behavior

`.github/workflows/release.yml` runs for `v*.*.*` tags.

For a tag build:

- it builds `Reified.slnx`
- it tests `Reified.slnx` (every package-scoped test project)
- it derives the package version from the tag by stripping the leading `v`
- it runs `bash scripts/check-release-notes.sh <version>`, which fails if `dev-docs/releases/<version>.md` is missing
- it runs `dotnet livedocs audit --warn-as-error`, builds the docs site, and captures the release capsule
- it runs `bash scripts/pack.sh -v <version>` **after** the docs steps — `dotnet pack` rebuilds each project fresh for the
  tagged version, and running it before the docs steps corrupted FsLiveDocs' assembly loading (surfaced as every doc page
  failing to resolve `Reified.*` types); the docs pipeline must run against the plain `dotnet build` output
- it uploads package and docs workflow artifacts
- it creates a GitHub Release with `.nupkg`, `.snupkg`, and the immutable FsLiveDocs capsule attached, using
  `dev-docs/releases/<version>.md` as the release body
- after the GitHub Release exists, it dispatches the Pages workflow with the capsule URL and SHA-256; Pages adds that
  capsule to the committed history baseline for the build and publishes the version switcher
- it runs a separate `publish-nuget` job that publishes the package artifacts to nuget.org

The committed `.livedocs/history.json` is the baseline used by branch and manual Pages builds. After a release, update
that baseline with the released capsule's immutable GitHub URL and SHA-256 before the next release-preparation commit.

For manual `workflow_dispatch`, leave `release_tag` empty for an `0.0.0-rcDev` verification run. Supplying an existing
tag such as `v0.2.0` checks out that tag and repairs its GitHub Release assets and versioned documentation without
publishing packages to NuGet again.

## NuGet publishing

The release workflow publishes packages to nuget.org only from tags matching `v*.*.*`.

NuGet publishing is isolated in the `publish-nuget` job:

- it runs only after the package job succeeds
- it runs only for tag refs beginning with `refs/tags/v`
- it uses the `nuget` GitHub Environment
- it reads `NUGET_API_KEY` from GitHub Secrets
- it publishes the already-built package artifact instead of rebuilding

Configure the `nuget` environment in GitHub repository settings. For maximum safety, require manual approval for that environment and store `NUGET_API_KEY` as an environment secret rather than a repository-wide secret.

The publish command is equivalent to:

```bash
dotnet nuget push artifacts/package/<package>.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

The workflow publishes every `.nupkg` and `.snupkg` produced for the release and uses `--skip-duplicate` so rerunning a failed publish job does not fail on packages that already reached nuget.org.

## Local packing

Use the repository version:

```bash
bash scripts/pack.sh
```

Override the version explicitly:

```bash
bash scripts/pack.sh -v 0.7.0
```

Packages are written to `artifacts/package`.
