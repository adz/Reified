---
weight: 80
title: Benchmarks
targetFramework: net8.0
---

# Benchmarks

This page records local runs of the JSON codec and boundary parsing suites on one laptop and toolchain. Use the
numbers to compare paths and allocations, not as cross-machine performance guarantees. See
[Across .NET versions](#across-net-versions) for the current headline comparison against `System.Text.Json`; the
sections below it are a single-machine, single-runtime run from 2026-09-15 kept for the allocation and staging
detail it shows, not for its specific percentages.

The suites live in
[benchmarks/Reified.Schema.Benchmarks/CodecSuites.fs](https://github.com/adz/Reified/blob/main/benchmarks/Reified.Schema.Benchmarks/CodecSuites.fs).
The `Benchmarks` FAKE target starts
a Release run and forwards any BenchmarkDotNet arguments.

## Setup

The measured run used:

- Fedora Linux 44
- Intel Core i5-10310U, 4 physical cores / 8 logical cores
- .NET SDK 10.0.300
- .NET runtime 10.0.8
- F# 10.0
- BenchmarkDotNet 0.15.8

The recorded results use BenchmarkDotNet's `ShortRun` job: one launch, three warmups, and three measured iterations.
This is enough for a laptop-local directional comparison, but the error bars are wide, often a large fraction of the
mean, so treat close timings as equivalent until a longer run shows otherwise. Allocations are stable between runs
and are the more reliable signal here.

## JSON codec

The codec suite measures `Reified.Schema`'s `Json` codec on the same aggregate and representation as `System.Text.Json`: string
against string, and UTF-8 bytes against UTF-8 bytes. The model has seven primitive fields, one nested record, two
collections, and boundary constraints on name and age.

Run them:

```bash
BENCHMARK_ARGS='--job short --filter *' dotnet run --project tools/Reified.Build -- --target Benchmarks
```

| Operation | Reified mean / allocated | `System.Text.Json` mean / allocated |
| --- | --- | --- |
| Serialize string | 1.33 us / 1,232 B | 1.36 us / 1,136 B |
| Serialize UTF-8 | 1.28 us / 880 B | 1.31 us / 776 B |
| Deserialize string | 3.10 us / 2,408 B | 2.88 us / 2,056 B |
| Deserialize UTF-8 | 2.42 us / 2,016 B | 2.99 us / 2,056 B |

The canonical-order UTF-8 decoder was 19% faster than `System.Text.Json` in this run and allocated 40 B less. String
decoding includes UTF-8 conversion and remained slower. The integer encoder's direct formatting keeps Reified
serialization within 96–104 B of `System.Text.Json`. The UTF-8 APIs avoid the string representation and are the
relevant comparison when a payload already arrives as bytes.

Codec compilation is separate from per-payload work:

| Operation | Mean | Allocated |
| --- | --- | --- |
| `Json.compile` for the customer schema | 14.19 us | 20.45 KB |

Compile a codec once and reuse it. Recompiling per payload would dominate the encode path and cost roughly two to
three times one decode on this model.

## Boundary parsing

The boundary suite compares the trusted codec against full boundary parsing, `JsonDocument` to `Data` to `Schema.parse` with complete path-aware diagnostics:

| Operation | Mean | Allocated |
| --- | --- | --- |
| `Reified Json.deserializeBytes` (trusted, end to end) | 2.91 us | 1.97 KB |
| `JsonDocument` + `Data` + `Schema.parse` (boundary, end to end) | 11.87 us | 13.22 KB |

The boundary path was 4.1 times the mean and 6.7 times the managed allocation of trusted UTF-8 decoding in this
run. Stage measurements show where that work lands:

| Boundary stage | Mean | Allocated |
| --- | --- | --- |
| JSON document to `Data` | 3.08 us | 3.56 KB |
| `Schema.parse` invalid `Data` | 7.85 us | 9.21 KB |
| `Schema.parse` valid `Data` | 8.22 us | 9.66 KB |

The stages are diagnostic measurements, not additive accounting: the end-to-end benchmark measures its own complete
operation. The invalid case changes the constrained name to an empty string and measures accumulated error creation.

Alias resolution uses the same customer shape, with `Name` accepted as an input-only alias for canonical `name`:

| Alias operation | Mean | Allocated |
| --- | --- | --- |
| Compiled JSON decode through `Name` | 3.43 us | 2.59 KB |
| `Schema.parse` through `Name` | 8.20 us | 9.66 KB |
| Reject canonical `name` plus alias `Name` | 7.13 us | 8.80 KB |

Canonical and alias `Schema.parse` timings are indistinguishable within this short run. Alias JSON decoding takes the
general fallback path and therefore retains its field slots; ambiguity detection exits before value parsing completes.

## Scaling cases

The wide-record suite isolates field dispatch and per-field decode state with 24 integer fields:

| Operation | Reified mean / allocated | `System.Text.Json` mean / allocated |
| --- | --- | --- |
| Serialize UTF-8 | 0.74 us / 320 B | 0.71 us / 232 B |
| Deserialize UTF-8 | 1.70 us / 1,072 B | 2.11 us / 744 B |

The integer formatting keeps serialization allocation close even as field count grows. Canonical-order decoding avoids
field dispatch and per-field slots; it was 19% faster than `System.Text.Json` here, while allocating 328 B more because
applying the curried constructor still creates intermediate closures. Reordered or aliased objects use the general
unordered decoder.

The list suite measures a root `int list` without record-field dispatch:

| Items | Reified serialize | `System.Text.Json` serialize | Reified deserialize | `System.Text.Json` deserialize |
| ---: | ---: | ---: | ---: | ---: |
| 10 | 0.21 us / 80 B | 0.23 us / 88 B | 0.31 us / 640 B | 0.49 us / 576 B |
| 1,000 | 17.65 us / 3,952 B | 19.02 us / 3,960 B | 33.23 us / 64,000 B | 39.76 us / 40,464 B |
| 10,000 | 189.34 us / 48,952 B | 153.86 us / 48,960 B | 388.81 us / 640,000 B | 417.31 us / 451,440 B |

Serialization allocation is effectively equal across list sizes. Timing leads varied by list size and remain within
wide short-run error bars, so a longer job is required before treating either serializer as the throughput winner. Reified list decoding is competitive on time but allocates more per item.

## Across .NET versions

Reified's codec is compiled once from the schema; it does not do less work than `System.Text.Json` for
serialization, so treat the two as **competitive**, not "Reified wins," on that path. Decoding is where the
compiled codec's advantage is real and consistent, though it narrows on newer .NET.

Measured 2026-09-29 with BenchmarkDotNet's `medium` job (15 iterations, 2 launches, 10 warmups) against
`ReifiedBenchmarks.Schema.JsonCodecBenchmarks`, the same customer aggregate as the table above, on an otherwise
idle machine. See [`dev-docs/BenchmarkProcess.md`](https://github.com/adz/Reified/blob/main/dev-docs/BenchmarkProcess.md)
for exactly how to reproduce a multi-runtime run like this one, including two BenchmarkDotNet bugs that make a
naive attempt silently benchmark the wrong runtime.

| Path | .NET 8.0.424 | .NET 10.0.300 (this package's target) | .NET 11.0.100-rc.1 |
| --- | --- | --- | --- |
| Deserialize UTF-8 | Reified 2.821 us, STJ 3.262 us (Reified +16%) | Reified 2.285 us, STJ 2.614 us (Reified +14%) | Reified 2.438 us, STJ 2.612 us (Reified +7%) |
| Deserialize string | Reified 2.996 us, STJ 3.425 us (Reified +14%) | Reified 2.470 us, STJ 2.677 us (Reified +8%) | Reified 2.560 us, STJ 2.681 us (Reified +5%) |
| Serialize UTF-8 | Reified 1.471 us, STJ 1.482 us (even) | Reified 1.175 us, STJ 1.243 us (Reified +6%) | Reified 1.231 us, STJ 1.151 us (STJ +7%) |
| Serialize string | Reified 1.531 us, STJ 1.562 us (even) | Reified 1.244 us, STJ 1.274 us (Reified +2%) | Reified 1.282 us, STJ 1.220 us (STJ +5%) |

Reified stays ahead on both deserialize paths across all three runtimes, by a margin that shrinks as the .NET
runtime gets newer, from 16% down to 7% on UTF-8 decode. Serialize is roughly matched on .NET 8 and 10, and on
.NET 11 RC, `System.Text.Json` measured 5-7% ahead on both serialize paths. `System.Text.Json` keeps improving its
own allocation and JIT behavior every release, and this codec's advantage was always thinner on the encode side
(direct integer/string formatting, not a structurally different approach), so this narrowing is expected, not a
regression to chase.

Read "faster than `System.Text.Json`" as a decode-path claim from here on, not a blanket one. Re-run this table
against .NET 11 once it reaches GA; a preview or RC toolchain is not representative of shipped performance.

## Conclusion

- For trusted canonical UTF-8 payloads, Reified decoded both the representative aggregate and the 24-field record
  faster than `System.Text.Json` in this run; see [Across .NET versions](#across-net-versions) for the current,
  multi-runtime figure that this page's homepage and getting-started links quote.
- For untrusted input, boundary parsing costs more because it builds `Data`, checks constraints, and accumulates
  path-aware diagnostics.
- Wide records identify decode slots and field matching as the next optimization target; large lists identify
  per-item decode allocation as a separate scaling target.
- Compile each codec once and reuse it so compilation stays outside the per-payload path.
