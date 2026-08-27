---
weight: 80
title: Benchmarks
targetFramework: net8.0
---

# Benchmarks

This page records one local run of the JSON codec and boundary parsing suites. The numbers describe this laptop and
toolchain; use them to compare paths and allocations, not as cross-machine performance guarantees.

The suites live in
[benchmarks/Reified.Schema.Benchmarks/CodecSuites.fs](https://github.com/adz/Reified/blob/main/benchmarks/Reified.Schema.Benchmarks/CodecSuites.fs).
[scripts/run-benchmarks.sh](https://github.com/adz/Reified/blob/main/scripts/run-benchmarks.sh) prompts before starting
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
That is enough for a laptop-local directional comparison, but close results should be treated as equivalent until a
longer run shows otherwise.

## JSON codec

The codec suite measures `Reified.Schema.Json` on the same aggregate and representation as `System.Text.Json`: string
against string, and UTF-8 bytes against UTF-8 bytes. The model has seven primitive fields, one nested record, two
collections, and boundary constraints on name and age.

Run them:

```bash
./scripts/run-benchmarks.sh --job short --filter "*"
```

| Operation | Reified mean / allocated | `System.Text.Json` mean / allocated |
| --- | --- | --- |
| Serialize string | 2.42 us / 1,232 B | 2.31 us / 1,136 B |
| Serialize UTF-8 | 2.21 us / 880 B | 2.24 us / 776 B |
| Deserialize string | 5.17 us / 2,912 B | 4.82 us / 2,056 B |
| Deserialize UTF-8 | 4.89 us / 2,520 B | 4.68 us / 2,056 B |

The timings are close enough that this short run does not establish a meaningful throughput winner. Replacing the
integer encoder's per-value temporary array with direct UTF-8 formatting reduced Reified serialization by 240 B on
this model. The remaining gap is 96–104 B when encoding and 464–856 B when decoding. The UTF-8 APIs avoid the string
representation and are the relevant comparison when a payload already arrives as bytes.

Codec compilation is separate from per-payload work:

| Operation | Mean | Allocated |
| --- | --- | --- |
| `Json.compile` for the customer schema | 14.38 us | 14.53 KB |

Compile a codec once and reuse it. Recompiling per payload would dominate the encode path and roughly triple the cost
of one decode on this model.

## Boundary parsing

The boundary suite compares the trusted codec against full boundary parsing — `JsonDocument` to `Data` to `Schema.parse` with complete path-aware diagnostics:

| Operation | Mean | Allocated |
| --- | --- | --- |
| `Reified Json.deserializeBytes` (trusted, end to end) | 5.10 us | 2.46 KB |
| `JsonDocument` + `Data` + `Schema.parse` (boundary, end to end) | 20.06 us | 13.24 KB |

The boundary path was 3.94 times the mean and 5.38 times the managed allocation of trusted UTF-8 decoding in this
run. Stage measurements show where that work lands:

| Boundary stage | Mean | Allocated |
| --- | --- | --- |
| JSON document to `Data` | 5.72 us | 3.56 KB |
| `Schema.parse` invalid `Data` | 13.72 us | 9.23 KB |
| `Schema.parse` valid `Data` | 14.39 us | 9.68 KB |

The stages are diagnostic measurements, not additive accounting: the end-to-end benchmark measures its own complete
operation. The invalid case changes the constrained name to an empty string and measures accumulated error creation.

## Scaling cases

The wide-record suite isolates field dispatch and per-field decode state with 24 integer fields:

| Operation | Reified mean / allocated | `System.Text.Json` mean / allocated |
| --- | --- | --- |
| Serialize UTF-8 | 1.40 us / 320 B | 1.18 us / 232 B |
| Deserialize UTF-8 | 5.94 us / 2,056 B | 3.61 us / 744 B |

The integer formatting change keeps serialization allocation close even as field count grows. Decode allocation grows
with Reified's slot-per-field implementation, and linear field-name matching becomes more visible on a wide record.

The list suite measures a root `int list` without record-field dispatch:

| Items | Reified serialize | `System.Text.Json` serialize | Reified deserialize | `System.Text.Json` deserialize |
| ---: | ---: | ---: | ---: | ---: |
| 10 | 0.346 us / 80 B | 0.454 us / 88 B | 0.507 us / 640 B | 0.862 us / 576 B |
| 1,000 | 30.54 us / 3,952 B | 34.62 us / 3,960 B | 62.17 us / 64,000 B | 65.78 us / 40,464 B |
| 10,000 | 297.97 us / 48,952 B | 264.82 us / 48,960 B | 665.98 us / 640,000 B | 692.21 us / 451,440 B |

Serialization allocation is effectively equal across list sizes after direct integer formatting. The short-run timing
lead changes at 10,000 items, so a longer job is required before treating either serializer as the throughput winner.
Reified list decoding is competitive on time but allocates more per item.

## Conclusion

- For trusted payloads, the compiled codec and `System.Text.Json` had similar throughput on this model; Reified
  allocated more.
- For untrusted input, boundary parsing costs more because it builds `Data`, checks constraints, and accumulates
  path-aware diagnostics.
- Wide records identify decode slots and field matching as the next optimization target; large lists identify
  per-item decode allocation as a separate scaling target.
- Compile each codec once and reuse it so compilation stays outside the per-payload path.
