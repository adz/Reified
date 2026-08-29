# Release Process

This project uses a coordinated pre-1.0 release train.

## Versioning policy

- Every package in this repository shares one version before 1.0.
- The shared version is declared once in `Directory.Build.props`.
- Packable project files must not declare their own `<Version>`.
- A tag such as `v0.8.0` produces every public Reified package at version `0.8.0`.
- Empty version bumps are acceptable before 1.0 because the package boundaries are still settling and a single documented version is simpler for users.
- Independent package versioning can be reconsidered once the split is stable, likely at or after 1.0.
- The Axial repository has its own train. A tag here says nothing about a version there.

## Public package set

the `Pack` FAKE target is the authority; `tests/Reified.Package.Tests` fails if a packable runtime package is missing
from it. The set is:

- `Reified.Constraint`
- `Reified.Refinements`
- `Reified.Parse`
- `Reified.Result`
- `Reified.Data`
- `Reified.Schema`
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
3. Run the complete FAKE release gate:

```bash
REIFIED_VERSION=<release-version> dotnet run --project tools/Reified.Build -- --target ReleaseCandidate
```

This builds and tests the solution, checks Fable and NativeAOT, captures documentation before packing, tests the packed
NuGet packages as external consumers, inserts the local capsule into release history, renders every version, and checks
every generated local link and version-switcher entry.

4. Commit the release-prep changes.
5. Push the release commit to `main`.
6. Create and push the tag:

```bash
git tag v0.8.0
git push origin v0.8.0
```

## CI release behavior

`.github/workflows/release.yml` runs for `v*.*.*` tags.

For a tag build, it derives the package version from the tag and runs the same `ReleaseCandidate` FAKE target used
locally. Documentation capture runs before packing because package-specific rebuilds corrupt FsLiveDocs assembly
loading. The target then tests packed packages and the complete versioned site before publication.

After the release candidate passes, the workflow:

- it uploads package and docs workflow artifacts
- it creates a GitHub Release with `.nupkg`, `.snupkg`, and the immutable FsLiveDocs capsule attached, using
  `dev-docs/releases/<version>.md` as the release body
- after the GitHub Release exists, it dispatches Pages with the capsule URL and SHA-256 and waits for that deployment
- only after Pages succeeds, it runs `publish-nuget` with the already-tested package artifacts

FsLiveDocs 0.4 owns release-history synchronization and verification. The Pages workflow uses `history-sync` to
merge semantic-versioned `Reified-<version>-livedocs.zip` assets from GitHub Releases into its temporary index;
GitHub's asset digest supplies the SHA-256. A release dispatch requires the new capsule to be current with the exact
expected URL and checksum. `build-history --retry 3` retries transient capsule downloads, and `verify-output` checks
entry points, version-switcher order, and generated local links. Later `main` builds therefore retain the complete
published history. The committed history remains an offline compatibility baseline and should be refreshed
periodically, but publishing a release does not depend on a follow-up commit.

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
dotnet run --project tools/Reified.Build -- --target Pack
```

Override the version explicitly:

```bash
REIFIED_VERSION=0.7.0 dotnet run --project tools/Reified.Build -- --target Pack
```

Packages are written to `artifacts/package`.
