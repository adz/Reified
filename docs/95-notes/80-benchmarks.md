---
weight: 80
title: Benchmarks
targetFramework: net8.0
---

# Benchmarks

This page shows what the compiled JSON codec costs compared with `System.Text.Json`, and what full boundary
parsing costs compared with the trusted codec. Those two comparisons are the ones that decide which path an
endpoint should take.

The suites live in
[benchmarks/Reified.Schema.Benchmarks/CodecSuites.fs](https://github.com/adz/Reified/blob/main/benchmarks/Reified.Schema.Benchmarks/CodecSuites.fs).
[scripts/run-benchmarks.sh](https://github.com/adz/Reified/blob/main/scripts/run-benchmarks.sh) prompts before
starting the run so you can stop other processes first.

## Setup

The measured run used:

- .NET SDK 10.0.203
- .NET runtime 10.0.7
- F# 10.0
- BenchmarkDotNet 0.15.8

## JSON codec

The codec suites measure `Reified.Schema.Json` — the JSON codec compiled from a `Schema<'model>` declaration — on a realistic aggregate (seven primitive fields, one nested record, and two collections) against `System.Text.Json` on the same model. Both suites live in [benchmarks/Reified.Schema.Benchmarks/CodecSuites.fs](https://github.com/adz/Reified/blob/main/benchmarks/Reified.Schema.Benchmarks/CodecSuites.fs).

Run them:

```bash
dotnet run -c Release --project benchmarks/Reified.Schema.Benchmarks -- --filter "*JsonCodecBenchmarks*" "*BoundaryParseBenchmarks*"
```


Measured with a BenchmarkDotNet short job on the recorded toolchain:

| Method | Mean | Allocated |
| --- | --- | --- |
| `System.Text.Json Serialize` | 1.44 us | 1.11 KB |
| `Reified Json.serialize` | 1.55 us | 1.44 KB |
| `Reified Json.deserializeBytes` | 2.85 us | 2.46 KB |
| `Reified Json.deserialize` | 3.10 us | 2.84 KB |
| `System.Text.Json Deserialize` | 3.11 us | 2.01 KB |

The codec compiles once per schema and runs with no runtime reflection, so it stays on par with `System.Text.Json`'s reflection-based serializer while remaining AOT- and trimming-safe by construction. `deserializeBytes` skips the string-to-UTF-8 conversion and is the faster decode entry point when the payload already arrives as bytes.

The boundary suite compares the trusted codec against full boundary parsing — `JsonDocument` to `Data` to `Schema.parse` with complete path-aware diagnostics:

| Method | Mean | Allocated |
| --- | --- | --- |
| `Reified Json.deserialize (trusted path)` | 3.15 us | 2.84 KB |
| `JsonDocument + Data + Schema.parse (boundary parsing)` | 19.78 us | 27.71 KB |

That gap is the price of diagnostics, redisplayable structured data, and constraint checking, and it is why the two paths exist: parse untrusted input where the diagnostics pay for themselves, and use the compiled codec for trusted payloads such as internal services, storage, and queues.

## Conclusion

- for trusted payloads — internal services, storage, queues — the compiled codec is the right path, and costs
  about what a reflection-based serializer costs while staying AOT- and trimming-safe.
- for untrusted input, pay for boundary parsing: the diagnostics, redisplayable structured data, and constraint
  checking are the reason the schema exists.
- the codec compiles once per schema declaration, so the compilation cost is not on the per-payload path.
