namespace Reified.Schema.Contracts

open Reified

open System
open System.Text

/// <summary>Emits the checked-in F# for a resolved contract file: one public record, schema, validate,
/// parse, and Fields module per contract, in declaration order.</summary>
[<RequireQualifiedAccess>]
module Emitter =

    let private fsharpKeywords =
        set
            [ "abstract"; "and"; "as"; "assert"; "base"; "begin"; "class"; "default"; "delegate"; "do"; "done"
              "downcast"; "downto"; "elif"; "else"; "end"; "exception"; "extern"; "false"; "finally"; "fixed"
              "for"; "fun"; "function"; "global"; "if"; "in"; "inherit"; "inline"; "interface"; "internal"
              "lazy"; "let"; "match"; "member"; "module"; "mutable"; "namespace"; "new"; "not"; "null"; "of"
              "open"; "or"; "override"; "private"; "public"; "rec"; "return"; "select"; "static"; "struct"
              "then"; "to"; "true"; "try"; "type"; "upcast"; "use"; "val"; "void"; "when"; "while"; "with"
              "yield"; "const"; "atomic"; "break"; "checked"; "component"; "constraint"; "constructor"
              "continue"; "eager"; "event"; "external"; "functor"; "include"; "method"; "mixin"; "object"
              "parallel"; "process"; "protected"; "pure"; "sealed"; "tailcall"; "trait"; "virtual" ]

    let private escapeIdent (name: string) =
        if fsharpKeywords.Contains name then $"``{name}``" else name

    let private pascal (name: string) =
        if name.Length = 0 then name
        else string (Char.ToUpperInvariant name.[0]) + name.Substring 1

    let private camel (name: string) =
        if name.Length = 0 then name
        else string (Char.ToLowerInvariant name.[0]) + name.Substring 1

    let private localName (qualifiedName: string) =
        qualifiedName.Split('.') |> Array.last

    let private unionCaseRef typeName caseName =
        if localName typeName = caseName then typeName else $"{typeName}.{caseName}"

    let private duCaseName (text: string) =
        text.Split([| '-'; '_'; ' ' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map pascal
        |> String.Concat

    let private escapeString (value: string) =
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\t", "\\t")

    /// The generated DU name for a literal-union or union-block field. The prefix is the contract's
    /// generated type name, so superseded versions keep distinct case types.
    let private caseTypeName contractTypeName fieldName = contractTypeName + pascal fieldName

    let private renderNumericLiteral (kind: PrimitiveType) literal =
        let suffix =
            match kind with
            | PDecimal -> "m"
            | _ -> ""

        match literal with
        | LInt value -> string value + suffix
        | LDecimal value -> value.ToString(Globalization.CultureInfo.InvariantCulture) + "m"
        | LString value -> $"\"{escapeString value}\""
        | LBool value -> if value then "true" else "false"

    let rec private numericKind fieldType =
        match fieldType with
        | Primitive PDecimal -> PDecimal
        | _ -> PInt

    /// Renders one Constraint expression for the constraint list.
    let private renderConstraint fieldType constraint' =
        let sized minName maxName size =
            match constraint' with
            | MinSize n -> $"Constraint.{minName} ({n})"
            | MaxSize n -> $"Constraint.{maxName} ({n})"
            | _ -> size

        match constraint' with
        | AtLeast literal -> $"Constraint.atLeast ({renderNumericLiteral (numericKind fieldType) literal})"
        | GreaterThan literal -> $"Constraint.greaterThan ({renderNumericLiteral (numericKind fieldType) literal})"
        | AtMost literal -> $"Constraint.atMost ({renderNumericLiteral (numericKind fieldType) literal})"
        | LessThan literal -> $"Constraint.lessThan ({renderNumericLiteral (numericKind fieldType) literal})"
        | MultipleOf literal -> $"Constraint.multipleOf ({renderNumericLiteral (numericKind fieldType) literal})"
        | MinSize _
        | MaxSize _ -> sized "minLength" "maxLength" ""
        | ExactLength length -> $"Constraint.length ({length})"
        | LengthRange(minimum, maximum) -> $"Constraint.lengthBetween ({minimum}) ({maximum})"
        | Present -> "Constraint.present"
        | Supplied -> failwith "supply is emitted as Schema.mustSupply, not as a constraint"
        | Pattern value -> $"Constraint.pattern (\"{escapeString value}\")"
        | Distinct -> "Constraint.distinct"
        | CheckRef name -> failwith $"check reference '{name}' should have been rejected by the resolver"

    /// Renders the typed Reified.SchemaDSL form used after an inferred or explicit field.
    let private renderFieldConstraint fieldType constraint' =
        let argument (value: string) = if value.StartsWith "-" then $"({value})" else value

        let sized minName maxName size =
            match constraint' with
            | MinSize n -> $"{minName} {n}"
            | MaxSize n -> $"{maxName} {n}"
            | _ -> size

        match constraint' with
        | AtLeast literal -> $"atLeast {renderNumericLiteral (numericKind fieldType) literal |> argument}"
        | GreaterThan literal -> $"greaterThan {renderNumericLiteral (numericKind fieldType) literal |> argument}"
        | AtMost literal -> $"atMost {renderNumericLiteral (numericKind fieldType) literal |> argument}"
        | LessThan literal -> $"lessThan {renderNumericLiteral (numericKind fieldType) literal |> argument}"
        | MultipleOf literal -> $"multipleOf {renderNumericLiteral (numericKind fieldType) literal |> argument}"
        | MinSize _
        | MaxSize _ -> sized "minLength" "maxLength" ""
        | ExactLength length -> $"Constraint.length {length}"
        | LengthRange(minimum, maximum) -> $"lengthBetween {minimum} {maximum}"
        | Present -> "present"
        | Supplied -> failwith "supply is emitted as Schema.mustSupply, not as a constraint"
        | Pattern value -> $"pattern \"{escapeString value}\""
        | Distinct -> "Constraint.distinct"
        | CheckRef name -> failwith $"check reference '{name}' should have been rejected by the resolver"

    /// The F# type of a field as written in the record. `refTypeName` maps a
    /// pinned contract reference to its generated type name.
    let rec private fsType (refTypeName: ContractRef -> string) contractTypeName fieldName fieldType =
        match fieldType with
        | Primitive PText
        | Primitive PEmail -> "string"
        | Primitive PInt -> "int"
        | Primitive PDecimal -> "decimal"
        | Primitive PBool -> "bool"
        | Primitive PDate -> "System.DateOnly"
        | Primitive PDateTime -> "System.DateTimeOffset"
        | Primitive PGuid -> "System.Guid"
        | Reference reference -> refTypeName reference
        | ListOf element -> $"{fsType refTypeName contractTypeName fieldName element} list"
        | MapOf element -> $"Map<string, {fsType refTypeName contractTypeName fieldName element}>"
        | MapOfTransparentKey(typeName, _, element) -> $"Map<{typeName}, {fsType refTypeName contractTypeName fieldName element}>"
        | LiteralUnion _
        | UnionBlock _ -> caseTypeName contractTypeName fieldName
        | ExternalEnum(typeName, _)
        | ExternalTransparent(typeName, _, _)
        | ExternalUnion(typeName, _, _) -> typeName

    /// The base Schema.* expression for a field's type, before decorations. Self-references (same
    /// contract, same version) lower to Schema.defer over the module's own schema binding.
    let rec private baseValueExpr (refSchemaName: ContractRef -> string) (unionSchemaName: string -> string) (contractName, contractVersion) fieldName fieldType =
        match fieldType with
        | Primitive PText
        | Primitive PEmail -> "Schema.text"
        | Primitive PInt -> "Schema.int"
        | Primitive PDecimal -> "Schema.decimal"
        | Primitive PBool -> "Schema.bool"
        | Primitive PDate -> "Schema.date"
        | Primitive PDateTime -> "Schema.dateTime"
        | Primitive PGuid -> "Schema.guid"
        | Reference reference when reference.RefName = contractName && reference.RefVersion = contractVersion ->
            "Schema.defer (fun () -> schema)"
        | Reference reference -> $"{refSchemaName reference}.schema"
        | ListOf element -> $"Schema.listWith {parenthesize (baseValueExpr refSchemaName unionSchemaName (contractName, contractVersion) fieldName element)}"
        | MapOf element -> $"Schema.mapWith {parenthesize (baseValueExpr refSchemaName unionSchemaName (contractName, contractVersion) fieldName element)}"
        | MapOfTransparentKey(typeName, caseName, element) ->
            let caseRef = unionCaseRef typeName caseName
            $"Schema.mapWithKey {caseRef} (function {caseRef} key -> key) {parenthesize (baseValueExpr refSchemaName unionSchemaName (contractName, contractVersion) fieldName element)}"
        | LiteralUnion _
        | ExternalEnum _ -> $"Schema.enum {camel fieldName}Cases"
        | ExternalTransparent(typeName, caseName, payload) ->
            let underlying = baseValueExpr refSchemaName unionSchemaName (contractName, contractVersion) fieldName payload
            let caseRef = unionCaseRef typeName caseName
            $"Schema.convert {caseRef} (function {caseRef} value -> value) {parenthesize underlying}"
        | UnionBlock(discriminator, _) -> $"Schema.unionWith (UnionRepresentation.Internal \"{escapeString discriminator}\") {camel fieldName}Cases"
        | ExternalUnion(typeName, _, _) -> unionSchemaName typeName

    and private parenthesize (expression: string) =
        if expression.Contains " " then $"({expression})" else expression

    let private joinedDoc (doc: string list) = String.Join(" ", doc)

    let private renderDefault fieldType literal =
        match fieldType, literal with
        | LiteralUnion _, LString value -> failwith $"literal union defaults are rendered by the caller, got \"{value}\""
        | _, LString value -> $"\"{escapeString value}\""
        | _, LBool value -> (if value then "true" else "false")
        | _, (LInt _ | LDecimal _) -> renderNumericLiteral (numericKind fieldType) literal

    /// Renders the complete value expression for one field's schema pipe (excluding field-level constraints).
    let private valueExpr refSchemaName unionSchemaName (contractName, contractVersion, contractTypeName) (field: FieldDecl) =
        let mutable expression = baseValueExpr refSchemaName unionSchemaName (contractName, contractVersion) field.FieldName field.FieldType

        let isOuterConstraint constraint' =
            match constraint' with
            | Supplied -> true
            | Present when field.Optional -> true
            | _ -> false

        let innerConstraints =
            field.Constraints |> List.filter (fst >> isOuterConstraint >> not)

        if field.Optional && not (List.isEmpty innerConstraints) then
            let emailPrefix =
                match field.FieldType with
                | Primitive PEmail -> [ "Constraint.email" ]
                | _ -> []

            let rendered =
                emailPrefix @ (innerConstraints |> List.map (fun (constraint', _) -> renderConstraint field.FieldType constraint'))

            let joined = String.Join("; ", rendered)
            expression <- $"{expression} |> Schema.constrainAll [ {joined} ]"
        elif field.Optional then
            match field.FieldType with
            | Primitive PEmail -> expression <- $"{expression} |> Schema.constrainAll [ Constraint.email ]"
            | _ -> ()

        match field.Doc with
        | [] -> ()
        | doc -> expression <- $"{expression} |> Schema.describe \"{escapeString (joinedDoc doc)}\""

        match field.Format with
        | Some format -> expression <- $"{expression} |> Schema.withFormat (SchemaFormat.create \"{escapeString format}\")"
        | None -> ()

        match field.Default with
        | None -> ()
        | Some literal ->
            let renderedDefault =
                match field.FieldType, literal with
                | LiteralUnion _, LString value -> $"{caseTypeName contractTypeName field.FieldName}.{duCaseName value}"
                | ExternalEnum(typeName, cases), LString value ->
                    let fsCase =
                        cases
                        |> List.tryFind (fun case -> case.EnumTag = value)
                        |> Option.map _.EnumFsCase
                        |> Option.defaultValue (duCaseName value)

                    $"{typeName}.{fsCase}"
                | _ -> renderDefault field.FieldType literal

            expression <- $"{expression} |> Schema.withDefault {renderedDefault}"

        if field.Optional then
            $"Schema.option {parenthesize expression}"
        else
            expression

    /// True when the contract declares that boundary input must be supplied. Supply is decided before a typed
    /// value exists, so it is emitted as a Schema operation rather than as a value constraint.
    let private declaresSupply (field: FieldDecl) =
        field.Constraints |> List.exists (fst >> (=) Supplied)

    let private fieldLevelConstraints (field: FieldDecl) =
        if field.Optional then
            field.Constraints
            |> List.choose (fun (constraint', _) ->
                match constraint' with
                | Present -> Some(renderFieldConstraint field.FieldType constraint')
                | _ -> None)
        else
            let emailPrefix =
                match field.FieldType with
                | Primitive PEmail -> [ "email" ]
                | _ -> []

            emailPrefix
            @ (field.Constraints
               |> List.filter (fun (constraint', _) -> constraint' <> Supplied)
               |> List.map (fun (constraint', _) -> renderFieldConstraint field.FieldType constraint'))

    /// True when SchemaDefaults can resolve the generated F# field type without an explicit value schema.
    let rec private hasCanonicalSchema fieldType =
        match fieldType with
        | Primitive _ -> true
        | ListOf _
        | MapOf _ -> false
        | MapOfTransparentKey _ -> false
        | Reference _
        | LiteralUnion _
        | UnionBlock _
        | ExternalEnum _
        | ExternalTransparent _
        | ExternalUnion _ -> false

    let private canInferField (field: FieldDecl) =
        hasCanonicalSchema field.FieldType
        && List.isEmpty field.Doc
        && Option.isNone field.Format
        && Option.isNone field.Default
        && (not field.Optional || List.isEmpty field.Constraints && field.FieldType <> Primitive PEmail)

    let private fieldTypeOf (field: FieldDecl) =
        let inner = field.FieldType
        inner

    /// Emits one contract file as F# source text. `fileSet` is the whole resolved generation set;
    /// it decides generated type names for references (the latest version of a name keeps the bare
    /// name, superseded versions are suffixed, like ConfigV1).
    let emit (namespaceName: string) (fileSet: ContractFile list) (file: ContractFile) : string =
        let declared = fileSet |> List.collect _.Contracts

        let latestVersions =
            declared
            |> List.groupBy _.QualifiedName
            |> List.map (fun (name, contracts) -> name, contracts |> List.map _.Version |> List.max)
            |> Map.ofList

        // User-owned types keep their actual F# names even when a chain override means the conventional
        // generated name would differ.
        let externalNames =
            declared
            |> List.choose (fun contract ->
                contract.ExternalTypeName
                            |> Option.map (fun name -> (contract.QualifiedName, contract.Version), name))
            |> Map.ofList

        let typeNameOf name version =
            match Map.tryFind (name, version) externalNames with
            | Some externalName -> externalName
            | None ->
                match Map.tryFind name latestVersions with
                | Some latest when latest <> version -> $"{name}V{version}"
                | _ -> name

        let refTypeName (reference: ContractRef) =
            let target =
                declared
                |> List.tryFind (fun contract ->
                    contract.QualifiedName = reference.RefName && contract.Version = reference.RefVersion)

            match target with
            | None -> typeNameOf reference.RefName reference.RefVersion
            | Some target when target.OwnsType || target.ContractName = "" -> typeNameOf reference.RefName reference.RefVersion
            | Some target when file.Module.IsNone && file.Contracts |> List.exists (fun contract -> contract.QualifiedName = target.QualifiedName) ->
                target.ExternalTypeName |> Option.map localName |> Option.defaultValue (typeNameOf reference.RefName reference.RefVersion)
            | Some target -> target.ExternalTypeName |> Option.defaultValue (typeNameOf reference.RefName reference.RefVersion)

        let refSchemaName (reference: ContractRef) =
            let target =
                declared
                |> List.find (fun contract ->
                    contract.QualifiedName = reference.RefName && contract.Version = reference.RefVersion)

            let source =
                fileSet
                |> List.find (fun candidate ->
                    candidate.Contracts |> List.exists (fun contract -> contract.QualifiedName = target.QualifiedName))

            match target.ExternalTypeName, source.Module with
            | Some externalTypeName, Some sourceModule when source.FilePath = file.FilePath -> localName externalTypeName
            | Some externalTypeName, Some sourceModule -> sourceModule + "Schemas." + localName externalTypeName
            | _ -> refTypeName reference
        let rec externalUnionsIn fieldType =
            match fieldType with
            | ExternalUnion(typeName, representation, cases) as union ->
                union
                :: (cases
                    |> List.collect (fun case ->
                        match case.ExtPayload with
                        | ExternalFields fields -> fields |> List.collect (fun field -> externalUnionsIn field.ExtFieldType)
                        | ExternalRecord _
                        | ExternalEmpty -> []))
            | ListOf element
            | MapOf element
            | MapOfTransparentKey(_, _, element)
            | ExternalTransparent(_, _, element) -> externalUnionsIn element
            | Primitive _
            | Reference _
            | LiteralUnion _
            | UnionBlock _
            | ExternalEnum _ -> []

        // A union schema is owned by its first use in project compile order. Nested unions are collected with
        // their enclosing field, so every later use can reference one stable generated binding.
        let unionOwners =
            fileSet
            |> List.collect (fun source ->
                source.Contracts
                |> List.collect (fun contract ->
                    contract.Fields
                    |> List.collect (fun field -> externalUnionsIn field.FieldType)
                    |> List.map (fun union -> source, union)))
            |> List.fold (fun owners (source, union) ->
                match union with
                | ExternalUnion(typeName, _, _) when not (Map.containsKey typeName owners) -> Map.add typeName (source, union) owners
                | _ -> owners) Map.empty

        let unionSchemaName typeName =
            let source, _ = Map.find typeName unionOwners
            let moduleName = localName typeName

            if source.FilePath = file.FilePath then
                moduleName + ".schema"
            else
                match source.Module with
                | Some sourceModule -> sourceModule + "Schemas." + moduleName + ".schema"
                | None -> typeName + ".schema"

        let namespaceName = file.Namespace |> Option.defaultValue namespaceName

        let builder = StringBuilder()
        let line (text: string) = builder.AppendLine text |> ignore
        let fileName = IO.Path.GetFileName file.FilePath

        let versionList =
            file.Contracts
            |> List.map (fun contract -> $"{contract.ContractName}.v{contract.Version}")
            |> fun names -> String.Join(", ", names)

        line "// <auto-generated>"
        line $"//   Generated by reified schemagen from {fileName} ({versionList})."
        line "//   Do not edit directly; edit the contract source and regenerate."
        line "// </auto-generated>"
        match file.Module with
        | Some moduleName -> line $"module {moduleName}Schemas"
        | None -> line $"namespace {namespaceName}"
        line ""
        line "open Reified"

        let emittedUnionSchemas = Collections.Generic.HashSet<string>()

        let rec emitUnionSchema typeName =
            let source, fieldType = Map.find typeName unionOwners

            if source.FilePath = file.FilePath && emittedUnionSchemas.Add typeName then
                match fieldType with
                | ExternalUnion(_, representation, cases) ->
                    // Dependencies must be declared first because F# resolves generated bindings in file order.
                    for case in cases do
                        match case.ExtPayload with
                        | ExternalFields fields ->
                            for field in fields do
                                for nested in externalUnionsIn field.ExtFieldType do
                                    match nested with
                                    | ExternalUnion(nestedTypeName, _, _) -> emitUnionSchema nestedTypeName
                                    | _ -> ()
                        | ExternalRecord _
                        | ExternalEmpty -> ()

                    let unionTypeName = typeName
                    let unionModuleName = localName typeName
                    line ""
                    line "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
                    line "[<RequireQualifiedAccess>]"
                    line $"module {unionModuleName} ="
                    line ""
                    line "    open Reified.SchemaDSL"

                    for case in cases do
                        match case.ExtPayload with
                        | ExternalFields fields ->
                            let payloadType = case.ExtFsCase + "Payload"
                            let helper = camel payloadType

                            line ""
                            line $"    type private {payloadType} ="
                            line "        {"
                            for field in fields do
                                let suffix = if field.ExtOptional then " option" else ""
                                line $"            {field.ExtFieldName}: {fsType refTypeName unionTypeName case.ExtFsCase field.ExtFieldType}{suffix}"
                            line "        }"
                            line ""
                            line $"    let private {helper} ="
                            line $"        schema<{payloadType}> {{"
                            for field in fields do
                                if not field.ExtOptional && hasCanonicalSchema field.ExtFieldType then
                                    line $"            fieldAs \"{escapeString field.ExtWireName}\" _.{field.ExtFieldName}"
                                else
                                    let expression = baseValueExpr refSchemaName unionSchemaName ("", 0) field.ExtFieldName field.ExtFieldType
                                    let expression = if field.ExtOptional then $"Schema.option ({expression})" else expression
                                    line $"            fieldAs \"{escapeString field.ExtWireName}\" _.{field.ExtFieldName} {{"
                                    line $"                withSchema ({expression})"
                                    line "            }"
                            let parameters = fields |> List.map (fun field -> camel field.ExtFieldName) |> String.concat " "
                            let assignments = fields |> List.map (fun field -> $"{field.ExtFieldName} = {camel field.ExtFieldName}") |> String.concat "; "
                            line $"            construct (fun {parameters} -> {{ {assignments} }})"
                            line "        }"
                        | ExternalRecord _
                        | ExternalEmpty -> ()

                    line ""
                    line "    let private cases ="
                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""

                        match case.ExtPayload with
                        | ExternalEmpty ->
                            let predicate =
                                if List.length cases = 1 then $"(function {typeName}.{case.ExtFsCase} -> true)"
                                else $"(function {typeName}.{case.ExtFsCase} -> true | _ -> false)"
                            line $"        {opener}UnionCase.empty \"{escapeString case.ExtTag}\" {typeName}.{case.ExtFsCase} {predicate}{closer}"
                        | ExternalRecord reference ->
                            let extractor =
                                if List.length cases = 1 then $"(function {typeName}.{case.ExtFsCase} payload -> Some payload)"
                                else $"(function {typeName}.{case.ExtFsCase} payload -> Some payload | _ -> None)"
                            line $"        {opener}UnionCase.fields \"{escapeString case.ExtTag}\" {typeName}.{case.ExtFsCase} {extractor} {refSchemaName reference}.schema{closer}"
                        | ExternalFields fields ->
                            let payloadType = case.ExtFsCase + "Payload"
                            let helper = camel payloadType
                            let arguments = fields |> List.map (fun field -> "payload." + field.ExtFieldName) |> String.concat ", "
                            let names = fields |> List.map (fun field -> camel field.ExtFieldName) |> String.concat ", "
                            let construction = $"(fun (payload: {payloadType}) -> {typeName}.{case.ExtFsCase}({arguments}))"
                            let pattern = $"{typeName}.{case.ExtFsCase}({names})"
                            let record = fields |> List.map (fun field -> $"{field.ExtFieldName} = {camel field.ExtFieldName}") |> String.concat "; "
                            let extractor =
                                if List.length cases = 1 then $"(function {pattern} -> Some {{ {record} }})"
                                else $"(function {pattern} -> Some {{ {record} }} | _ -> None)"
                            line $"        {opener}UnionCase.fields \"{escapeString case.ExtTag}\" {construction} {extractor} {helper}{closer}")

                    let representationExpression =
                        match representation with
                        | GeneratedInternal discriminator -> $"UnionRepresentation.Internal \"{escapeString discriminator}\""
                        | GeneratedAdjacent(discriminator, payload, style) -> $"UnionRepresentation.Adjacent(\"{escapeString discriminator}\", \"{escapeString payload}\", UnionPayloadStyle.{style})"
                        | GeneratedExternal(style, unwrapFieldless) ->
                            let unwrap = if unwrapFieldless then "true" else "false"
                            $"UnionRepresentation.External(UnionPayloadStyle.{style}, {unwrap})"

                    line ""
                    line $"    let schema : Schema<{typeName}> ="
                    line $"        Schema.unionWith ({representationExpression}) cases"
                | _ -> ()

        for contract in file.Contracts do
            for field in contract.Fields do
                for union in externalUnionsIn field.FieldType do
                    match union with
                    | ExternalUnion(typeName, _, _) -> emitUnionSchema typeName
                    | _ -> ()

            let contractTypeName =
                if file.Module.IsSome then
                    typeNameOf contract.QualifiedName contract.Version
                else
                    contract.ExternalTypeName
                    |> Option.map localName
                    |> Option.defaultValue (typeNameOf contract.QualifiedName contract.Version)
            let contractModuleName =
                contract.ExternalTypeName
                |> Option.map localName
                |> Option.defaultValue contractTypeName
            let contractRef = $"{fileName}, {contract.ContractName}.v{contract.Version}"

            let rec hasSelfReference fieldType =
                match fieldType with
                | Reference reference -> reference.RefName = contract.QualifiedName && reference.RefVersion = contract.Version
                | ListOf element
                | MapOf element -> hasSelfReference element
                | MapOfTransparentKey(_, _, element) -> hasSelfReference element
                | UnionBlock(_, cases) ->
                    cases |> List.exists (fun case -> case.CaseRef.RefName = contract.QualifiedName && case.CaseRef.RefVersion = contract.Version)
                | ExternalUnion(_, _, cases) ->
                    cases
                    |> List.exists (fun case ->
                        match case.ExtPayload with
                        | ExternalRecord reference -> reference.RefName = contract.QualifiedName && reference.RefVersion = contract.Version
                        | ExternalFields fields -> fields |> List.exists (fun field -> hasSelfReference field.ExtFieldType)
                        | ExternalEmpty -> false)
                | ExternalTransparent(_, _, payload) -> hasSelfReference payload
                | Primitive _
                | LiteralUnion _
                | ExternalEnum _ -> false

            // User-owned record fields are referenced verbatim; generated records normalize to PascalCase.
            let fsFieldName (field: FieldDecl) =
                if contract.OwnsType then pascal field.FieldName else field.FieldName

            let caseFields =
                contract.Fields
                |> List.filter (fun field ->
                    match field.FieldType with
                    | LiteralUnion _
                    | UnionBlock _
                    | ExternalEnum _ -> true
                    | ExternalUnion _ -> false
                    | ExternalTransparent _ -> false
                    | _ -> false)

            if contract.OwnsType then
                // Case DUs come before the record that uses them; user-owned union types already exist.
                for field in caseFields do
                    match field.FieldType with
                    | LiteralUnion _
                    | UnionBlock _ ->
                        line ""
                        line $"/// The \"{field.FieldName}\" cases of {contract.ContractName} ({contractRef})."
                        line "[<RequireQualifiedAccess>]"
                        line $"type {caseTypeName contractTypeName field.FieldName} ="

                        match field.FieldType with
                        | LiteralUnion cases ->
                            for case in cases do
                                line $"    | {duCaseName case}"
                        | UnionBlock(_, cases) ->
                            for case in cases do
                                line $"    | {duCaseName case.CaseTag} of {refTypeName case.CaseRef}"
                        | _ -> ()
                    | _ -> ()

                line ""

                for doc in contract.Doc do
                    line $"/// {doc}"

                line $"type {contractTypeName} ="
                line "    {"

                for field in contract.Fields do
                    for doc in field.Doc do
                        line $"        /// {doc}"

                    let optionSuffix = if field.Optional then " option" else ""
                    line $"        {escapeIdent (fsFieldName field)}: {fsType refTypeName contractTypeName field.FieldName (fieldTypeOf field)}{optionSuffix}"

                line "    }"

            line ""
            line $"/// Schema and boundary functions for {contractTypeName} ({contractRef})."
            line "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
            line "[<RequireQualifiedAccess>]"
            line $"module {contractModuleName} ="
            line ""
            line "    open Reified.SchemaDSL"
            line "    open Reified.ConstraintDSL"

            for field in caseFields do
                line ""

                line $"    let private {camel field.FieldName}Cases ="

                match field.FieldType with
                | LiteralUnion cases ->
                    let du = caseTypeName contractTypeName field.FieldName

                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""
                        line $"        {opener}EnumCase.create \"{escapeString case}\" {du}.{duCaseName case}{closer}")
                | UnionBlock(_, cases) ->
                    let du = caseTypeName contractTypeName field.FieldName

                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""

                        let extractor =
                            if List.length cases = 1 then
                                $"(function {du}.{duCaseName case.CaseTag} payload -> Some payload)"
                            else
                                $"(function {du}.{duCaseName case.CaseTag} payload -> Some payload | _ -> None)"

                        line
                            $"        {opener}UnionCase.fields \"{escapeString case.CaseTag}\" {du}.{duCaseName case.CaseTag} {extractor} {refSchemaName case.CaseRef}.schema{closer}")
                | ExternalEnum(typeName, cases) ->
                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""
                        line $"        {opener}EnumCase.create \"{escapeString case.EnumTag}\" {typeName}.{case.EnumFsCase}{closer}")
                | _ -> ()

            line ""
            line $"    /// The schema declared by {fileName} ({contract.ContractName}.v{contract.Version})."
            let recursion = if contract.Fields |> List.exists (fun field -> hasSelfReference field.FieldType) then " rec" else ""
            line $"    let{recursion} schema : Schema<{contractTypeName}> ="

            let parameters =
                contract.Fields
                |> List.map (fun field -> escapeIdent (camel field.FieldName))
                |> fun names -> String.Join(" ", names)

            let schemaBuilder =
                if recursion = " rec" then
                    "SchemaDSL.schema"
                else
                    "schema"

            line $"        {schemaBuilder}<{contractTypeName}> {{"

            for field in contract.Fields do
                let wire = FieldDecl.wireName field
                let getter =
                    $"(fun (value: {contractTypeName}) -> value.{escapeIdent (fsFieldName field)})"
                let constraints = fieldLevelConstraints field
                let supplies = declaresSupply field

                if canInferField field && List.isEmpty constraints && not supplies then
                    line $"            fieldAs \"{escapeString wire}\" {getter}"
                else
                    line $"            fieldAs \"{escapeString wire}\" {getter} {{"

                    if not (canInferField field) then
                        let value = valueExpr refSchemaName unionSchemaName (contract.QualifiedName, contract.Version, contractTypeName) field
                        line $"                withSchema {parenthesize value}"

                    if supplies then
                        line "                mustSupply"

                    match constraints with
                    | [] -> ()
                    | [ constraint' ] ->
                        line $"                constrain {parenthesize constraint'}"
                    | constraints ->
                        line "                constraints ["

                        for constraint' in constraints do
                            line $"                    {constraint'}"

                        line "                ]"

                    line "            }"

            match contract.Constructor with
            | Some constructorName ->
                line $"            construct (fun {parameters} -> {constructorName} {parameters})"
            | None ->
                line $"            construct (fun {parameters} ->"

                contract.Fields
                |> List.iteri (fun index field ->
                    let opener = if index = 0 then "{ " else "  "
                    let closer = if index = List.length contract.Fields - 1 then " })" else ""
                    line $"                {opener}{contractTypeName}.{escapeIdent (fsFieldName field)} = {escapeIdent (camel field.FieldName)}{closer}")

            line "        }"

            match contract.Doc with
            | [] -> ()
            | doc -> line $"        |> Schema.describe \"{escapeString (joinedDoc doc)}\""

            line ""
            line "    /// Checks a draft built with an ordinary record literal."
            line $"    let validate (draft: {contractTypeName}) : Result<{contractTypeName}, SchemaErrors> ="
            line "        Schema.check schema draft"
            line ""
            line "    /// Parses structured boundary data through the schema."
            line $"    let parse (input: Data) : Result<{contractTypeName}, SchemaErrors> ="
            line "        Schema.parse schema input"

            // The latest version of a multi-version chain gets the Contract wiring. Migrations stay
            // hand-written typed F#: the builder takes each n-1 -> n migration as a parameter, so the
            // grammar never names F# symbols and the compiler enforces the chain.
            let oldestVersion =
                fileSet
                |> List.collect _.Contracts
                |> List.filter (fun candidate -> candidate.QualifiedName = contract.QualifiedName)
                |> List.map _.Version
                |> List.min

            if contract.Version = Map.find contract.QualifiedName latestVersions && oldestVersion < contract.Version then
                line ""
                line "    /// Builds the versioned wire contract; supply each n-1 -> n migration and the version-detection source."
                line "    let contract"

                for step in oldestVersion .. contract.Version - 1 do
                    let fromType = typeNameOf contract.QualifiedName step |> localName
                    let toType = typeNameOf contract.QualifiedName (step + 1) |> localName
                    line $"        (migrateV{step}ToV{step + 1}: {fromType} -> Result<{toType}, MigrationError>)"

                line "        (source: VersionSource)"
                line $"        : Contract<{contractTypeName}> ="
                line $"        Contract.create \"{escapeString contract.ContractName}\" {contract.Version} schema"

                for step in contract.Version - 1 .. -1 .. oldestVersion do
                    line $"        |> Contract.supersedes {step} {typeNameOf contract.QualifiedName step |> localName}.schema migrateV{step}ToV{step + 1}"

                line "        |> Contract.build source"

        builder.ToString().Replace("\r\n", "\n")
