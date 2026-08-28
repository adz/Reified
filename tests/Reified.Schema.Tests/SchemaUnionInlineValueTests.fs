namespace Reified.Tests

open System
open Reified
open Swensen.Unquote
open Xunit
open Reified.SchemaDSL

module SchemaUnionInlineValueTests =
    type private CardDetails = { Number: string }
    type private InvoiceDetails = { Reference: string }

    type private Payment =
        | Card of CardDetails
        | Invoice of InvoiceDetails

    type private Checkout = { Payment: Payment }

    type private Command =
        | Volume of amount: decimal
        | Stop

    let private tryVolumeCase = function
        | Volume amount -> Some amount
        | _ -> None

    let private cardSchema () =
        schema<CardDetails> {
            field _.Number
            construct (fun number -> { Number = number })
        }

    let private invoiceSchema () =
        schema<InvoiceDetails> {
            field _.Reference
            construct (fun reference -> { Reference = reference })
        }

    let private paymentSchema () =
        Schema.unionWith
            (UnionRepresentation.Internal "type")
            [ UnionCase.fields "card" Card (function Card details -> Some details | _ -> None) ((cardSchema ()))
              UnionCase.fields
                  "invoice"
                  Invoice
                  (function Invoice details -> Some details | _ -> None)
                  ((invoiceSchema ())) ]

    [<Fact>]
    let ``union-inline value schema exposes discriminator and spliced case fields`` () =
        let schema =
            schema<Checkout> {
                field _.Payment {
                    withSchema (paymentSchema ())
                }
                construct (fun payment -> { Payment = payment })
            }

        let payment =
            Inspect.model schema
            |> _.Fields
            |> List.exactlyOne

        match payment.Schema.Shape with
        | SchemaShape.Union union ->
            test <@ union.Representation = UnionRepresentation.Internal "type" @>
            test <@ union.Cases |> List.map _.Tag = [ "card"; "invoice" ] @>
            test <@ match union.Cases[0].Shape with UnionCaseShape.Fields payload -> payload.Fields |> List.map _.Name = [ "number" ] | _ -> false @>
            test <@ match union.Cases[1].Shape with UnionCaseShape.Fields payload -> payload.Fields |> List.map _.Name = [ "reference" ] | _ -> false @>
        | _ -> failwith "Expected a union-inline value shape."

    [<Fact>]
    let ``union-inline value schemas lower to json schema oneOf with spliced properties`` () =
        let schema =
            schema<Checkout> {
                field _.Payment {
                    withSchema (paymentSchema ())
                }
                construct (fun payment -> { Payment = payment })
            }

        let generated = JsonSchema.generate schema

        test <@ generated.Contains "\"payment\":{\"oneOf\":[" @>

        test
            <@ generated.Contains
                "{\"type\":\"object\",\"properties\":{\"type\":{\"const\":\"card\"},\"number\":{\"type\":\"string\"}},\"required\":[\"type\",\"number\"]}" @>

        test
            <@ generated.Contains
                "{\"type\":\"object\",\"properties\":{\"type\":{\"const\":\"invoice\"},\"reference\":{\"type\":\"string\"}},\"required\":[\"type\",\"reference\"]}" @>

    [<Fact>]
    let ``unionInline rejects payload field names that collide with the discriminator`` () =
        let colliding =
            schema<CardDetails> {
                fieldAs "type" _.Number
                construct (fun number -> { Number = number })
            }

        Assert.Throws<ArgumentException>(fun () ->
            Schema.unionWith
                (UnionRepresentation.Internal "type")
                [ UnionCase.fields "card" Card (function Card details -> Some details | _ -> None) (colliding) ]
            |> ignore)
        |> ignore

    [<Fact>]
    let ``unionInline rejects payloads that are not nested model schemas`` () =
        Assert.Throws<ArgumentException>(fun () ->
            Schema.unionWith
                (UnionRepresentation.Internal "type")
                [ UnionCase.value
                      "invoice"
                      (fun reference -> Invoice { Reference = reference })
                      (function Invoice details -> Some details.Reference | _ -> None)
                      Schema.text ]
            |> ignore)
        |> ignore

    [<Fact>]
    let ``case builder uses a named extractor with total payload field getters`` () =
        let volumeCase =
            case "volume" {
                tryExtract tryVolumeCase
                fieldAs "amount" id
                construct Volume
            }

        let command =
            Schema.union
                [ volumeCase
                  UnionCase.empty "stop" Stop (function Stop -> true | _ -> false) ]

        test <@ Schema.parse command (Data.objectOfMap (Map.ofList [ "type", Data.Text "volume"; "amount", Data.Number "12.5" ])) = Ok(Volume 12.5m) @>
        test <@ Schema.check command (Volume 12.5m) = Ok(Volume 12.5m) @>

        match Inspect.schema command with
        | { Shape = SchemaShape.Union union } ->
            test <@ match union.Cases[0].Shape with UnionCaseShape.Fields payload -> payload.Fields |> List.map _.Name = [ "amount" ] | _ -> false @>
        | _ -> failwith "Expected a union shape."

        match volumeCase.Definition.Payload with
        | FieldsUnionCase { Shape = NestedValueDefinition(_, source) } ->
            let payloadSchema = source :?> Schema<Command>
            test <@ Option.isSome payloadSchema.RecordPlan @>
        | _ -> failwith "Expected the case block to retain its typed record plan."
