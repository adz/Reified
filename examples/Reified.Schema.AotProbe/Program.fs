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
        F25: string
        F26: int
        F27: bool
        F28: string
        F29: int
        F30: bool
        F31: string
        F32: int
        F33: bool
        F34: string
        F35: int
        F36: bool
        F37: string
        F38: int
        F39: bool
        F40: string
        F41: int
        F42: bool
        F43: string
        F44: int
        F45: bool
        F46: string
        F47: int
        F48: bool
        F49: string
        F50: int
        F51: bool
        F52: string
        F53: int
        F54: bool
        F55: string
        F56: int
        F57: bool
        F58: string
        F59: int
        F60: bool
        F61: string
        F62: int
        F63: bool
        F64: string
        F65: int
        F66: bool
        F67: string
        F68: int
        F69: bool
        F70: string
        F71: int
        F72: bool
        F73: string
        F74: int
        F75: bool
        F76: string
        F77: int
        F78: bool
        F79: string
        F80: int
        F81: bool
        F82: string
        F83: int
        F84: bool
        F85: string
        F86: int
        F87: bool
        F88: string
        F89: int
        F90: bool
        F91: string
        F92: int
        F93: bool
        F94: string
        F95: int
        F96: bool
        F97: string
        F98: int
        F99: bool
        F100: string
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
            fieldAs "f25" _.F25
            fieldAs "f26" _.F26
            fieldAs "f27" _.F27
            fieldAs "f28" _.F28
            fieldAs "f29" _.F29
            fieldAs "f30" _.F30
            fieldAs "f31" _.F31
            fieldAs "f32" _.F32
            fieldAs "f33" _.F33
            fieldAs "f34" _.F34
            fieldAs "f35" _.F35
            fieldAs "f36" _.F36
            fieldAs "f37" _.F37
            fieldAs "f38" _.F38
            fieldAs "f39" _.F39
            fieldAs "f40" _.F40
            fieldAs "f41" _.F41
            fieldAs "f42" _.F42
            fieldAs "f43" _.F43
            fieldAs "f44" _.F44
            fieldAs "f45" _.F45
            fieldAs "f46" _.F46
            fieldAs "f47" _.F47
            fieldAs "f48" _.F48
            fieldAs "f49" _.F49
            fieldAs "f50" _.F50
            fieldAs "f51" _.F51
            fieldAs "f52" _.F52
            fieldAs "f53" _.F53
            fieldAs "f54" _.F54
            fieldAs "f55" _.F55
            fieldAs "f56" _.F56
            fieldAs "f57" _.F57
            fieldAs "f58" _.F58
            fieldAs "f59" _.F59
            fieldAs "f60" _.F60
            fieldAs "f61" _.F61
            fieldAs "f62" _.F62
            fieldAs "f63" _.F63
            fieldAs "f64" _.F64
            fieldAs "f65" _.F65
            fieldAs "f66" _.F66
            fieldAs "f67" _.F67
            fieldAs "f68" _.F68
            fieldAs "f69" _.F69
            fieldAs "f70" _.F70
            fieldAs "f71" _.F71
            fieldAs "f72" _.F72
            fieldAs "f73" _.F73
            fieldAs "f74" _.F74
            fieldAs "f75" _.F75
            fieldAs "f76" _.F76
            fieldAs "f77" _.F77
            fieldAs "f78" _.F78
            fieldAs "f79" _.F79
            fieldAs "f80" _.F80
            fieldAs "f81" _.F81
            fieldAs "f82" _.F82
            fieldAs "f83" _.F83
            fieldAs "f84" _.F84
            fieldAs "f85" _.F85
            fieldAs "f86" _.F86
            fieldAs "f87" _.F87
            fieldAs "f88" _.F88
            fieldAs "f89" _.F89
            fieldAs "f90" _.F90
            fieldAs "f91" _.F91
            fieldAs "f92" _.F92
            fieldAs "f93" _.F93
            fieldAs "f94" _.F94
            fieldAs "f95" _.F95
            fieldAs "f96" _.F96
            fieldAs "f97" _.F97
            fieldAs "f98" _.F98
            fieldAs "f99" _.F99
            fieldAs "f100" _.F100
            construct (fun f1 f2 f3 f4 f5 f6 f7 f8 f9 f10 f11 f12 f13 f14 f15 f16 f17 f18 f19 f20 f21 f22 f23 f24 f25 f26 f27 f28 f29 f30 f31 f32 f33 f34 f35 f36 f37 f38 f39 f40 f41 f42 f43 f44 f45 f46 f47 f48 f49 f50 f51 f52 f53 f54 f55 f56 f57 f58 f59 f60 f61 f62 f63 f64 f65 f66 f67 f68 f69 f70 f71 f72 f73 f74 f75 f76 f77 f78 f79 f80 f81 f82 f83 f84 f85 f86 f87 f88 f89 f90 f91 f92 f93 f94 f95 f96 f97 f98 f99 f100 ->
                {
                F1 = f1
                F2 = f2
                F3 = f3
                F4 = f4
                F5 = f5
                F6 = f6
                F7 = f7
                F8 = f8
                F9 = f9
                F10 = f10
                F11 = f11
                F12 = f12
                F13 = f13
                F14 = f14
                F15 = f15
                F16 = f16
                F17 = f17
                F18 = f18
                F19 = f19
                F20 = f20
                F21 = f21
                F22 = f22
                F23 = f23
                F24 = f24
                F25 = f25
                F26 = f26
                F27 = f27
                F28 = f28
                F29 = f29
                F30 = f30
                F31 = f31
                F32 = f32
                F33 = f33
                F34 = f34
                F35 = f35
                F36 = f36
                F37 = f37
                F38 = f38
                F39 = f39
                F40 = f40
                F41 = f41
                F42 = f42
                F43 = f43
                F44 = f44
                F45 = f45
                F46 = f46
                F47 = f47
                F48 = f48
                F49 = f49
                F50 = f50
                F51 = f51
                F52 = f52
                F53 = f53
                F54 = f54
                F55 = f55
                F56 = f56
                F57 = f57
                F58 = f58
                F59 = f59
                F60 = f60
                F61 = f61
                F62 = f62
                F63 = f63
                F64 = f64
                F65 = f65
                F66 = f66
                F67 = f67
                F68 = f68
                F69 = f69
                F70 = f70
                F71 = f71
                F72 = f72
                F73 = f73
                F74 = f74
                F75 = f75
                F76 = f76
                F77 = f77
                F78 = f78
                F79 = f79
                F80 = f80
                F81 = f81
                F82 = f82
                F83 = f83
                F84 = f84
                F85 = f85
                F86 = f86
                F87 = f87
                F88 = f88
                F89 = f89
                F90 = f90
                F91 = f91
                F92 = f92
                F93 = f93
                F94 = f94
                F95 = f95
                F96 = f96
                F97 = f97
                F98 = f98
                F99 = f99
                F100 = f100
                })
    }

let wideValue =
    {
        F1 = "v1"
        F2 = 2
        F3 = true
        F4 = "v4"
        F5 = 5
        F6 = true
        F7 = "v7"
        F8 = 8
        F9 = true
        F10 = "v10"
        F11 = 11
        F12 = true
        F13 = "v13"
        F14 = 14
        F15 = true
        F16 = "v16"
        F17 = 17
        F18 = true
        F19 = "v19"
        F20 = 20
        F21 = true
        F22 = "v22"
        F23 = 23
        F24 = true
        F25 = "v25"
        F26 = 26
        F27 = true
        F28 = "v28"
        F29 = 29
        F30 = true
        F31 = "v31"
        F32 = 32
        F33 = true
        F34 = "v34"
        F35 = 35
        F36 = true
        F37 = "v37"
        F38 = 38
        F39 = true
        F40 = "v40"
        F41 = 41
        F42 = true
        F43 = "v43"
        F44 = 44
        F45 = true
        F46 = "v46"
        F47 = 47
        F48 = true
        F49 = "v49"
        F50 = 50
        F51 = true
        F52 = "v52"
        F53 = 53
        F54 = true
        F55 = "v55"
        F56 = 56
        F57 = true
        F58 = "v58"
        F59 = 59
        F60 = true
        F61 = "v61"
        F62 = 62
        F63 = true
        F64 = "v64"
        F65 = 65
        F66 = true
        F67 = "v67"
        F68 = 68
        F69 = true
        F70 = "v70"
        F71 = 71
        F72 = true
        F73 = "v73"
        F74 = 74
        F75 = true
        F76 = "v76"
        F77 = 77
        F78 = true
        F79 = "v79"
        F80 = 80
        F81 = true
        F82 = "v82"
        F83 = 83
        F84 = true
        F85 = "v85"
        F86 = 86
        F87 = true
        F88 = "v88"
        F89 = 89
        F90 = true
        F91 = "v91"
        F92 = 92
        F93 = true
        F94 = "v94"
        F95 = 95
        F96 = true
        F97 = "v97"
        F98 = 98
        F99 = true
        F100 = "v100"
    }

let probeWideRecordJson () =
    let codec = Json.compile wideSchema
    Json.serialize codec wideValue |> Json.deserialize codec |> Assert.equal wideValue

let probeWideRecordParse () =
    Data.ofNameValues [
        "f1", "v1"
        "f2", "2"
        "f3", "true"
        "f4", "v4"
        "f5", "5"
        "f6", "true"
        "f7", "v7"
        "f8", "8"
        "f9", "true"
        "f10", "v10"
        "f11", "11"
        "f12", "true"
        "f13", "v13"
        "f14", "14"
        "f15", "true"
        "f16", "v16"
        "f17", "17"
        "f18", "true"
        "f19", "v19"
        "f20", "20"
        "f21", "true"
        "f22", "v22"
        "f23", "23"
        "f24", "true"
        "f25", "v25"
        "f26", "26"
        "f27", "true"
        "f28", "v28"
        "f29", "29"
        "f30", "true"
        "f31", "v31"
        "f32", "32"
        "f33", "true"
        "f34", "v34"
        "f35", "35"
        "f36", "true"
        "f37", "v37"
        "f38", "38"
        "f39", "true"
        "f40", "v40"
        "f41", "41"
        "f42", "true"
        "f43", "v43"
        "f44", "44"
        "f45", "true"
        "f46", "v46"
        "f47", "47"
        "f48", "true"
        "f49", "v49"
        "f50", "50"
        "f51", "true"
        "f52", "v52"
        "f53", "53"
        "f54", "true"
        "f55", "v55"
        "f56", "56"
        "f57", "true"
        "f58", "v58"
        "f59", "59"
        "f60", "true"
        "f61", "v61"
        "f62", "62"
        "f63", "true"
        "f64", "v64"
        "f65", "65"
        "f66", "true"
        "f67", "v67"
        "f68", "68"
        "f69", "true"
        "f70", "v70"
        "f71", "71"
        "f72", "true"
        "f73", "v73"
        "f74", "74"
        "f75", "true"
        "f76", "v76"
        "f77", "77"
        "f78", "true"
        "f79", "v79"
        "f80", "80"
        "f81", "true"
        "f82", "v82"
        "f83", "83"
        "f84", "true"
        "f85", "v85"
        "f86", "86"
        "f87", "true"
        "f88", "v88"
        "f89", "89"
        "f90", "true"
        "f91", "v91"
        "f92", "92"
        "f93", "true"
        "f94", "v94"
        "f95", "95"
        "f96", "true"
        "f97", "v97"
        "f98", "98"
        "f99", "true"
        "f100", "v100"
    ]
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
