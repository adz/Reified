# Benchmark process

This covers running `benchmarks/Reified.Schema.Benchmarks` for real numbers, and specifically the multi-runtime
comparison (.NET 8, 10, 11) behind the "competitive with `System.Text.Json`" claim in
`docs/95-notes/80-benchmarks.md`. It does not cover authoring new benchmark cases; see `CodecSuites.fs` and
`ScaledSuites.fs` for those.

## Ordinary single-runtime run

```bash
BENCHMARK_ARGS='--job short --filter *' dotnet run --project tools/Reified.Build -- --target Benchmarks
```

`--job short` is fast but noisy: BenchmarkDotNet's own `ShortRun` docs call its error bars "often a large fraction
of the mean." Treat a short run as a smoke test, not a number to quote. Use `--job medium` (or no `--job` at all,
BenchmarkDotNet's default) before writing a result into `docs/95-notes/80-benchmarks.md`, and run it on an
otherwise idle machine: a background build or another dotnet process visibly moves the mean between runs.

## Multi-runtime comparison

The repository's own `Reified.Schema` targets `netstandard2.1;net8.0`, and the benchmark project ordinarily targets
one TFM (`net10.0`) matching the ambient SDK. Comparing `Json.compile` against `System.Text.Json` across several
.NET versions needs the benchmark host itself rebuilt and rerun per TFM, against a matching installed runtime.

### 1. Install the SDKs

The ambient toolchain (pinned in `mise.toml`) is .NET 10.0.300. Install the others side by side without touching
that pin:

```bash
mise install dotnet@8.0.424   # any recent 8.0.x; installs under ~/.local/share/mise/installs/dotnet/8.0.424

mkdir -p ~/.dotnet-net11
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 11.0 --quality preview --install-dir ~/.dotnet-net11
```

.NET 11 is pre-GA as of this writing, so the install script's `--quality` must be `preview` (or `daily`); `rc` and
`ga` are rejected even once .NET 11 reaches its RC stage, because the script's quality taxonomy does not have an
`rc` value distinct from `preview`. Verify what actually landed and confirm the runtime, not only the SDK, is
present, since a missing runtime fails silently later as a wrong-`dotnet`-on-`PATH` problem instead of a clear
"not installed" error:

```bash
~/.dotnet-net11/dotnet --list-sdks
~/.dotnet-net11/dotnet --list-runtimes | grep NETCore
```

Each install lives in its own root (`~/.local/share/mise/installs/dotnet/8.0.424`, `~/.dotnet-net11`, and the
ambient `/home/adam/.local/share/mise/installs/dotnet/10.0.300`), not merged into one shared-framework directory.
That matters for the pitfalls below: nothing here sees another root's runtimes unless you point at it explicitly.

### 2. Bump BenchmarkDotNet for .NET 11

BenchmarkDotNet 0.15.8 (the version this project ships) throws `NotImplementedException: GetRuntimeVersion not
implemented for NotRecognized` under .NET 11: it predates
[dotnet/BenchmarkDotNet#2911](https://github.com/dotnet/BenchmarkDotNet/pull/2911) ("Add net11 support", merged
into `master` 2025-12-12), which is where `CoreRuntime`, `RuntimeMoniker`, and the toolchain resolvers first learn
about `net11.0`. Bump the benchmark project's `PackageReference` to `0.16.0-preview.2` or later, .NET 8 and .NET 10
results are unaffected; the package's `lib/net10.0` assets are used for every TFM up to net10.0, and its
`lib/net10.0` assets are also what a net11.0-targeted project resolves to (no `lib/net11.0` folder exists yet),
so it is the same compiled logic across all three runs.

This is a scratch change: revert `ReifiedBenchmarks.fsproj` to `net10.0` / `BenchmarkDotNet 0.15.8` once you have
your numbers, so the shipped benchmark project keeps building against the same toolchain as everything else in the
repo.

### 3. Build and run one TFM at a time

Do not multi-target the benchmark project (`<TargetFrameworks>net8.0;net10.0;net11.0</TargetFrameworks>`) to run
all three from one build. BenchmarkDotNet's generated child project references your project via a bare
`ProjectReference` with no TFM pinned, so it restores *every* TFM in `TargetFrameworks`, including ones the SDK
you are currently using cannot build (`NETSDK1045: The current .NET SDK does not support targeting .NET 11.0`),
even when you only asked to run the one it can. Set a single `<TargetFramework>`, rebuild, run, then edit and
rebuild for the next:

```bash
# net8.0
sed -i 's#<TargetFramework>[^<]*</TargetFramework>#<TargetFramework>net8.0</TargetFramework>#' \
  benchmarks/Reified.Schema.Benchmarks/ReifiedBenchmarks.fsproj
rm -rf benchmarks/Reified.Schema.Benchmarks/{obj,bin} artifacts/bin/ReifiedBenchmarks artifacts/obj/ReifiedBenchmarks
DOTNET8=~/.local/share/mise/installs/dotnet/8.0.424/dotnet
$DOTNET8 build benchmarks/Reified.Schema.Benchmarks -c Release
$DOTNET8 run --project benchmarks/Reified.Schema.Benchmarks -c Release --no-build -- \
  --job medium --filter '*JsonCodecBenchmarks*' --cli "$DOTNET8"
```

Repeat with `net10.0` against the ambient `dotnet`, and with `net11.0` against `~/.dotnet-net11/dotnet` (see the
next section for the extra flag that build needs).

### 4. Two BenchmarkDotNet bugs that only show up cross-runtime

**Bug 1: the child process launches via `PATH`, not your `--cli`/invocation binary.** Passing
`--job medium --filter ... --cli /path/to/dotnet` and launching with that same binary is not enough on its own.
BenchmarkDotNet's generated child benchmark process is started with a bare `dotnet` command that resolves through
the ambient `PATH`, in this environment always the mise-shimmed 10.0.300, regardless of which `dotnet` binary you
used to start the parent process. Running a `net8.0`-built benchmark this way fails with:

```
You must install or update .NET to run this application.
Framework: 'Microsoft.NETCore.App', version '8.0.0' (x64)
.NET location: /home/adam/.local/share/mise/installs/dotnet/10.0.300/
```

Always pass `--cli <path>` explicitly, pointed at the SDK matching the TFM you built.

**Bug 2: `--cli` itself is only honored for a cross-runtime job, not the default "run as the host's own runtime"
job.** Even with `--cli` set correctly, running the benchmark exe directly (no `--runtimes` flag) still fails, this
time with:

```
The required .NET Core SDK version 11.0 or higher for runtime .NET 11.0 is not installed.
```

despite the matching SDK being present and `--cli` pointed at it. The cause is in
`BenchmarkDotNet.Validators.DotNetSdkValidator.ValidateCoreSdks`: it runs `<cliPath> --list-sdks` to confirm the
target runtime's SDK is available, but the `customDotNetCliPath` it receives is only populated for a job created
through the `--runtimes` cross-targeting path. The default same-runtime-as-host job validates against
`DotNetCliCommandExecutor.DefaultDotNetCliPath` (the ambient `PATH` binary) unconditionally, ignoring `--cli`.
Confirmed by loading the installed `BenchmarkDotNet.dll` and calling its internal `GetInstalledDotNetSdks` directly
with the correct path: it returns `[11.0.100]` immediately, so the SDK list-and-parse logic itself is not the bug.

The fix is to always pass `--runtimes <tfm>` alongside `--cli`, even when the exe was already built for exactly
that TFM and you are not really asking BenchmarkDotNet to cross-target anything:

```bash
DOTNET11=~/.dotnet-net11/dotnet
$DOTNET11 build benchmarks/Reified.Schema.Benchmarks -c Release
$DOTNET11 run --project benchmarks/Reified.Schema.Benchmarks -c Release --no-build -- \
  --job medium --filter '*JsonCodecBenchmarks*' --runtimes net11.0 --cli "$DOTNET11"
```

### 5. Don't run TFMs concurrently

Each TFM switch does `rm -rf` on the shared `artifacts/bin/ReifiedBenchmarks` and `artifacts/obj/ReifiedBenchmarks`
directories, since the custom `ArtifactsPath` in this repo is not parameterized per TFM. Running two of these
build/run sequences at once corrupts both. Run them one at a time.

## Confirming a real .NET 11 install, not a silent .NET 10 fallback

The two bugs above both manifest as "looks like it's testing .NET 11 but is actually silently running .NET 10.0
under the hood," so treat the following as a mandatory precondition before trusting a .NET 11 number, not an
optional sanity check:

```bash
~/.dotnet-net11/dotnet --list-runtimes | grep -q "Microsoft.NETCore.App 11\." || echo "net11 runtime missing"
```

And in the BenchmarkDotNet summary output itself, check the `[Host]` line names the runtime you intended
(`.NET 11.0.0-rc.1...`), not `.NET 10.0.8`, before reading any numbers from that run.
