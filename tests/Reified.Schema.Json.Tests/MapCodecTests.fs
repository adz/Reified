namespace Reified.Schema.Json.Tests

open Reified.Schema.Json
open Reified
open Swensen.Unquote
open Xunit
open Reified.SchemaDSL

/// <summary>Covers the compiled JSON codec's handling of <c>Schema.map</c> dictionary value schemas.</summary>
module MapCodecTests =
    type private Thresholds = { Values: Map<string, decimal> }
    type private LocaleTag = LocaleTag of string
    type private LocalizedText = { Values: Map<LocaleTag, string> }

    let private thresholdsSchema () =
        schema<Thresholds> {
            field _.Values {
                withSchema (Schema.mapWith Schema.decimal)
            }
            construct (fun values -> { Values = values })
        }

    let private localizedTextSchema () =
        schema<LocalizedText> {
            field _.Values {
                withSchema (Schema.mapWithKey LocaleTag (fun (LocaleTag value) -> value) Schema.text)
            }
            construct (fun values -> { Values = values })
        }

    [<Fact>]
    let ``round trips a Map field through the compiled codec`` () =
        let codec = Json.compile (thresholdsSchema ())
        let thresholds = { Values = Map.ofList [ "low", 1.5M; "high", 9.5M ] }

        let json = Json.serialize codec thresholds
        let roundTripped = Json.deserialize codec json

        test <@ roundTripped = thresholds @>

    [<Fact>]
    let ``serializes a Map field as a JSON object`` () =
        let codec = Json.compile (thresholdsSchema ())

        let json = Json.serialize codec { Values = Map.ofList [ "low", 1.5M ] }

        test <@ json.Contains "\"values\":{\"low\":1.5}" @>

    [<Fact>]
    let ``deserializes an empty JSON object into an empty Map`` () =
        let codec = Json.compile (thresholdsSchema ())

        test <@ Json.deserialize codec "{\"values\":{}}" = { Values = Map.empty } @>

    [<Fact>]
    let ``round trips a map with transparent string keys`` () =
        let codec = Json.compile (localizedTextSchema ())
        let value = { Values = Map.ofList [ LocaleTag "en", "Hello"; LocaleTag "fr", "Bonjour" ] }

        test <@ Json.deserialize codec (Json.serialize codec value) = value @>
