namespace Reified.Schema.Contracts.Tests

open Reified.Schema.Contracts
open Swensen.Unquote
open Xunit

/// Specs for the record frontend: F# source with [<DeriveSchema>] records lowering into the shared
/// ContractDecl AST. Parsing is syntax-only, so these tests feed source strings directly.
module RecordsTests =

    let private parse source =
        match Records.parse SchemaNaming.CamelCase "wire.fs" source with
        | Ok file -> file
        | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics

    let private parseErrors source =
        match Records.parse SchemaNaming.CamelCase "wire.fs" source with
        | Ok file -> failwithf "Expected diagnostics, got a clean parse of %d contracts" (List.length file.Contracts)
        | Error diagnostics -> diagnostics |> List.map _.Message

    [<Fact>]
    let ``same-named cross-file schemas fall back to full qualification instead of ambiguous opens`` () =
        let fileA =
            """
module My.A
open Reified.DerivedSchema

[<DeriveSchema>]
type Status = { Code: string }
"""

        let fileB =
            """
module My.B
open Reified.DerivedSchema

[<DeriveSchema>]
type Status = { Message: string }
"""

        let consumer =
            """
module My.Consumer
open Reified.DerivedSchema

[<DeriveSchema>]
type Combined =
    { A: My.A.Status
      B: My.B.Status }
"""

        let files =
            Records.parseSet SchemaNaming.CamelCase [ "a.fs", fileA; "b.fs", fileB; "c.fs", consumer ]
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        let c = files |> List.find (fun file -> file.FilePath = "c.fs")
        let emitted = Emitter.emit "Fallback" files c
        test <@ not (emitted.Contains "open My.ASchemas") @>
        test <@ not (emitted.Contains "open My.BSchemas") @>
        test <@ emitted.Contains "withSchema My.ASchemas.Status.schema" @>
        test <@ emitted.Contains "withSchema My.BSchemas.Status.schema" @>

    [<Fact>]
    let ``a qualified cross-file reference is never shadowed by a same-named local union`` () =
        let otherSource =
            """
module My.Other
open Reified.DerivedSchema

[<DeriveEnum>]
type Status =
    | Open
    | Closed
"""

        let wireSource =
            """
module My.Wire
open Reified.DerivedSchema

[<DeriveUnion>]
type Status =
    | Manual of note: string
    | Auto

[<DeriveSchema>]
type Ticket =
    { Local: Status
      Remote: My.Other.Status }
"""

        let files =
            Records.parseSet SchemaNaming.CamelCase [ "other.fs", otherSource; "wire.fs", wireSource ]
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        let wire = files |> List.find (fun file -> file.FilePath = "wire.fs")
        let emitted = Emitter.emit "Fallback" files wire
        test <@ emitted.Contains "let private remoteCases =" @>
        test <@ emitted.Contains "EnumCase.create \"open\" My.Other.Status.Open" @>
        test <@ emitted.Contains "EnumCase.create \"closed\" My.Other.Status.Closed" @>
        test <@ emitted.Contains "withSchema Status.schema" @>
        test <@ emitted.Contains "case \"manual\" {" @>
        test <@ emitted.Contains "tryExtract tryManualCase" @>
        test <@ not (emitted.Contains "open My.Other") @>

    [<Fact>]
    let ``a union schema module aliases its raw type and shortens case access when safe`` () =
        let wireSource = """
module My.Wire
open Reified.DerivedSchema

[<DeriveUnion "kind">]
type BindingSource =
    | Literal of value: string
    | Ambient

[<DeriveSchema>]
type Ticket = { Source: BindingSource }
"""

        let files =
            Records.parseSet SchemaNaming.CamelCase [ "wire.fs", wireSource ]
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        let wire = files |> List.find (fun file -> file.FilePath = "wire.fs")
        let emitted = Emitter.emit "Fallback" files wire
        test <@ emitted.Contains "open type BindingSource" @>
        test <@ emitted.Contains "let private tryLiteralCase = function\n        | Literal value -> Some value\n        | _ -> None" @>
        test <@ emitted.Contains "construct Literal" @>
        test <@ emitted.Contains "Ambient" @>
        test <@ not (emitted.Contains "My.Wire.BindingSource.Literal") @>
        test <@ emitted.Contains "type private BindingSource = global.My.Wire.BindingSource" @>
        test <@ emitted.Contains "let schema =" @>

    [<Fact>]
    let ``zzz union ownership order probe`` () =
        // A declares BindingSource and uses it itself; B also uses it. B is passed first here to see
        // whether ownership is order-dependent or attributed to the declaring file.
        let fileA = """
module My.A
open Reified.DerivedSchema

[<DeriveUnion>]
type BindingSource =
    | Literal of value: string

[<DeriveSchema>]
type RecordInA = { Source: BindingSource }
"""
        let fileB = """
module My.B
open Reified.DerivedSchema

[<DeriveSchema>]
type RecordInB = { Source: My.A.BindingSource }
"""

        let files =
            Records.parseSet SchemaNaming.CamelCase [ "b.fs", fileB; "a.fs", fileA ]
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        let a = files |> List.find (fun file -> file.FilePath = "a.fs")
        let emittedA = Emitter.emit "Fallback" files a
        printfn "%s" emittedA
        test <@ emittedA.Contains "module BindingSource =" @>

    [<Fact>]
    let ``a declared union emits from its owner even when no local record references it`` () =
        let valuesSource = """
module My.Values
open Reified.DerivedSchema

[<DeriveUnion>]
type VariableValue =
    | Literal of text: string

[<DeriveSchema>]
type Prompt = { Text: string }
"""

        let consumerSource = """
module My.Consumer
open Reified.DerivedSchema

[<DeriveSchema>]
type Container = { Value: My.Values.VariableValue }
"""

        let files =
            Records.parseSet SchemaNaming.CamelCase [ "values.fs", valuesSource; "consumer.fs", consumerSource ]
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        let values = files |> List.find (fun file -> file.FilePath = "values.fs")
        let consumer = files |> List.find (fun file -> file.FilePath = "consumer.fs")
        let valuesOutput = Emitter.emit "Fallback" files values
        let consumerOutput = Emitter.emit "Fallback" files consumer

        test <@ valuesOutput.Contains "module VariableValue =" @>
        test <@ consumerOutput.Contains "withSchema VariableValue.schema" @>

    [<Fact>]
    let ``a bare marked record lowers to a shape-only version-1 contract`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order = { Sku: string; Quantity: int }
"""

        test <@ file.Namespace = Some "My.Wire" @>

        match file.Contracts with
        | [ contract ] ->
            test <@ contract.ContractName = "Order" @>
            test <@ contract.Version = 1 @>
            test <@ not contract.OwnsType @>
            test <@ contract.QualifiedName = "My.Wire.Order" @>
            test <@ contract.ExternalTypeName = Some "My.Wire.Order" @>
            test <@ contract.Fields |> List.map _.FieldName = [ "Sku"; "Quantity" ] @>
            test <@ contract.Fields |> List.map FieldDecl.wireName = [ "sku"; "quantity" ] @>
        | contracts -> failwithf "Expected one contract, got %A" contracts

    [<Fact>]
    let ``unmarked records and other declarations are ignored`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

type NotWire = { A: int }

[<DeriveSchema>]
type Order = { Sku: string }

module Helpers =
    let f x = x + 1
"""

        test <@ file.Contracts |> List.map _.ContractName = [ "Order" ] @>

    [<Fact>]
    let ``attributes lower to the shared constraint vocabulary with exact decimals`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order =
    { [<Pattern "^SKU-">] Sku: string
      [<AtLeast 0.5>] Weight: decimal
      [<Min 1; Distinct>] Tags: string list
      [<Default 3; AtMost 10>] Boxes: int
      [<Email>] Contact: string }
"""

        let fields = file.Contracts.Head.Fields
        let byName name = fields |> List.find (fun field -> field.FieldName = name)

        test <@ (byName "Sku").Constraints |> List.map fst = [ Pattern "^SKU-" ] @>
        test <@ (byName "Weight").Constraints |> List.map fst = [ AtLeast(LDecimal 0.5m) ] @>
        test <@ (byName "Tags").Constraints |> List.map fst = [ MinSize 1; Distinct ] @>
        test <@ (byName "Boxes").Constraints |> List.map fst = [ AtMost(LInt 10) ] @>
        test <@ (byName "Boxes").Default = Some(LInt 3) @>
        test <@ (byName "Contact").FieldType = Primitive PEmail @>

    [<Fact>]
    let ``lifted value and supply attributes lower without extending the contract grammar`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Profile =
    { [<Present; Format "email">] Email: string
      [<Length 8>] Code: string
      [<LengthBetween(2, 5)>] Tags: string list
      [<Supplied>] Referral: string option }
"""

        let fields = file.Contracts.Head.Fields
        let byName name = fields |> List.find (fun field -> field.FieldName = name)

        test <@ (byName "Email").Constraints |> List.map fst = [ Present ] @>
        test <@ (byName "Email").Format = Some "email" @>
        test <@ (byName "Code").Constraints |> List.map fst = [ ExactLength 8 ] @>
        test <@ (byName "Tags").Constraints |> List.map fst = [ LengthRange(2, 5) ] @>
        test <@ (byName "Referral").Constraints |> List.map fst = [ Supplied ] @>

        test <@ Resolver.resolve [ file ] = [] @>

        let emitted = Emitter.emit "Fallback" [ file ] file
        test <@ emitted.Contains "format (SchemaFormat.create \"email\")" @>
        test <@ emitted.Contains "constrain present" @>
        test <@ emitted.Contains "constrain (Constraint.length 8)" @>
        test <@ emitted.Contains "constrain (lengthBetween 2 5)" @>
        // Supply is decided before a typed value exists, so it emits a Schema operation, not a constraint.
        test <@ emitted.Contains "mustSupply" @>

    [<Fact>]
    let ``option fields become optional and doc comments carry through`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

/// An order.
[<DeriveSchema>]
type Order =
    { /// Optional customer note.
      Note: string option }
"""

        let contract = file.Contracts.Head
        test <@ contract.Doc = [ "An order." ] @>
        test <@ contract.Fields.Head.Optional @>
        test <@ contract.Fields.Head.FieldType = Primitive PText @>
        test <@ contract.Fields.Head.Doc = [ "Optional customer note." ] @>

    [<Fact>]
    let ``the XxxVn convention builds a chain only when the bare name is marked`` () =
        let chained =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type OrderV1 = { Sku: string }

[<DeriveSchema>]
type Order = { Sku: string; Quantity: int }
"""

        test <@ chained.Contracts |> List.map (fun c -> c.ContractName, c.Version) = [ "Order", 1; "Order", 2 ] @>

        let standalone =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type ApolloV2 = { Crew: int }
"""

        test <@ standalone.Contracts |> List.map (fun c -> c.ContractName, c.Version) = [ "ApolloV2", 1 ] @>

    [<Fact>]
    let ``chain attribute arguments override the naming convention`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema(Chain = "Order", Version = 1)>]
type LegacyOrder = { Sku: string }

[<DeriveSchema(Chain = "Order", Version = 2)>]
type Order = { Sku: string; Quantity: int }
"""

        let contracts = file.Contracts
        test <@ contracts |> List.map (fun c -> c.ContractName, c.Version, c.ExternalTypeName) = [ "Order", 1, Some "My.Wire.LegacyOrder"; "Order", 2, Some "My.Wire.Order" ] @>

    [<Fact>]
    let ``nullary unions lower to enums and tagged unions to inline unions`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Card = { Number: string }

[<DeriveSchema>]
type Invoice = { Reference: string }

[<DeriveUnion "kind">]
type Source =
    | ByCard of Card
    | ByInvoice of Invoice

type Plan =
    | Free
    | [<SchemaName "super-pro">] Pro

[<DeriveSchema>]
type Signup = { Plan: Plan; Source: Source }
"""

        let fields = file.Contracts |> List.find (fun c -> c.ContractName = "Signup") |> _.Fields

        match (fields |> List.find (fun f -> f.FieldName = "Plan")).FieldType with
        | ExternalEnum(typeName, cases) ->
            test <@ typeName = "Plan" @>
            test <@ cases = [ { EnumTag = "free"; EnumFsCase = "Free" }; { EnumTag = "super-pro"; EnumFsCase = "Pro" } ] @>
        | other -> failwithf "Expected an enum, got %A" other

        match (fields |> List.find (fun f -> f.FieldName = "Source")).FieldType with
        | ExternalUnion(typeName, GeneratedInternal discriminator, cases, _) ->
            test <@ typeName = "Source" @>
            test <@ discriminator = "kind" @>
            test <@ cases |> List.map (fun c -> c.ExtTag, c.ExtFsCase, match c.ExtPayload with ExternalRecord reference -> Some reference.RefName | _ -> None) = [ "byCard", "ByCard", Some "My.Wire.Card"; "byInvoice", "ByInvoice", Some "My.Wire.Invoice" ] @>
        | other -> failwithf "Expected a tagged union, got %A" other

    [<Fact>]
    let ``parameterless derived unions default to type and admit fieldless cases`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Volume = { Amount: decimal }

[<DeriveUnion>]
type Command =
    | Stop
    | Volume of Volume

[<DeriveSchema>]
type Envelope = { Command: Command }
"""

        let field =
            file.Contracts
            |> List.find (fun contract -> contract.ContractName = "Envelope")
            |> _.Fields
            |> List.exactlyOne

        match field.FieldType with
        | ExternalUnion("Command", GeneratedInternal "type", cases, _) ->
            test <@ cases |> List.map (fun case -> case.ExtTag, match case.ExtPayload with ExternalRecord reference -> Some reference.RefName | _ -> None) = [ "stop", None; "volume", Some "My.Wire.Volume" ] @>

            let emitted = Emitter.emit "Fallback" [ file ] file
            // Volume is both a case tag on Command and the sibling record referenced by that very case
            // (`Volume.schema`); `open type Command` would shadow that reference, so this falls back to
            // full qualification instead of the short form used when there is no such collision.
            test <@ not (emitted.Contains "open type Command") @>
            test <@ emitted.Contains "UnionCase.empty \"stop\" Model.Stop" @>
            test <@ emitted.Contains "UnionCase.fields \"volume\" Model.Volume (function Model.Volume payload -> Some payload | _ -> None) Volume.schema" @>
            test <@ emitted.Contains "Schema.union [" @>
        | other -> failwithf "Expected the default internal union, got %A" other

    [<Fact>]
    let ``derived unions lower directly named case fields to typed payload schemas`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveUnion>]
type Command =
    | Stop
    | Volume of amount: decimal
    | Move of x: int * y: int

[<DeriveSchema>]
type Envelope = { Command: Command }
"""

        let field = file.Contracts |> List.exactlyOne |> _.Fields |> List.exactlyOne

        match field.FieldType with
        | ExternalUnion("Command", GeneratedInternal "type", cases, _) ->
            match cases |> List.map _.ExtPayload with
            | [ ExternalEmpty; ExternalFields [ amount ]; ExternalFields [ x; y ] ] ->
                test <@ amount.ExtWireName = "amount" && amount.ExtFieldType = Primitive PDecimal @>
                test <@ (x.ExtWireName, y.ExtWireName) = ("x", "y") @>
            | payloads -> failwithf "Unexpected payloads: %A" payloads

            let emitted = Emitter.emit "Fallback" [ file ] file
            test <@ emitted.Contains "let private tryVolumeCase = function\n        | Volume amount -> Some amount\n        | _ -> None" @>
            test <@ emitted.Contains "type private MoveCasePayload = {\n        x: int\n        y: int\n    }" @>
            test <@ emitted.Contains "case \"volume\" {\n                tryExtract tryVolumeCase" @>
            test <@ emitted.Contains "fieldAs \"amount\" id" @>
            test <@ emitted.Contains "construct Volume" @>
            test <@ emitted.Contains "case \"move\" {\n                tryExtract tryMoveCase" @>
            test <@ emitted.Contains "construct (fun x y -> Move(x, y))" @>
            test <@ not (emitted.Contains "schema<VolumeCasePayload>") @>
        | other -> failwithf "Expected a direct-field internal union, got %A" other

    [<Fact>]
    let ``FullyQualified opts a union out of the short open-type case access`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveUnion(FullyQualified = true)>]
type Command =
    | Stop
    | Volume of amount: decimal

[<DeriveSchema>]
type Envelope = { Command: Command }
"""

        let emitted = Emitter.emit "Fallback" [ file ] file
        test <@ not (emitted.Contains "open type Command") @>
        test <@ emitted.Contains "type private Model = Command" @>
        test <@ emitted.Contains "UnionCase.empty \"stop\" Model.Stop" @>
        test <@ emitted.Contains "let private tryVolumeCase = function\n        | Model.Volume amount -> Some amount\n        | _ -> None" @>
        test <@ emitted.Contains "construct Model.Volume" @>

    [<Fact>]
    let ``a RequireQualifiedAccess union falls back to full qualification, since open type exposes none of its cases`` () =
        // Confirmed against the real compiler: `open type` on a [<RequireQualifiedAccess>] union exposes
        // none of its case tags (FS0039), unlike a plain union where it exposes them all. This must be
        // detected without requiring the user to also write [<DeriveUnion(FullyQualified = true)>].
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<RequireQualifiedAccess>]
[<DeriveUnion>]
type Command =
    | Stop
    | Volume of amount: decimal

[<DeriveSchema>]
type Envelope = { Command: Command }
"""

        let emitted = Emitter.emit "Fallback" [ file ] file
        test <@ not (emitted.Contains "open type Command") @>
        test <@ emitted.Contains "type private Model = Command" @>
        test <@ emitted.Contains "UnionCase.empty \"stop\" Model.Stop" @>
        test <@ emitted.Contains "let private tryVolumeCase = function\n        | Model.Volume amount -> Some amount\n        | _ -> None" @>
        test <@ emitted.Contains "construct Model.Volume" @>

    [<Fact>]
    let ``a union whose case tag collides with the Reified vocabulary falls back to full qualification`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveUnion>]
type Command =
    | Schema
    | Volume of amount: decimal

[<DeriveSchema>]
type Envelope = { Command: Command }
"""

        let emitted = Emitter.emit "Fallback" [ file ] file
        test <@ not (emitted.Contains "open type Command") @>
        test <@ emitted.Contains "UnionCase.empty \"schema\" Model.Schema" @>

    [<Fact>]
    let ``an unmarked single-case union is a transparent value`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

type Command = Volume of decimal

[<DeriveSchema>]
type Envelope = { Command: Command }
"""
        let field = file.Contracts |> List.exactlyOne |> _.Fields |> List.exactlyOne
        match field.FieldType with
        | ExternalTransparent("Command", "Volume", Primitive PDecimal) -> ()
        | other -> failwithf "Expected a transparent value, got %A" other

    [<Fact>]
    let ``derived unions resolve explicit compatibility representations`` () =
        let parseRepresentation attribute =
            let file =
                parse
                    $"""
namespace My.Wire

open Reified.DerivedSchema

[<{attribute}>]
type Command = Volume of amount: decimal

[<DeriveSchema>]
type Envelope = {{ Command: Command }}
"""
            match (file.Contracts |> List.exactlyOne |> _.Fields |> List.exactlyOne).FieldType with
            | ExternalUnion(_, representation, _, _) -> representation, Emitter.emit "Fallback" [ file ] file
            | other -> failwithf "Expected a generated union, got %A" other

        let external, externalCode = parseRepresentation "DeriveUnion(Representation = UnionRepresentationKind.External, PayloadStyle = UnionPayloadStyleKind.UnwrappedSingle, UnwrapFieldless = false)"
        let adjacent, adjacentCode = parseRepresentation "DeriveUnion(Representation = UnionRepresentationKind.Adjacent, PayloadField = \"fields\", PayloadStyle = UnionPayloadStyleKind.Positional)"

        test <@ external = GeneratedExternal("UnwrappedSingle", false) @>
        test <@ externalCode.Contains "UnionRepresentation.External(UnionPayloadStyle.UnwrappedSingle, false)" @>
        test <@ adjacent = GeneratedAdjacent("type", "fields", "Positional") @>
        test <@ adjacentCode.Contains "UnionRepresentation.Adjacent(\"type\", \"fields\", UnionPayloadStyle.Positional)" @>

    [<Fact>]
    let ``unsupported field types are rejected with guidance`` () =
        let messages =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order =
    { Weight: float
      Count: int64
      Rows: int[]
      Pair: int * int }
"""

        test <@ messages |> List.exists (fun m -> m.Contains "'decimal'") @>
        test <@ messages |> List.exists (fun m -> m.Contains "'int'") @>
        test <@ messages |> List.exists (fun m -> m.Contains "use 'list'") @>
        test <@ messages |> List.exists (fun m -> m.Contains "unsupported wire field type") @>

    [<Fact>]
    let ``marked records may live directly in a file-level module but not a nested module`` () =
        let privateRecord =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order = private { Sku: string }
"""

        test <@ privateRecord |> List.exists (fun m -> m.Contains "must be public") @>

        let nested =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

module Inner =
    [<DeriveSchema>]
    type Order = { Sku: string }
"""

        test <@ nested |> List.exists (fun m -> m.Contains "namespace level") @>

        let moduleFile =
            parse
                """
module My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order = { Sku: string }
"""

        test <@ moduleFile.Module = Some "My.Wire" @>
        test <@ moduleFile.Contracts.Head.QualifiedName = "My.Wire.Order" @>
        let emitted = Emitter.emit "Fallback" [ moduleFile ] moduleFile
        test <@ emitted.Contains "module My.WireSchemas" @>

    [<Fact>]
    let ``a project catalogue resolves fully qualified cross-file record references`` () =
        let variablesSource =
            """
module My.Workflow.Variables
open Reified.DerivedSchema
[<DeriveSchema>]
type Variable = { Key: string }

[<DeriveUnion>]
type VariableType =
    | Text
    | Number
"""

        let templatesSource =
            """
module My.Workflow.Templates
open Reified.DerivedSchema
[<DeriveSchema>]
type Template =
    { Variable: My.Workflow.Variables.Variable
      VariableType: My.Workflow.Variables.VariableType }
"""

        let sources = [ ("variables.fs", variablesSource); ("templates.fs", templatesSource) ]

        let files =
            Records.parseSet SchemaNaming.CamelCase sources
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        test <@ Resolver.resolve files = [] @>

        let variables = files |> List.find (fun file -> file.FilePath = "variables.fs")
        let template = files |> List.find (fun file -> file.FilePath = "templates.fs")
        let ownerOutput = Emitter.emit "Fallback" files variables
        let emitted = Emitter.emit "Fallback" files template
        test <@ emitted.Contains "open My.Workflow.VariablesSchemas" @>
        test <@ emitted.Contains "type private Template = global.My.Workflow.Templates.Template" @>
        test <@ emitted.Contains "schema<Template>" @>
        test <@ emitted.Contains "withSchema Variable.schema" @>
        test <@ emitted.Contains "let validate = Schema.check schema" @>
        test <@ emitted.Contains "let parse = Schema.parse schema" @>
        test <@ not (emitted.Contains "My.Workflow.VariablesSchemas.Variable.schema") @>
        // VariableType is declared in variables.fs (which also has a contract of its own), so it is
        // owned there even though nothing in variables.fs itself references it - the consumer just opens it.
        test <@ ownerOutput.Contains "module VariableType =" @>
        test <@ ownerOutput.Contains "type private VariableType = global.My.Workflow.Variables.VariableType" @>
        test <@ ownerOutput.Contains "UnionCase.empty \"text\" Text" @>
        test <@ not (emitted.Contains "UnionCase.empty \"text\"") @>
        test <@ emitted.Contains "withSchema VariableType.schema" @>

    [<Fact>]
    let ``cross-file union references share the schema generated at the first declaration site`` () =
        let sourceFile =
            """
module My.Workflow.Variables
open Reified.DerivedSchema

[<DeriveUnion>]
type BindingSource =
    | Literal of value: string
    | FromTarget of factId: string

[<DeriveSchema>]
type Variable = { Source: BindingSource }
"""

        let consumerFile =
            """
module My.Workflow.Templates
open Reified.DerivedSchema

[<DeriveSchema>]
type Template =
    { Primary: My.Workflow.Variables.BindingSource
      Alternatives: My.Workflow.Variables.BindingSource list }
"""

        let files =
            Records.parseSet SchemaNaming.CamelCase [ "variables.fs", sourceFile; "templates.fs", consumerFile ]
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        let source = files |> List.find (fun file -> file.FilePath = "variables.fs")
        let consumer = files |> List.find (fun file -> file.FilePath = "templates.fs")
        let sourceOutput = Emitter.emit "Fallback" files source
        let consumerOutput = Emitter.emit "Fallback" files consumer

        test <@ sourceOutput.Split("module BindingSource =").Length - 1 = 1 @>
        test <@ consumerOutput.Split("module BindingSource =").Length - 1 = 0 @>
        test <@ consumerOutput.Contains "open My.Workflow.VariablesSchemas" @>
        test <@ consumerOutput.Contains "withSchema BindingSource.schema" @>
        test <@ consumerOutput.Contains "withSchema (Schema.listWith BindingSource.schema)" @>

    [<Fact>]
    let ``a cross-file enum without a generated owner falls back to the consuming file`` () =
        let sharedSource =
            """
module My.Workflow.Shared
open Reified.DerivedSchema

[<DeriveEnum>]
type Priority =
    | Low
    | Medium
    | High
"""

        let wireSource =
            """
module My.Workflow.Wire
open Reified.DerivedSchema

[<DeriveSchema>]
type Task = { Priority: My.Workflow.Shared.Priority }
"""

        let files =
            Records.parseSet SchemaNaming.CamelCase [ "shared.fs", sharedSource; "wire.fs", wireSource ]
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        let wire = files |> List.find (fun file -> file.FilePath = "wire.fs")
        let emitted = Emitter.emit "Fallback" files wire
        test <@ emitted.Contains "module Priority =" @>
        test <@ emitted.Contains "type private Priority = global.My.Workflow.Shared.Priority" @>
        test <@ emitted.Contains "EnumCase.create \"low\" Priority.Low" @>
        test <@ emitted.Contains "withSchema Priority.schema" @>

    [<Fact>]
    let ``a RequireQualifiedAccess union declared in another file also falls back to full qualification`` () =
        // Same check as the same-file case, but through the cross-file known-types catalog path
        // (knownTypesIn), which parses [<RequireQualifiedAccess>] independently of the main pass.
        let sharedSource =
            """
module My.Other
open Reified.DerivedSchema

[<RequireQualifiedAccess>]
[<DeriveUnion>]
type Status =
    | Open
    | Closed
"""

        let wireSource =
            """
module My.Wire
open Reified.DerivedSchema

[<DeriveSchema>]
type Ticket = { Status: My.Other.Status }
"""

        let files =
            Records.parseSet SchemaNaming.CamelCase [ "other.fs", sharedSource; "wire.fs", wireSource ]
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        // other.fs declares only a union, no [<DeriveSchema>] record, so it never gets a .g.fs of its own;
        // the union is owned by wire.fs, the only file that references it.
        let wire = files |> List.find (fun file -> file.FilePath = "wire.fs")
        let emitted = Emitter.emit "Fallback" files wire
        test <@ not (emitted.Contains "open type My.Other.Status") @>
        test <@ emitted.Contains "UnionCase.empty \"open\" Status.Open" @>

    [<Fact>]
    let ``transparent string unions may key derived maps`` () =
        let file =
            parse
                """
module My.Workflow

open Reified.DerivedSchema

type LocaleTag = LocaleTag of string

[<DeriveSchema>]
type Template = { Values: Map<LocaleTag, string> }
"""

        let emitted = Emitter.emit "Fallback" [ file ] file
        test <@ emitted.Contains "module LocaleTag =" @>
        test <@ emitted.Contains "type private LocaleTag = global.My.Workflow.LocaleTag" @>
        test <@ emitted.Contains "let map item = Schema.mapWithKey LocaleTag.LocaleTag" @>
        test <@ emitted.Contains "withSchema (LocaleTag.map Schema.text)" @>

    [<Fact>]
    let ``transparent schemas live beside their declaring file and are reused cross-file`` () =
        let ownerSource =
            """
namespace My.Shared
open Reified.DerivedSchema

type LocaleTag = LocaleTag of string

[<DeriveSchema>]
type Marker = { Name: string }
"""

        let consumerSource =
            """
namespace My.Consumer
open Reified.DerivedSchema

[<DeriveSchema>]
type Catalog =
    { DefaultLocale: My.Shared.LocaleTag
      Values: Map<My.Shared.LocaleTag, string> }
"""

        let files =
            Records.parseSet SchemaNaming.CamelCase [ "shared.fs", ownerSource; "consumer.fs", consumerSource ]
            |> List.map (fun (_, result) ->
                match result with
                | Ok file -> file
                | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics)

        let owner = files |> List.find (fun file -> file.FilePath = "shared.fs")
        let consumer = files |> List.find (fun file -> file.FilePath = "consumer.fs")
        let ownerOutput = Emitter.emit "Fallback" files owner
        let consumerOutput = Emitter.emit "Fallback" files consumer

        test <@ ownerOutput.Contains "module LocaleTag =" @>
        test <@ ownerOutput.Contains "let schema = Schema.convert Model.LocaleTag" @>
        test <@ ownerOutput.Contains "let map item = Schema.mapWithKey Model.LocaleTag" @>
        test <@ consumerOutput.Contains "open My.Shared" @>
        test <@ consumerOutput.Contains "withSchema LocaleTag.schema" @>
        test <@ consumerOutput.Contains "withSchema (LocaleTag.map Schema.text)" @>
        test <@ not (consumerOutput.Contains "Schema.convert Model.LocaleTag") @>

    [<Fact>]
    let ``nested and collection union values receive generated case schemas`` () =
        let file =
            parse
                """
module My.Workflow

open Reified.DerivedSchema

[<DeriveUnion>]
type BindingSource =
    | Literal of value: string
    | FromTarget of factId: string

[<DeriveUnion>]
type BindingPolicy =
    | TemplateOwned of source: BindingSource

[<DeriveSchema>]
type Template =
    { Policy: BindingPolicy
      Sources: Map<string, BindingSource> }
"""

        let emitted = Emitter.emit "Fallback" [ file ] file

        test <@ emitted.Split("module BindingSource =").Length - 1 = 1 @>
        test <@ emitted.Split("module BindingPolicy =").Length - 1 = 1 @>
        test <@ emitted.Contains "let schema =" @>
        test <@ emitted.Contains "let private tryLiteralCase = function\n        | Literal value -> Some value\n        | _ -> None" @>
        test <@ emitted.Contains "fieldAs \"value\" id" @>
        test <@ not (emitted.Contains "schema<{|") @>
        test <@ not (emitted.Contains "withSchema Schema.text") @>
        test <@ emitted.Contains "withSchema BindingSource.schema" @>
        test <@ emitted.Contains "withSchema (Schema.mapWith BindingSource.schema)" @>
        test <@ not (emitted.Contains "sourceCases") @>
        test <@ not (emitted.Contains "sourcesCases") @>

    [<Fact>]
    let ``generic records and non-records cannot be marked`` () =
        let generic =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Box<'t> = { Value: string }
"""

        test <@ generic |> List.exists (fun m -> m.Contains "cannot be generic") @>

        let union =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Choice =
    | A
    | B
"""

        test <@ union |> List.exists (fun m -> m.Contains "unions participate as field types") @>

    [<Fact>]
    let ``references stay within the file and unions need proper payloads`` () =
        let unknownReference =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order = { Location: Elsewhere }
"""

        test <@ unknownReference |> List.exists (fun m -> m.Contains "unknown wire field type 'Elsewhere'") @>

        let badUnionPayload =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveUnion "kind">]
type Source =
    | Inline of string
    | Other

[<DeriveSchema>]
type Order = { Source: Source }
"""

        test <@ badUnionPayload |> List.exists (fun m -> m.Contains "must name every field") @>

    [<Fact>]
    let ``a self-referencing record emits a deferred schema`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Category =
    { Name: string
      Children: Category list }
"""

        test <@ Resolver.resolve [ file ] = [] @>
        let emitted = Emitter.emit "Fallback" [ file ] file
        test <@ emitted.Contains "// Recursive schemas intentionally defer self-reference" @>
        test <@ emitted.Contains "#nowarn \"40\"" @>
        test <@ emitted.Contains "let rec schema" @>
        test <@ emitted.Contains "Schema.listWith (Schema.defer (fun () -> schema))" @>
        test <@ not (emitted.Contains "\ntype Category =") @>

    [<Fact>]
    let ``chain overrides emit against the user's actual type names`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema(Chain = "Order", Version = 1)>]
type LegacyOrder = { Sku: string }

[<DeriveSchema(Chain = "Order", Version = 2)>]
type Order = { Sku: string; Quantity: int }
"""

        test <@ Resolver.resolve [ file ] = [] @>
        let emitted = Emitter.emit "Fallback" [ file ] file
        test <@ emitted.Contains "module LegacyOrder" @>
        test <@ emitted.Contains "type private Model = LegacyOrder" @>
        test <@ emitted.Contains "schema<Model>" @>
        test <@ emitted.Contains "(migrateV1ToV2: LegacyOrder -> Result<Order, MigrationError>)" @>
        test <@ emitted.Contains "|> Contract.supersedes 1 LegacyOrder.schema migrateV1ToV2" @>
        test <@ emitted.Contains "namespace My.Wire" @>

    [<Fact>]
    let ``marked records outside the file's first namespace are rejected`` () =
        let messages =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order = { Sku: string }

namespace My.Other

open Reified.DerivedSchema

[<DeriveSchema>]
type Stray = { A: int }
"""

        test <@ messages |> List.exists (fun m -> m.Contains "'Stray'" && m.Contains "one namespace per wire file") @>

    [<Fact>]
    let ``snake case naming policy applies to fields and enum tags`` () =
        let file =
            match
                Records.parse SchemaNaming.SnakeCase "wire.fs"
                    """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order = { MarketingOptIn: bool }
"""
            with
            | Ok file -> file
            | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics

        test <@ file.Contracts.Head.Fields |> List.map FieldDecl.wireName = [ "marketing_opt_in" ] @>

    [<Fact>]
    let ``a schema constructor member lowers to the contract's constructor`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order =
    { Sku: string; Quantity: int }

    [<SchemaConstructor>]
    static member create sku quantity = { Sku = sku; Quantity = max 1 quantity }
"""

        test <@ file.Contracts.Head.Constructor = Some "Order.create" @>

    [<Fact>]
    let ``a marked record without a constructor attribute has no constructor`` () =
        let file =
            parse
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order = { Sku: string }
"""

        test <@ file.Contracts.Head.Constructor = None @>

    [<Fact>]
    let ``a schema constructor without the derive attribute is rejected`` () =
        let messages =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

type Order =
    { Sku: string }

    [<SchemaConstructor>]
    static member create sku = { Sku = sku }
"""

        test <@ messages |> List.exists (fun m -> m.Contains "[<SchemaConstructor>]" && m.Contains "[<DeriveSchema>]") @>

    [<Fact>]
    let ``a schema constructor on the type itself is rejected with guidance`` () =
        let messages =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema; SchemaConstructor>]
type Order = { Sku: string }
"""

        test <@ messages |> List.exists (fun m -> m.Contains "goes on the static member") @>

    [<Fact>]
    let ``a schema constructor on an instance member is rejected`` () =
        let messages =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order =
    { Sku: string }

    [<SchemaConstructor>]
    member this.create sku = { Sku = sku }
"""

        test <@ messages |> List.exists (fun m -> m.Contains "static member") @>

    [<Fact>]
    let ``marking two schema constructors is rejected`` () =
        let messages =
            parseErrors
                """
namespace My.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order =
    { Sku: string }

    [<SchemaConstructor>]
    static member create sku = { Sku = sku }

    [<SchemaConstructor>]
    static member ofSku sku = { Sku = sku }
"""

        test <@ messages |> List.exists (fun m -> m.Contains "exactly one") @>
