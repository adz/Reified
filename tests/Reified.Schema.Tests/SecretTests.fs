namespace Reified.Tests

open Reified
open Reified.Refinements
open Reified.SchemaDSL
open Xunit

module SecretTests =
    type Credentials = { Password: Secret<string> }
    let private credentialsSchema () = schema<Credentials> { field _.Password; construct (fun password -> { Password = password }) }

    [<Fact>]
    let ``secret rendering is redacted and reveal is explicit`` () =
        let secret = Secret.create "sensitive-sentinel"
        Assert.Equal("sensitive-sentinel", Secret.reveal secret)
        Assert.Equal("<redacted>", string secret)
        Assert.DoesNotContain("sensitive-sentinel", sprintf "%A" secret)
        Assert.False(obj.ReferenceEquals(secret, Secret.create "sensitive-sentinel"))

    [<Fact>]
    let ``canonical secret schemas decode actual values but encode placeholders`` () =
        let codec = Json.compile (credentialsSchema ())
        let value = Json.deserialize codec "{\"password\":\"sensitive-sentinel\"}"
        Assert.Equal("sensitive-sentinel", Secret.reveal value.Password)
        Assert.Equal("{\"password\":\"<redacted>\"}", Json.serialize codec value)
        Assert.Equal("\"<redacted>\"", Json.serialize (Json.compile (Schema.secretWith Schema.int)) (Secret.create 123))

    [<Fact>]
    let ``secret parse and check failures erase messages and inner paths`` () =
        let raw = Schema.text |> Schema.validate (fun value -> Error(SchemaError.Custom("echo", Some value)))
        let secretSchema = Schema.secretWith raw
        let assertSafe result =
            match result with
            | Ok _ -> failwith "Expected rejection"
            | Error errors ->
                Assert.DoesNotContain("sensitive-sentinel", string errors)
                Assert.Equal("<redacted>", SchemaErrors.toList errors |> List.head |> fun issue -> string issue.Error)
        Schema.parse secretSchema (Data.Text "sensitive-sentinel") |> assertSafe
        Schema.check secretSchema (Secret.create "sensitive-sentinel") |> assertSafe
        let mapped = Schema.mapWith raw |> Schema.secretWith
        Schema.parse mapped (Data.Object ["sensitive-sentinel", Data.Text "sensitive-sentinel"]) |> assertSafe

    [<Fact>]
    let ``secret defaults stay sealed and metadata cannot reveal raw defaults`` () =
        let payload = Schema.text |> Schema.withDefault "sensitive-sentinel"
        let secretSchema = Schema.secretWith payload
        let settings = schema<Credentials> { field _.Password { withSchema secretSchema }; construct (fun password -> { Password = password }) }
        match Schema.parse settings (Data.Object []) with
        | Ok value -> Assert.Equal("sensitive-sentinel", Secret.reveal value.Password)
        | Error _ -> failwith "Expected sealed default"
        Assert.Equal("sensitive-sentinel", Schema.defaultValue secretSchema |> Option.get |> Secret.reveal)
        Assert.DoesNotContain("sensitive-sentinel", JsonSchema.generateValue secretSchema)
        Assert.Throws<System.ArgumentException>(fun () -> let inspect: Secret<string> -> string = Schema.inspectUnderlying secretSchema in inspect |> ignore) |> ignore

    [<Fact>]
    let ``secret codec errors and retained inputs never keep payloads`` () =
        let codec = Json.compile (Schema.secretWith Schema.int)
        let failure = Assert.Throws<JsonCodecException>(fun () -> Json.deserialize codec "\"sensitive-sentinel\"" |> ignore)
        Assert.DoesNotContain("sensitive-sentinel", failure.ToString())
        let retained = Schema.parseRetainingInput (credentialsSchema ()) (Data.Object ["password", Data.Text "sensitive-sentinel"])
        Assert.DoesNotContain("sensitive-sentinel", Data.Json.render retained.Input)
