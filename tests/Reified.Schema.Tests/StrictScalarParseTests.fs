namespace Reified.Tests

open System
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL
open Swensen.Unquote
open Xunit

module StrictScalarParseTests =
    type Wire = { Name: string; Count: int; Active: bool }

    let private wireSchema =
        schema<Wire> {
            field _.Name
            field _.Count
            field _.Active
            construct (fun name count active -> { Name = name; Count = count; Active = active })
        }

    type private Shape =
        | Circle
        | Square

    type private Event =
        | Started of int
        | Stopped

    [<Fact>]
    let ``parseJson keeps JSON scalar fidelity`` () =
        test <@ Schema.parseJson wireSchema """{ "name": "Ada", "count": 5, "active": true }""" = Ok { Name = "Ada"; Count = 5; Active = true } @>

        // Text fields do not accept numbers or Booleans.
        test <@ Schema.parseJson wireSchema """{ "name": 5, "count": 5, "active": true }""" |> Result.isError @>
        test <@ Schema.parseJson wireSchema """{ "name": true, "count": 5, "active": true }""" |> Result.isError @>

        // Numeric fields do not accept text.
        test <@ Schema.parseJson wireSchema """{ "name": "Ada", "count": "5", "active": true }""" |> Result.isError @>

        // Boolean fields do not accept text.
        test <@ Schema.parseJson wireSchema """{ "name": "Ada", "count": 5, "active": "true" }""" |> Result.isError @>

    [<Fact>]
    let ``parse stays lenient for query and form data`` () =
        let input = Data.Object [ "name", Data.Number "5"; "count", Data.Text "5"; "active", Data.Text "true" ]

        test <@ Schema.parse wireSchema input = Ok { Name = "5"; Count = 5; Active = true } @>

    [<Fact>]
    let ``parseJson reports malformed JSON as one syntax failure`` () =
        match Schema.parseJson wireSchema """{ "name": """ with
        | Error errors ->
            let issues = SchemaErrors.toList errors
            test <@ issues.Length = 1 @>
            test <@ issues[0].Error.ToString().Contains "unexpected end" @>
        | Ok _ -> failwith "Expected a syntax failure."

    [<Fact>]
    let ``tagOf returns the wire tag for union and enum values`` () =
        let enumSchema = Schema.enum [ EnumCase.create "circle" Circle; EnumCase.create "square" Square ]
        test <@ Schema.tagOf enumSchema Circle = "circle" @>
        test <@ Schema.tagOf enumSchema Square = "square" @>

        let unionSchema =
            Schema.unionWith UnionRepresentations.reifiedAdjacent [
                UnionCase.value "started" Started (function Started n -> Some n | _ -> None) Schema.int
                UnionCase.empty "stopped" Stopped (function Stopped -> true | _ -> false)
            ]

        test <@ Schema.tagOf unionSchema (Started 3) = "started" @>
        test <@ Schema.tagOf unionSchema Stopped = "stopped" @>
