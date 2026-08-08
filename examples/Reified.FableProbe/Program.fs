module Reified.FableProbe.Program

open Reified
open Reified.Schema.Json

// Runs the same assertions on .NET and on Fable JavaScript. A check that passes here and fails there is the
// failure this probe exists to catch: Fable erases Guid to a string and TimeSpan to a number, its whitespace
// set differs from .NET's, and its strings are UTF-16 while text sizes are counted in code points.

#if FABLE_COMPILER
[<Fable.Core.Emit("JSON.parse($0)")>]
let private parseNativeJson (json: string) : obj = null
#endif

let private require condition message =
    if not condition then
        failwith message

[<EntryPoint>]
let main _ =
    let planSummary = Checks.buildSchemaPlanSummary ()

    require
        (planSummary = [ "0:name"; "1:age" ])
        $"Unexpected schema plan summary: %A{planSummary}"

    printfn "Schema record plan: ok"

    let roundTripped = Checks.runCodecRoundTrip ()

    require
        (roundTripped = ({ Name = "Ada"; Age = 37 }: Checks.SchemaContact))
        $"Unexpected codec round-trip result: %A{roundTripped}"

    printfn "Codec round-trip: ok"

    require
        (Checks.runConstraintSurface ())
        "The type-directed constraint catalogue did not behave correctly."

    printfn "Constraints: ok"

    require
        (Checks.runOperandAgreement ())
        "Operand projection described a constraint differently from .NET."

    printfn "Operand agreement: ok"

    require
        (Checks.runLocalizationSurface ())
        "Localized constraint rendering did not behave correctly."

    printfn "Localization: ok"

#if FABLE_COMPILER
    // Portable parsing must preserve number tokens and duplicate keys, and the native bridge must exist only
    // on this target.
    let parsed = Json.parseData "{\"n\":1.20e+3,\"n\":2}"
    let native = Data.ofJsonValue (parseNativeJson "{\"name\":\"Ada\",\"active\":true}")

    require
        (parsed = Data.Object [ "n", Data.Number "1.20e+3"; "n", Data.Number "2" ])
        $"Unexpected portable Data parse: %A{parsed}"

    require
        (native = Data.Object [ "name", Data.Text "Ada"; "active", Data.Bool true ])
        $"Unexpected native JavaScript JSON conversion: %A{native}"

    printfn "Data JSON boundaries: ok"
#endif

    printfn "Reified Fable probe: ok"
    0
