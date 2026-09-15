namespace ReifiedBenchmarks.Schema

open System.Text.Json
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Order
open Reified
open Reified
open Reified.SchemaDSL

module WideModel =
    type WideRecord =
        { F01: int
          F02: int
          F03: int
          F04: int
          F05: int
          F06: int
          F07: int
          F08: int
          F09: int
          F10: int
          F11: int
          F12: int
          F13: int
          F14: int
          F15: int
          F16: int
          F17: int
          F18: int
          F19: int
          F20: int
          F21: int
          F22: int
          F23: int
          F24: int }

    let schema =
        schema<WideRecord> {
            field _.F01
            field _.F02
            field _.F03
            field _.F04
            field _.F05
            field _.F06
            field _.F07
            field _.F08
            field _.F09
            field _.F10
            field _.F11
            field _.F12
            field _.F13
            field _.F14
            field _.F15
            field _.F16
            field _.F17
            field _.F18
            field _.F19
            field _.F20
            field _.F21
            field _.F22
            field _.F23
            field _.F24
            construct (fun f01 f02 f03 f04 f05 f06 f07 f08 f09 f10 f11 f12 f13 f14 f15 f16 f17 f18 f19 f20 f21 f22 f23 f24 ->
                { F01 = f01; F02 = f02; F03 = f03; F04 = f04; F05 = f05; F06 = f06
                  F07 = f07; F08 = f08; F09 = f09; F10 = f10; F11 = f11; F12 = f12
                  F13 = f13; F14 = f14; F15 = f15; F16 = f16; F17 = f17; F18 = f18
                  F19 = f19; F20 = f20; F21 = f21; F22 = f22; F23 = f23; F24 = f24 })
        }

    let sample =
        { F01 = 1; F02 = 2; F03 = 3; F04 = 4; F05 = 5; F06 = 6
          F07 = 7; F08 = 8; F09 = 9; F10 = 10; F11 = 11; F12 = 12
          F13 = 13; F14 = 14; F15 = 15; F16 = 16; F17 = 17; F18 = 18
          F19 = 19; F20 = 20; F21 = 21; F22 = 22; F23 = 23; F24 = 24 }

/// Measures field dispatch and per-field state on a record wider than the typical aggregate.
[<MemoryDiagnoser>]
[<GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type WideRecordBenchmarks() =
    let codec = Json.compile WideModel.schema
    let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    let jsonBytes = Json.serializeBytes codec WideModel.sample

    [<Benchmark(Baseline = true, Description = "Reified serializeBytes (24 fields)")>]
    [<BenchmarkCategory("Serialize")>]
    member _.ReifiedSerialize() = Json.serializeBytes codec WideModel.sample |> ignore

    [<Benchmark(Description = "System.Text.Json SerializeToUtf8Bytes (24 fields)")>]
    [<BenchmarkCategory("Serialize")>]
    member _.SystemTextJsonSerialize() = JsonSerializer.SerializeToUtf8Bytes(WideModel.sample, options) |> ignore

    [<Benchmark(Baseline = true, Description = "Reified deserializeBytes (24 fields)")>]
    [<BenchmarkCategory("Deserialize")>]
    member _.ReifiedDeserialize() = Json.deserializeBytes codec jsonBytes |> ignore

    [<Benchmark(Description = "System.Text.Json Deserialize UTF-8 (24 fields)")>]
    [<BenchmarkCategory("Deserialize")>]
    member _.SystemTextJsonDeserialize() = JsonSerializer.Deserialize<WideModel.WideRecord>(jsonBytes, options) |> ignore

/// Estimates the ceiling for a cached known-layout decoder. The strict scans validate each expected UTF-8
/// property name and integer token but deliberately omit model construction, so they are an optimistic lower bound,
/// not a proposed public decoder.
[<MemoryDiagnoser>]
[<GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type KnownLayoutCeilingBenchmarks() =
    let codec = Json.compile WideModel.schema
    let canonicalNames = [| for index in 1 .. 24 -> "f" + index.ToString("00") |]
    let reverseNames = Array.rev canonicalNames
    let canonicalBytes = Json.serializeBytes codec WideModel.sample
    let reverseBytes =
        reverseNames
        |> Array.map (fun name -> $"\"{name}\":{int (name.Substring 1)}")
        |> String.concat ","
        |> fun fields -> System.Text.Encoding.UTF8.GetBytes("{" + fields + "}")

    let strictScan (expected: string[]) (bytes: byte[]) =
        let mutable reader = Utf8JsonReader(bytes)
        if not (reader.Read()) || reader.TokenType <> JsonTokenType.StartObject then invalidOp "Expected object."
        let mutable total = 0
        for name in expected do
            if not (reader.Read()) || reader.TokenType <> JsonTokenType.PropertyName || not (reader.ValueTextEquals name) then
                invalidOp "Known layout mismatch."
            if not (reader.Read()) || reader.TokenType <> JsonTokenType.Number then invalidOp "Expected integer."
            total <- total + reader.GetInt32()
        if not (reader.Read()) || reader.TokenType <> JsonTokenType.EndObject then invalidOp "Expected object end."
        total

    [<Benchmark(Description = "Current Reified canonical order")>]
    [<BenchmarkCategory("Canonical")>]
    member _.CurrentCanonical() = Json.deserializeBytes codec canonicalBytes |> ignore

    [<Benchmark(Baseline = true, Description = "Strict known canonical scan (ceiling)")>]
    [<BenchmarkCategory("Canonical")>]
    member _.StrictCanonical() = strictScan canonicalNames canonicalBytes |> ignore

    [<Benchmark(Description = "Current Reified reverse order")>]
    [<BenchmarkCategory("Known noncanonical")>]
    member _.CurrentReverse() = Json.deserializeBytes codec reverseBytes |> ignore

    [<Benchmark(Baseline = true, Description = "Strict known reverse scan (ceiling)")>]
    [<BenchmarkCategory("Known noncanonical")>]
    member _.StrictReverse() = strictScan reverseNames reverseBytes |> ignore

/// Measures collection scaling independently of record-field dispatch.
[<MemoryDiagnoser>]
[<GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type IntegerListBenchmarks() =
    let codec = Json.compile (Schema.listWith Schema.int)
    let mutable values = []
    let mutable jsonBytes = [||]

    [<Params(10, 1000, 10000)>]
    member val ItemCount = 0 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        values <- List.init this.ItemCount id
        jsonBytes <- Json.serializeBytes codec values

    [<Benchmark(Baseline = true, Description = "Reified serializeBytes (int list)")>]
    [<BenchmarkCategory("Serialize")>]
    member _.ReifiedSerialize() = Json.serializeBytes codec values |> ignore

    [<Benchmark(Description = "System.Text.Json SerializeToUtf8Bytes (int list)")>]
    [<BenchmarkCategory("Serialize")>]
    member _.SystemTextJsonSerialize() = JsonSerializer.SerializeToUtf8Bytes values |> ignore

    [<Benchmark(Baseline = true, Description = "Reified deserializeBytes (int list)")>]
    [<BenchmarkCategory("Deserialize")>]
    member _.ReifiedDeserialize() = Json.deserializeBytes codec jsonBytes |> ignore

    [<Benchmark(Description = "System.Text.Json Deserialize UTF-8 (int list)")>]
    [<BenchmarkCategory("Deserialize")>]
    member _.SystemTextJsonDeserialize() = JsonSerializer.Deserialize<int list> jsonBytes |> ignore
