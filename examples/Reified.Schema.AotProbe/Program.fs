open System
open Reified
open Reified.SchemaDSL

type ProbeFailure(message: string) =
    inherit Exception(message)

module Assert =
    let equal<'value when 'value : equality> (expected: 'value) (actual: 'value) =
        if actual <> expected then
            raise (ProbeFailure(sprintf "Expected %+A but got %+A." expected actual))

type SchemaContact =
    {
        Name: string
        Age: int
    }

type SchemaFieldSummary =
    {
        Order: int
        ExternalName: string
    }

type SummaryChainResult<'model, 'constructorIn, 'constructorOut>(value: obj) =
    interface IRecordPlanState<'model, 'constructorIn, 'constructorOut> with
        member _.Value = value

type SummaryFactory<'model>() =
    interface IRecordPlanCompiler<'model, SchemaFieldSummary list> with
        member _.OnEnd() =
            SummaryChainResult<'model, 'constructor, 'constructor>(box ([]: SchemaFieldSummary list))
            :> IRecordPlanState<_, _, _>

        member _.OnField(order, field: Field<'model, 'field>, head) =
            let fields = head.Value :?> SchemaFieldSummary list
            let name = Field.externalName field |> ExternalFieldName.value
            let fieldSummary = { Order = order; ExternalName = name }

            SummaryChainResult<'model, 'constructorIn, 'next>(box (fields @ [ fieldSummary ]))
            :> IRecordPlanState<_, _, _>

        member _.OnComplete<'constructor, 'constructed>
            (
                _: 'constructor,
                chain: IRecordPlanState<'model, 'constructor, 'constructed>,
                _: 'constructed -> Result<'model, string>
            ) =
            chain.Value :?> SchemaFieldSummary list

let probeSchemaPlan () =
    let schema =
        schema<SchemaContact> {
            field _.Name
            field _.Age
            construct (fun name age -> { Name = name; Age = age })
        }

    Schema.compilePlan (SummaryFactory<SchemaContact>()) schema

let probeBareGetterFields () =
    // The bare field form derives wire names from getter quotations; this proves the quotation
    // pattern-match and the compiled-getter extraction both survive native AOT.
    let schema =
        schema<SchemaContact> {
            field _.Name {
                constrain (Constraint.minLength 1)
            }
            field _.Age
            construct (fun name age -> { Name = name; Age = age })
        }

    let description = Inspect.model schema
    description.Fields |> List.map _.Name |> Assert.equal [ "name"; "age" ]

    let checked' =
        Schema.check schema { Name = "Ada"; Age = 36 }

    checked' |> Assert.equal (Ok { Name = "Ada"; Age = 36 })

let private satisfied (constraint': Constraint<'value>) (value: 'value) =
    Constraint.check constraint' value

let probeTypeDirectedConstraints () =
    // The type-directed catalogue resolves through SRTP dispatchers, which is the part of the design most at risk
    // under native AOT: every shape below must select its overload with no reflection and no generic dictionary.
    satisfied Constraint.present "Ada" |> Assert.equal (Ok())
    satisfied Constraint.blank "  " |> Assert.equal (Ok())
    satisfied Constraint.present (Some 1) |> Assert.equal (Ok())
    satisfied Constraint.blank (None: int option) |> Assert.equal (Ok())
    satisfied Constraint.present (ValueSome 1) |> Assert.equal (Ok())
    satisfied Constraint.blank (ValueNone: int voption) |> Assert.equal (Ok())
    satisfied Constraint.present (System.Nullable 1) |> Assert.equal (Ok())
    satisfied Constraint.blank (System.Nullable<int>()) |> Assert.equal (Ok())
    satisfied Constraint.present [ 1 ] |> Assert.equal (Ok())
    satisfied Constraint.blank ([]: int list) |> Assert.equal (Ok())
    satisfied Constraint.present [| 1 |] |> Assert.equal (Ok())
    satisfied Constraint.blank ([||]: int array) |> Assert.equal (Ok())

    satisfied (Constraint.minLength 2) "Ada" |> Assert.equal (Ok())
    satisfied (Constraint.maxLength 2) [ 1 ] |> Assert.equal (Ok())
    satisfied (Constraint.lengthBetween 1 2) [| 1 |] |> Assert.equal (Ok())

    satisfied (Constraint.optional (Constraint.minLength 2)) (None: string option) |> Assert.equal (Ok())
    satisfied (Constraint.optional (Constraint.minLength 2)) (Some "Ada") |> Assert.equal (Ok())
    satisfied (Constraint.optional (Constraint.greaterThan 0)) (ValueSome 1) |> Assert.equal (Ok())
    satisfied (Constraint.optional (Constraint.greaterThan 0)) (System.Nullable 1) |> Assert.equal (Ok())

    satisfied Constraint.distinct [ 1; 2 ] |> Assert.equal (Ok())
    satisfied (Constraint.contains 1) [ 1; 2 ] |> Assert.equal (Ok())

type WideRecord =
    {
        F1: string
        F2: int
        F3: bool
        F4: string
        F5: int
        F6: bool
        F7: string
        F8: int
        F9: bool
        F10: string
        F11: int
        F12: bool
        F13: string
        F14: int
        F15: bool
        F16: string
        F17: int
        F18: bool
        F19: string
        F20: int
        F21: bool
        F22: string
        F23: int
        F24: bool
    }

let wideSchema =
    // NativeAOT drops instantiations of the F# closures that apply a curried constructor of this many arguments
    // unless the ILC generic-cycle limits shipped in Reified.Schema.props are in force.
    schema<WideRecord> {
            fieldAs "f1" _.F1
            fieldAs "f2" _.F2
            fieldAs "f3" _.F3
            fieldAs "f4" _.F4
            fieldAs "f5" _.F5
            fieldAs "f6" _.F6
            fieldAs "f7" _.F7
            fieldAs "f8" _.F8
            fieldAs "f9" _.F9
            fieldAs "f10" _.F10
            fieldAs "f11" _.F11
            fieldAs "f12" _.F12
            fieldAs "f13" _.F13
            fieldAs "f14" _.F14
            fieldAs "f15" _.F15
            fieldAs "f16" _.F16
            fieldAs "f17" _.F17
            fieldAs "f18" _.F18
            fieldAs "f19" _.F19
            fieldAs "f20" _.F20
            fieldAs "f21" _.F21
            fieldAs "f22" _.F22
            fieldAs "f23" _.F23
            fieldAs "f24" _.F24
            construct (fun f1 f2 f3 f4 f5 f6 f7 f8 f9 f10 f11 f12 f13 f14 f15 f16 f17 f18 f19 f20 f21 f22 f23 f24 -> { F1 = f1; F2 = f2; F3 = f3; F4 = f4; F5 = f5; F6 = f6; F7 = f7; F8 = f8; F9 = f9; F10 = f10; F11 = f11; F12 = f12; F13 = f13; F14 = f14; F15 = f15; F16 = f16; F17 = f17; F18 = f18; F19 = f19; F20 = f20; F21 = f21; F22 = f22; F23 = f23; F24 = f24 })
    }

let wideValue = { F1 = "v1"; F2 = 2; F3 = true; F4 = "v4"; F5 = 5; F6 = true; F7 = "v7"; F8 = 8; F9 = true; F10 = "v10"; F11 = 11; F12 = true; F13 = "v13"; F14 = 14; F15 = true; F16 = "v16"; F17 = 17; F18 = true; F19 = "v19"; F20 = 20; F21 = true; F22 = "v22"; F23 = 23; F24 = true }

let probeWideRecordJson () =
    let codec = Json.compile wideSchema
    Json.serialize codec wideValue |> Json.deserialize codec |> Assert.equal wideValue

let probeWideRecordParse () =
    Data.ofNameValues [ "f1", "v1"; "f2", "2"; "f3", "true"; "f4", "v4"; "f5", "5"; "f6", "true"; "f7", "v7"; "f8", "8"; "f9", "true"; "f10", "v10"; "f11", "11"; "f12", "true"; "f13", "v13"; "f14", "14"; "f15", "true"; "f16", "v16"; "f17", "17"; "f18", "true"; "f19", "v19"; "f20", "20"; "f21", "true"; "f22", "v22"; "f23", "23"; "f24", "true" ]
    |> Schema.parse wideSchema
    |> Result.mapError (fun _ -> "parse failed")
    |> Assert.equal (Ok wideValue)

let probe () =
    probeSchemaPlan ()
    |> Assert.equal
        [
            { Order = 0; ExternalName = "name" }
            { Order = 1; ExternalName = "age" }
        ]

    probeBareGetterFields ()
    probeTypeDirectedConstraints ()
    probeWideRecordJson ()
    probeWideRecordParse ()

[<EntryPoint>]
let main _ =
    probe ()
    0
