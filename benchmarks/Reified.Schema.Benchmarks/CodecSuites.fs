namespace ReifiedBenchmarks.Schema

open System
open System.Text.Json
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Order
open Reified
open Reified.ConstraintDSL
open Reified.Schema.Json
open Reified.SchemaDSL

/// The shared benchmark model: a realistic aggregate with primitives, a nested record, and collections.
module CodecModel =
    type Address = { Street: string; City: string }

    type Contact = { Label: string; Value: string }

    type Customer =
        { Id: Guid
          Name: string
          Age: int
          Balance: decimal
          Newsletter: bool
          Joined: DateOnly
          LastSeen: DateTimeOffset
          Address: Address
          Contacts: Contact list
          Scores: int list }

    let addressSchema =
        schema<Address> {
            field _.Street
            field _.City
            construct (fun street city -> { Street = street; City = city })
        }

    let contactSchema =
        schema<Contact> {
            field _.Label
            field _.Value
            construct (fun label value -> { Label = label; Value = value })
        }

    let customerSchema =
        schema<Customer> {
            field _.Id
            field _.Name {
                constraints [ present; maxLength 80 ]
            }
            field _.Age {
                constraints [ atLeast 0; atMost 130 ]
            }
            field _.Balance
            field _.Newsletter
            field _.Joined
            field _.LastSeen
            field _.Address {
                withSchema addressSchema
            }
            field _.Contacts {
                withSchema (Schema.listWith contactSchema)
            }
            field _.Scores {
                withSchema (Schema.listWith Schema.int)
            }
            construct (fun id name age balance newsletter joined lastSeen address contacts scores ->
                { Id = id
                  Name = name
                  Age = age
                  Balance = balance
                  Newsletter = newsletter
                  Joined = joined
                  LastSeen = lastSeen
                  Address = address
                  Contacts = contacts
                  Scores = scores })
        }

    let sample =
        { Id = Guid.Parse "7d9a2f5e-95c8-4f2b-b1e3-2f6d3a1c9b42"
          Name = "Ada Lovelace"
          Age = 36
          Balance = 1234.56m
          Newsletter = true
          Joined = DateOnly(2024, 3, 15)
          LastSeen = DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero)
          Address = { Street = "12 Analytical Way"; City = "London" }
          Contacts =
            [ { Label = "email"; Value = "ada@example.com" }
              { Label = "phone"; Value = "+44 20 7946 0000" } ]
          Scores = [ 3; 1; 4; 1; 5 ] }

/// Compares equivalent string and UTF-8 codec operations against System.Text.Json on the same model.
[<MemoryDiagnoser>]
[<GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type JsonCodecBenchmarks() =
    let codec = Json.compile CodecModel.customerSchema

    let serializerOptions =
        let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
        options

    let json = Json.serialize codec CodecModel.sample
    let jsonBytes = Json.serializeBytes codec CodecModel.sample

    [<Benchmark(Baseline = true, Description = "Reified Json.serialize")>]
    [<BenchmarkCategory("Serialize string")>]
    member _.ReifiedSerialize() = Json.serialize codec CodecModel.sample |> ignore

    [<Benchmark(Description = "System.Text.Json Serialize")>]
    [<BenchmarkCategory("Serialize string")>]
    member _.SystemTextJsonSerialize() =
        JsonSerializer.Serialize(CodecModel.sample, serializerOptions) |> ignore

    [<Benchmark(Baseline = true, Description = "Reified Json.serializeBytes")>]
    [<BenchmarkCategory("Serialize UTF-8")>]
    member _.ReifiedSerializeBytes() = Json.serializeBytes codec CodecModel.sample |> ignore

    [<Benchmark(Description = "System.Text.Json SerializeToUtf8Bytes")>]
    [<BenchmarkCategory("Serialize UTF-8")>]
    member _.SystemTextJsonSerializeBytes() =
        JsonSerializer.SerializeToUtf8Bytes(CodecModel.sample, serializerOptions) |> ignore

    [<Benchmark(Baseline = true, Description = "Reified Json.deserialize")>]
    [<BenchmarkCategory("Deserialize string")>]
    member _.ReifiedDeserialize() = Json.deserialize codec json |> ignore

    [<Benchmark(Description = "System.Text.Json Deserialize")>]
    [<BenchmarkCategory("Deserialize string")>]
    member _.SystemTextJsonDeserialize() =
        JsonSerializer.Deserialize<CodecModel.Customer>(json, serializerOptions) |> ignore

    [<Benchmark(Baseline = true, Description = "Reified Json.deserializeBytes")>]
    [<BenchmarkCategory("Deserialize UTF-8")>]
    member _.ReifiedDeserializeBytes() = Json.deserializeBytes codec jsonBytes |> ignore

    [<Benchmark(Description = "System.Text.Json Deserialize UTF-8")>]
    [<BenchmarkCategory("Deserialize UTF-8")>]
    member _.SystemTextJsonDeserializeBytes() =
        JsonSerializer.Deserialize<CodecModel.Customer>(jsonBytes, serializerOptions) |> ignore

/// Compares the trusted codec lane against boundary parsing with full path-aware diagnostics.
[<MemoryDiagnoser>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type BoundaryParseBenchmarks() =
    let codec = Json.compile CodecModel.customerSchema
    let jsonBytes = Json.serializeBytes codec CodecModel.sample

    let data =
        use document = JsonDocument.Parse jsonBytes
        Data.ofJsonDocument document

    let invalidData =
        match data with
        | Data.Object fields ->
            fields
            |> List.map (fun (name, value) -> if name = "name" then name, Data.Text "" else name, value)
            |> Data.Object
        | _ -> failwith "The benchmark fixture must be a JSON object."

    [<Benchmark(Description = "Reified deserializeBytes (trusted, end to end)")>]
    [<BenchmarkCategory("End to end")>]
    member _.CodecDeserialize() = Json.deserializeBytes codec jsonBytes |> ignore

    [<Benchmark(Description = "JsonDocument + Data + Schema.parse (boundary, end to end)")>]
    [<BenchmarkCategory("End to end")>]
    member _.BoundaryParse() =
        use document = JsonDocument.Parse jsonBytes
        let input = Data.ofJsonDocument document
        Schema.parse CodecModel.customerSchema input |> ignore

    [<Benchmark(Description = "Data.ofJsonDocument only")>]
    [<BenchmarkCategory("Boundary stages")>]
    member _.ConvertJsonDocumentToData() =
        use document = JsonDocument.Parse jsonBytes
        Data.ofJsonDocument document |> ignore

    [<Benchmark(Description = "Schema.parse valid Data only")>]
    [<BenchmarkCategory("Boundary stages")>]
    member _.ParseValidData() = Schema.parse CodecModel.customerSchema data |> ignore

    [<Benchmark(Description = "Schema.parse invalid Data only")>]
    [<BenchmarkCategory("Boundary stages")>]
    member _.ParseInvalidData() = Schema.parse CodecModel.customerSchema invalidData |> ignore

/// Measures the one-time schema codec compilation cost separately from per-payload work.
[<MemoryDiagnoser>]
type CodecCompilationBenchmarks() =
    [<Benchmark(Description = "Json.compile customer schema")>]
    member _.Compile() = Json.compile CodecModel.customerSchema |> ignore
