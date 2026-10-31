namespace Reified.Tests

open Reified
open Reified.SchemaDSL
open Xunit

module CaseInsensitiveBoundaryTests =
    type private HeaderSettings = { Headers: Map<string, string> }
    type private CardDetails = { Number: string; Metadata: Map<string, string> }
    type private Payment = Card of CardDetails
    type private Checkout = { Payment: Payment }
    type private ListSettings = { Groups: Map<string, string> list }

    let private headerSettingsSchema () =
        schema<HeaderSettings> {
            field _.Headers { withSchema (Schema.mapWith Schema.text) }
            construct (fun headers -> { Headers = headers })
        }

    let private cardSchema () =
        schema<CardDetails> {
            field _.Number
            field _.Metadata { withSchema (Schema.mapWith Schema.text) }
            construct (fun number metadata -> { Number = number; Metadata = metadata })
        }

    let private checkoutSchema () =
        let payment =
            Schema.unionWith
                (UnionRepresentation.Internal "type")
                [ UnionCase.fields "card" Card (function Card details -> Some details) (cardSchema ()) ]

        schema<Checkout> {
            field _.Payment { withSchema payment }
            construct (fun payment -> { Payment = payment })
        }

    let private listSettingsSchema () =
        schema<ListSettings> {
            field _.Groups { withSchema (Schema.listWith (Schema.mapWith Schema.text)) }
            construct (fun groups -> { Groups = groups })
        }

    [<Fact>]
    let ``case matching leaves map keys untouched`` () =
        let input =
            Data.Object [ "HEADERS", Data.Object [ "X-Trace", Data.Text "a"; "x-trace", Data.Text "b" ] ]

        Assert.Equal(
            Ok { Headers = Map.ofList [ "X-Trace", "a"; "x-trace", "b" ] },
            Schema.parseWith Schema.ignoreKeyCase (headerSettingsSchema ()) input
        )

    [<Fact>]
    let ``case matching leaves map keys in union payloads untouched`` () =
        let input =
            Data.Object [
                "PAYMENT",
                Data.Object [
                    "type", Data.Text "card"
                    "NUMBER", Data.Text "4242"
                    "METADATA", Data.Object [ "X-Trace", Data.Text "a"; "x-trace", Data.Text "b" ]
                ]
            ]

        Assert.Equal(
            Ok
                { Payment =
                    Card
                        { Number = "4242"
                          Metadata = Map.ofList [ "X-Trace", "a"; "x-trace", "b" ] } },
            Schema.parseWith Schema.ignoreKeyCase (checkoutSchema ()) input
        )

    [<Fact>]
    let ``case matching leaves map keys inside lists untouched`` () =
        let input =
            Data.Object [ "GROUPS", Data.List [ Data.Object [ "X-Trace", Data.Text "a"; "x-trace", Data.Text "b" ] ] ]

        Assert.Equal(
            Ok { Groups = [ Map.ofList [ "X-Trace", "a"; "x-trace", "b" ] ] },
            Schema.parseWith Schema.ignoreKeyCase (listSettingsSchema ()) input
        )
