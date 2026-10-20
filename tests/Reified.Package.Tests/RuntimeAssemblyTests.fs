module Reified.Tests.RuntimeAssemblyTests

open System
open System.IO
open System.Reflection
open System.Reflection.Emit
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open System.Reflection.PortableExecutable
open Xunit

let private shippedAssemblies () =
    [ "Reified.Constraint"
      "Reified.Data"
      "Reified.Parse"
      "Reified.Refinements"
      "Reified.Result"
      "Reified.Schema"
      "Reified.Schema.Http"
      "Reified.Schema.Testing" ]

let private opCodes () =
    typeof<OpCodes>.GetFields(BindingFlags.Public ||| BindingFlags.Static)
    |> Seq.map (fun field -> field.GetValue null :?> OpCode)
    |> Seq.map (fun opCode -> uint16 opCode.Value, opCode)
    |> Map.ofSeq

let private operandSize (opCode: OpCode) (il: byte array) offset =
    match opCode.OperandType with
    | OperandType.InlineNone -> 0
    | OperandType.ShortInlineBrTarget
    | OperandType.ShortInlineI
    | OperandType.ShortInlineVar -> 1
    | OperandType.InlineVar -> 2
    | OperandType.InlineBrTarget
    | OperandType.InlineField
    | OperandType.InlineI
    | OperandType.InlineMethod
    | OperandType.InlineSig
    | OperandType.InlineString
    | OperandType.InlineTok
    | OperandType.InlineType
    | OperandType.ShortInlineR -> 4
    | OperandType.InlineI8
    | OperandType.InlineR -> 8
    | OperandType.InlineSwitch -> 4 + 4 * BitConverter.ToInt32(il, offset)
    | value -> failwith ("Unknown IL operand type " + string value)

let private referencedTokens (il: byte array) =
    seq {
        let opCodes = opCodes ()
        let mutable offset = 0
        while offset < il.Length do
            let first = il[offset]
            let opCodeValue, instructionSize =
                if first = 0xfeuy then uint16 0xfe00 ||| uint16 il[offset + 1], 2
                else uint16 first, 1
            let opCode = opCodes[opCodeValue]
            let operandOffset = offset + instructionSize
            match opCode.OperandType with
            | OperandType.InlineMethod -> yield BitConverter.ToInt32(il, operandOffset)
            | _ -> ()
            offset <- operandOffset + operandSize opCode il operandOffset
    }

let private typeReferenceName (metadata: MetadataReader) handle =
    let typeReference = metadata.GetTypeReference handle
    metadata.GetString typeReference.Namespace, metadata.GetString typeReference.Name

let private memberParentName (metadata: MetadataReader) (memberReference: MemberReference) =
    match memberReference.Parent.Kind with
    | HandleKind.TypeReference ->
        typeReferenceName metadata (TypeReferenceHandle.op_Explicit memberReference.Parent)
    | HandleKind.TypeSpecification ->
        let specification = metadata.GetTypeSpecification(TypeSpecificationHandle.op_Explicit memberReference.Parent)
        let mutable signature = metadata.GetBlobReader specification.Signature
        if signature.ReadSignatureTypeCode() <> SignatureTypeCode.GenericTypeInstance then "", ""
        else
            signature.ReadSignatureTypeCode() |> ignore
            let handle = signature.ReadTypeHandle()
            match handle.Kind with
            | HandleKind.TypeReference ->
                typeReferenceName metadata (TypeReferenceHandle.op_Explicit handle)
            | _ -> "", ""
    | _ -> "", ""

let private isPrintfMember (metadata: MetadataReader) handle =
    let memberReference = metadata.GetMemberReference handle
    let memberName = metadata.GetString memberReference.Name
    let typeNamespace, typeName = memberParentName metadata memberReference
    typeNamespace = "Microsoft.FSharp.Core"
    && ((typeName = "PrintfModule")
        || (typeName = "ExtraTopLevelOperators"
            && (memberName.StartsWith("PrintFormat", StringComparison.Ordinal)
                || memberName.Contains("printf", StringComparison.OrdinalIgnoreCase)))
        || (typeName.StartsWith("PrintfFormat`", StringComparison.Ordinal) && memberName = ".ctor"))

let rec private resolvesToPrintfMember (metadata: MetadataReader) (handle: EntityHandle) =
    match handle.Kind with
    | HandleKind.MemberReference ->
        isPrintfMember metadata (MemberReferenceHandle.op_Explicit handle)
    | HandleKind.MethodSpecification ->
        let specification = metadata.GetMethodSpecification(MethodSpecificationHandle.op_Explicit handle)
        resolvesToPrintfMember metadata specification.Method
    | _ -> false

let private declaringTypeName (metadata: MetadataReader) (definition: TypeDefinition) =
    let typeName = metadata.GetString definition.Name
    let typeNamespace = metadata.GetString definition.Namespace
    if String.IsNullOrEmpty typeNamespace then typeName else typeNamespace + "." + typeName

let private printfCallers assemblyPath =
    use stream = File.OpenRead assemblyPath
    use peReader = new PEReader(stream)
    let metadata = peReader.GetMetadataReader()
    [ for typeHandle in metadata.TypeDefinitions do
          let typeDefinition = metadata.GetTypeDefinition typeHandle
          for methodHandle in typeDefinition.GetMethods() do
              let methodDefinition = metadata.GetMethodDefinition methodHandle
              if methodDefinition.RelativeVirtualAddress <> 0 then
                  let body = peReader.GetMethodBody methodDefinition.RelativeVirtualAddress
                  let callsPrintf =
                      body.GetILBytes()
                      |> referencedTokens
                      |> Seq.exists (fun token ->
                          let handle: EntityHandle = MetadataTokens.EntityHandle token
                          resolvesToPrintfMember metadata handle)
                  if callsPrintf then
                      yield declaringTypeName metadata typeDefinition + "." + metadata.GetString methodDefinition.Name ]

type private Marker = class end

let private deliberatelyBadGenericPrintf value = sprintf "%A" value

[<Fact>]
let ``scanner resolves generic printf calls through method specifications`` () =
    deliberatelyBadGenericPrintf 42 |> ignore
    let assemblyPath = typeof<Marker>.Assembly.Location
    use stream = File.OpenRead assemblyPath
    use peReader = new PEReader(stream)
    let metadata = peReader.GetMetadataReader()
    let genericPrintfReferences =
        seq { 1 .. metadata.GetTableRowCount(TableIndex.MethodSpec) }
        |> Seq.map (fun row -> MetadataTokens.EntityHandle(0x2b000000 ||| row))
        |> Seq.filter (resolvesToPrintfMember metadata)
        |> Seq.length
    Assert.True(genericPrintfReferences > 0, "The deliberately bad fixture must contain a printf MethodSpecification.")

    let callers = printfCallers assemblyPath
    Assert.Contains(callers, fun name -> name.EndsWith("deliberatelyBadGenericPrintf", StringComparison.Ordinal))

[<Fact>]
let ``shipped runtime assemblies do not call FSharp Core printf`` () =
    let offenders =
        shippedAssemblies ()
        |> List.collect (fun assemblyName ->
            let path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll")
            printfCallers path |> List.map (fun methodName -> assemblyName + ": " + methodName))

    Assert.True(offenders.IsEmpty, String.concat Environment.NewLine offenders)
