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

    let private splitLongEscapedText (maxLength: int) (text: string) =
        let rec loop (chunks: string list) (remaining: string) =
            if remaining.Length <= maxLength then
                List.rev (remaining :: chunks)
            else
                let breakAt = remaining.LastIndexOf(' ', maxLength)

                if breakAt <= 0 then
                    List.rev (remaining :: chunks)
                else
                    let chunk = remaining.Substring(0, breakAt + 1)
                    loop (chunk :: chunks) (remaining.Substring(breakAt + 1))

        loop [] text

    /// Wraps a long generated description literal as an ordinary concatenated F# string. The emitted value
    /// remains byte-for-byte identical; triple-quoted multiline strings would add source indentation/newlines.
    let private wrapDescriptionLine (text: string) =
        if text.Length <= 120 then
            [ text ]
        else
            let markers = [ "Schema.describe \""; "describe \"" ]

            let marker =
                markers
                |> List.choose (fun candidate ->
                    let index = text.IndexOf(candidate, StringComparison.Ordinal)
                    if index < 0 then None else Some(index, candidate))
                |> List.sortBy fst
                |> List.tryHead

            match marker with
            | None -> [ text ]
            | Some(index, marker) ->
                let contentStart = index + marker.Length
                let contentEnd = text.LastIndexOf('"')

                if contentEnd < contentStart then
                    [ text ]
                else
                    let content = text.Substring(contentStart, contentEnd - contentStart)
                    let leading = text.Substring(0, text.Length - text.TrimStart().Length)
                    let continuation = leading + "    "
                    let chunks = splitLongEscapedText (min 72 (max 40 (108 - continuation.Length))) content

                    match chunks with
                    | [ _ ] -> [ text ]
                    | chunks ->
                        [ yield text.Substring(0, contentStart - 1) + "("
                          for index, chunk in List.indexed chunks do
                              let operator = if index = 0 then "" else "+ "
                              yield $"{continuation}{operator}\"{chunk}\""
                          yield leading + ")" + text.Substring(contentEnd + 1) ]

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
        | ExternalUnion(typeName, _, _, _) -> typeName

    /// The base Schema.* expression for a field's type, before decorations. Self-references (same
    /// contract, same version) lower to Schema.defer over the module's own schema binding.
    let rec private baseValueExpr
        (refSchemaName: ContractRef -> string)
        (unionSchemaName: string -> string)
        (namedSchemaName: string -> string option)
        (keyMapName: string -> string option)
        (contractName, contractVersion)
        fieldName
        fieldType =
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
        | ListOf element -> $"Schema.listWith {parenthesize (baseValueExpr refSchemaName unionSchemaName namedSchemaName keyMapName (contractName, contractVersion) fieldName element)}"
        | MapOf element -> $"Schema.mapWith {parenthesize (baseValueExpr refSchemaName unionSchemaName namedSchemaName keyMapName (contractName, contractVersion) fieldName element)}"
        | MapOfTransparentKey(typeName, caseName, element) ->
            let item = parenthesize (baseValueExpr refSchemaName unionSchemaName namedSchemaName keyMapName (contractName, contractVersion) fieldName element)
            match keyMapName typeName with
            | Some helper -> $"{helper} {item}"
            | None ->
                let caseRef = unionCaseRef typeName caseName
                $"Schema.mapWithKey {caseRef} (function {caseRef} key -> key) {item}"
        | LiteralUnion _ -> $"Schema.enum {camel fieldName}Cases"
        | ExternalEnum(typeName, _) -> namedSchemaName typeName |> Option.defaultValue $"Schema.enum {camel fieldName}Cases"
        | ExternalTransparent(typeName, caseName, payload) ->
            match namedSchemaName typeName with
            | Some helper -> helper
            | None ->
                let underlying = baseValueExpr refSchemaName unionSchemaName namedSchemaName keyMapName (contractName, contractVersion) fieldName payload
                let caseRef = unionCaseRef typeName caseName
                $"Schema.convert {caseRef} (function {caseRef} value -> value) {parenthesize underlying}"
        | UnionBlock(discriminator, _) -> $"Schema.unionWith (UnionRepresentation.Internal \"{escapeString discriminator}\") {camel fieldName}Cases"
        | ExternalUnion(typeName, _, _, _) -> unionSchemaName typeName

    and private parenthesize (expression: string) =
        if expression.Contains " " then $"({expression})" else expression

    let private joinedDoc (doc: string list) = String.Join(" ", doc)

    let private renderDefault fieldType literal =
        match fieldType, literal with
        | LiteralUnion _, LString value -> failwith $"literal union defaults are rendered by the caller, got \"{value}\""
        | _, LString value -> $"\"{escapeString value}\""
        | _, LBool value -> (if value then "true" else "false")
        | _, (LInt _ | LDecimal _) -> renderNumericLiteral (numericKind fieldType) literal

    let private renderedFieldDefault contractTypeName (field: FieldDecl) literal =
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

    /// Renders the complete value expression for one field's schema pipe (excluding field-level constraints).
    let private valueExpr refSchemaName unionSchemaName namedSchemaName keyMapName (contractName, contractVersion, contractTypeName) (field: FieldDecl) =
        let mutable expression = baseValueExpr refSchemaName unionSchemaName namedSchemaName keyMapName (contractName, contractVersion) field.FieldName field.FieldType

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
            let renderedDefault = renderedFieldDefault contractTypeName field literal

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
        | ListOf element
        | MapOf element -> hasCanonicalSchema element
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

    /// True when the field block must select its starting schema explicitly rather than applying operations
    /// to the schema resolved from the getter type. Optional-field decorations stay on the inner value schema,
    /// and named/domain types have sibling schema values rather than a discoverable static Schema member.
    let private requiresExplicitSchema (field: FieldDecl) =
        let hasInnerOptionalConstraint =
            field.Constraints
            |> List.exists (fun (constraint', _) -> constraint' <> Supplied && constraint' <> Present)

        not (hasCanonicalSchema field.FieldType)
        || (field.Optional
            && (field.FieldType = Primitive PEmail
                || hasInnerOptionalConstraint
                || not (List.isEmpty field.Doc)
                || Option.isSome field.Format
                || Option.isSome field.Default))

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
            | ExternalUnion(typeName, representation, cases, _) as union ->
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

        let rec referencesIn fieldType =
            match fieldType with
            | Reference reference -> [ reference ]
            | ListOf element
            | MapOf element
            | MapOfTransparentKey(_, _, element)
            | ExternalTransparent(_, _, element) -> referencesIn element
            | UnionBlock(_, cases) -> cases |> List.map _.CaseRef
            | ExternalUnion(_, _, cases, _) ->
                cases
                |> List.collect (fun case ->
                    match case.ExtPayload with
                    | ExternalRecord reference -> [ reference ]
                    | ExternalFields fields -> fields |> List.collect (fun field -> referencesIn field.ExtFieldType)
                    | ExternalEmpty -> [])
            | Primitive _
            | LiteralUnion _
            | ExternalEnum _ -> []

        let rec externalEnumsIn fieldType =
            match fieldType with
            | ExternalEnum(typeName, cases) -> [ typeName, cases ]
            | ListOf element
            | MapOf element
            | MapOfTransparentKey(_, _, element)
            | ExternalTransparent(_, _, element) -> externalEnumsIn element
            | ExternalUnion(_, _, cases, _) ->
                cases
                |> List.collect (fun case ->
                    match case.ExtPayload with
                    | ExternalFields fields -> fields |> List.collect (fun field -> externalEnumsIn field.ExtFieldType)
                    | ExternalRecord _
                    | ExternalEmpty -> [])
            | Primitive _
            | Reference _
            | LiteralUnion _
            | UnionBlock _ -> []

        let rec transparentTypesIn fieldType =
            match fieldType with
            | ExternalTransparent(typeName, caseName, payload) ->
                (typeName, caseName, payload, true, false) :: transparentTypesIn payload
            | MapOfTransparentKey(typeName, caseName, element) ->
                (typeName, caseName, Primitive PText, false, true) :: transparentTypesIn element
            | ListOf element
            | MapOf element -> transparentTypesIn element
            | ExternalUnion(_, _, cases, _) ->
                cases
                |> List.collect (fun case ->
                    match case.ExtPayload with
                    | ExternalFields fields -> fields |> List.collect (fun field -> transparentTypesIn field.ExtFieldType)
                    | ExternalRecord _
                    | ExternalEmpty -> [])
            | Primitive _
            | Reference _
            | LiteralUnion _
            | UnionBlock _
            | ExternalEnum _ -> []

        let namedSchemaTypesIn fieldType =
            let enums = externalEnumsIn fieldType |> List.map fst
            let transparents =
                transparentTypesIn fieldType
                |> List.choose (fun (typeName, _, payload, _, _) ->
                    match payload with
                    | Primitive _ -> Some typeName
                    | _ -> None)

            (enums @ transparents) |> List.distinct

        let declaringFileOf typeName =
            fileSet
            |> List.tryFind (fun candidate ->
                not (List.isEmpty candidate.Contracts) && Set.contains typeName candidate.DeclaredTypes)

        let allFieldTypesByFile =
            fileSet
            |> List.collect (fun source ->
                source.Contracts
                |> List.collect (fun contract -> contract.Fields |> List.map (fun field -> source, field.FieldType)))

        let enumOwners =
            allFieldTypesByFile
            |> List.collect (fun (source, fieldType) -> externalEnumsIn fieldType |> List.map (fun definition -> source, definition))
            |> List.groupBy (fun (_, (typeName, _)) -> typeName)
            |> List.map (fun (typeName, uses) ->
                let firstSource, (_, cases) = List.head uses
                let owner = declaringFileOf typeName |> Option.defaultValue firstSource
                typeName, (owner, cases))
            |> Map.ofList

        // Primitive transparent wrappers have no schema declaration dependencies, so their canonical
        // schema can live beside the raw type. Complex wrappers remain inline until their dependencies can
        // be ordered with the same certainty.
        let transparentOwners =
            allFieldTypesByFile
            |> List.collect (fun (source, fieldType) ->
                transparentTypesIn fieldType
                |> List.filter (fun (_, _, payload, _, _) -> match payload with Primitive _ -> true | _ -> false)
                |> List.map (fun definition -> source, definition))
            |> List.groupBy (fun (_, (typeName, _, _, _, _)) -> typeName)
            |> List.map (fun (typeName, uses) ->
                let firstSource, (_, caseName, payload, _, _) = List.head uses
                let usedAsKey = uses |> List.exists (fun (_, (_, _, _, _, key)) -> key)
                let owner = declaringFileOf typeName |> Option.defaultValue firstSource
                typeName, (owner, caseName, payload, usedAsKey))
            |> Map.ofList

        // Two unrelated external types with the same short name can fall back to the same consumer file.
        // Such a file cannot safely declare both schema modules, so those rare cases retain inline schemas.
        let collidingOwnedTypeNames =
            [ yield! enumOwners |> Map.toList |> List.map (fun (typeName, (owner, _)) -> owner.FilePath, localName typeName)
              yield! transparentOwners |> Map.toList |> List.map (fun (typeName, (owner, _, _, _)) -> owner.FilePath, localName typeName) ]
            |> List.countBy id
            |> List.choose (fun (key, count) -> if count > 1 then Some key else None)
            |> Set.ofList

        let occupiedOwnerModuleNames =
            let contractModules =
                fileSet
                |> List.collect (fun source ->
                    source.Contracts
                    |> List.map (fun contract ->
                        let typeName = contract.ExternalTypeName |> Option.defaultValue contract.QualifiedName
                        source.FilePath, localName typeName))

            let unionModules =
                allFieldTypesByFile
                |> List.collect (fun (source, fieldType) ->
                    externalUnionsIn fieldType
                    |> List.choose (function
                        | ExternalUnion(typeName, _, _, _) ->
                            let owner = declaringFileOf typeName |> Option.defaultValue source
                            Some(owner.FilePath, localName typeName)
                        | _ -> None))

            Set.ofList (contractModules @ unionModules)

        let canEmitNamedType typeName owner =
            not (Set.contains (owner.FilePath, localName typeName) collidingOwnedTypeNames)
            && not (Set.contains (owner.FilePath, localName typeName) occupiedOwnerModuleNames)

        let generatedTypeScope (owner: ContractFile) =
            match owner.Module with
            | Some sourceModule -> sourceModule + "Schemas"
            | None -> owner.Namespace |> Option.defaultValue namespaceName

        let ownedTypeName typeName owner memberName =
            if owner.FilePath = file.FilePath then
                localName typeName + "." + memberName
            else
                generatedTypeScope owner + "." + localName typeName + "." + memberName

        let namedSchemaName typeName =
            match Map.tryFind typeName enumOwners, Map.tryFind typeName transparentOwners with
            | Some(owner, _), _ when canEmitNamedType typeName owner -> Some(ownedTypeName typeName owner "schema")
            | _, Some(owner, _, _, _) when canEmitNamedType typeName owner -> Some(ownedTypeName typeName owner "schema")
            | _ -> None

        let keyMapName typeName =
            match Map.tryFind typeName transparentOwners with
            | Some(owner, _, _, true) when canEmitNamedType typeName owner -> Some(ownedTypeName typeName owner "map")
            | _ -> None

        let namedTypeScope typeName =
            let owner =
                match Map.tryFind typeName enumOwners, Map.tryFind typeName transparentOwners with
                | Some(owner, _), _ -> Some owner
                | _, Some(owner, _, _, _) -> Some owner
                | _ -> None

            match owner with
            | Some owner when canEmitNamedType typeName owner && owner.FilePath <> file.FilePath -> Some(generatedTypeScope owner)
            | _ -> None

        let schemaScope reference =
            let target =
                declared
                |> List.find (fun contract ->
                    contract.QualifiedName = reference.RefName && contract.Version = reference.RefVersion)

            let source =
                fileSet
                |> List.find (fun candidate ->
                    candidate.Contracts |> List.exists (fun contract -> contract.QualifiedName = target.QualifiedName))

            match source.Module with
            | Some sourceModule when source.FilePath <> file.FilePath -> Some(sourceModule + "Schemas")
            | _ -> None

        // A union schema is owned by the file that declares it, whenever that file emits a schema file of
        // its own (i.e. has at least one contract to host it alongside). A union-only file never gets a
        // .g.fs of its own, so falls back to whichever file references it first in project compile order -
        // that fallback must stay order-independent from the declaring file's perspective, not the other
        // way around, or the same union could land in a different home on every regeneration.
        let unionOwners =
            let candidates =
                fileSet
                |> List.collect (fun source ->
                    source.Contracts
                    |> List.collect (fun contract ->
                        contract.Fields
                        |> List.collect (fun field -> externalUnionsIn field.FieldType)
                        |> List.map (fun union -> source, union)))

            candidates
            |> List.fold
                (fun owners (source, union) ->
                    match union with
                    | ExternalUnion(typeName, _, _, _) when not (Map.containsKey typeName owners) ->
                        let owner = declaringFileOf typeName |> Option.defaultValue source
                        Map.add typeName (owner, union) owners
                    | _ -> owners)
                Map.empty

        let unionSchemaName typeName =
            let source, _ = Map.find typeName unionOwners
            let moduleName = localName typeName

            if source.FilePath = file.FilePath then
                moduleName + ".schema"
            else
                match source.Module with
                | Some sourceModule -> sourceModule + "Schemas." + moduleName + ".schema"
                | None -> typeName + ".schema"

        let unionSchemaScope typeName =
            let source, _ = Map.find typeName unionOwners

            match source.Module with
            | Some sourceModule when source.FilePath <> file.FilePath -> Some(sourceModule + "Schemas")
            | _ -> None

        let namespaceName = file.Namespace |> Option.defaultValue namespaceName

        let qualifyOwnedType (typeName: string) =
            if typeName.StartsWith "global." then
                typeName
            elif typeName.Contains "." then
                "global." + typeName
            else
                match file.Module with
                | Some sourceModule -> $"global.{sourceModule}.{typeName}"
                | None -> $"global.{namespaceName}.{typeName}"

        let ownedTypeAlias typeName =
            if Option.isSome file.Module then localName typeName else "Model"

        let ownedTypeTarget typeName =
            if Option.isSome file.Module then qualifyOwnedType typeName else typeName

        let builder = StringBuilder()
        let line (text: string) =
            text
            |> wrapDescriptionLine
            |> List.iter (fun rendered -> builder.AppendLine rendered |> ignore)
        let fileName = IO.Path.GetFileName file.FilePath

        let rec hasSelfReference contract fieldType =
            match fieldType with
            | Reference reference -> reference.RefName = contract.QualifiedName && reference.RefVersion = contract.Version
            | ListOf element
            | MapOf element -> hasSelfReference contract element
            | MapOfTransparentKey(_, _, element) -> hasSelfReference contract element
            | UnionBlock(_, cases) ->
                cases |> List.exists (fun case -> case.CaseRef.RefName = contract.QualifiedName && case.CaseRef.RefVersion = contract.Version)
            | ExternalUnion(_, _, cases, _) ->
                cases
                |> List.exists (fun case ->
                    match case.ExtPayload with
                    | ExternalRecord reference -> reference.RefName = contract.QualifiedName && reference.RefVersion = contract.Version
                    | ExternalFields fields -> fields |> List.exists (fun field -> hasSelfReference contract field.ExtFieldType)
                    | ExternalEmpty -> false)
            | ExternalTransparent(_, _, payload) -> hasSelfReference contract payload
            | Primitive _
            | LiteralUnion _
            | ExternalEnum _ -> false

        let versionList =
            file.Contracts
            |> List.map (fun contract -> $"{contract.ContractName}.v{contract.Version}")
            |> fun names -> String.Join(", ", names)

        line "// <auto-generated>"
        line $"//   Generated by reified schemagen from {fileName} ({versionList})."
        line "//   Do not edit directly; edit the contract source and regenerate."
        line "// </auto-generated>"

        if file.Contracts |> List.exists (fun contract -> contract.Fields |> List.exists (fun field -> hasSelfReference contract field.FieldType)) then
            line "// Recursive schemas intentionally defer self-reference; suppress the recursive-value initialization warning."
            line "#nowarn \"40\""

        match file.Module with
        | Some moduleName -> line $"module {moduleName}Schemas"
        | None -> line $"namespace {namespaceName}"
        line ""
        line "open Reified"
        line "open Reified.SchemaDSL"

        let ownedEnums =
            enumOwners
            |> Map.toList
            |> List.choose (fun (typeName, (owner, cases)) ->
                if owner.FilePath = file.FilePath && canEmitNamedType typeName owner then Some(typeName, cases) else None)

        let ownedTransparents =
            transparentOwners
            |> Map.toList
            |> List.choose (fun (typeName, (owner, caseName, payload, usedAsKey)) ->
                if owner.FilePath = file.FilePath && canEmitNamedType typeName owner then
                    Some(typeName, caseName, payload, usedAsKey)
                else
                    None)

        for typeName, cases in ownedEnums do
            let moduleName = localName typeName
            let alias = ownedTypeAlias typeName
            line ""
            if file.Module.IsNone then
                line "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
            line "[<RequireQualifiedAccess>]"
            line $"module {moduleName} ="
            line ""
            line $"    type private {alias} = {ownedTypeTarget typeName}"
            line "    let schema ="
            line "        Schema.enum ["
            for case in cases do
                line $"            EnumCase.create \"{escapeString case.EnumTag}\" {alias}.{case.EnumFsCase}"
            line "        ]"

        for typeName, caseName, payload, usedAsKey in ownedTransparents do
            let moduleName = localName typeName
            let alias = ownedTypeAlias typeName
            let caseRef = alias + "." + caseName
            let underlying = baseValueExpr refSchemaName unionSchemaName (fun _ -> None) (fun _ -> None) ("", 0) "value" payload
            line ""
            if file.Module.IsNone then
                line "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
            line "[<RequireQualifiedAccess>]"
            line $"module {moduleName} ="
            line ""
            line $"    type private {alias} = {ownedTypeTarget typeName}"
            line $"    let schema = Schema.convert {caseRef} (function {caseRef} value -> value) {parenthesize underlying}"
            if usedAsKey then
                line $"    let map item = Schema.mapWithKey {caseRef} (function {caseRef} key -> key) item"

        let emittedUnionSchemas = Collections.Generic.HashSet<string>()

        let rec emitUnionSchema typeName =
            let source, fieldType = Map.find typeName unionOwners

            if source.FilePath = file.FilePath && emittedUnionSchemas.Add typeName then
                match fieldType with
                | ExternalUnion(_, representation, cases, fullyQualified) ->
                    // Dependencies must be declared first because F# resolves generated bindings in file order.
                    for case in cases do
                        match case.ExtPayload with
                        | ExternalFields fields ->
                            for field in fields do
                                for nested in externalUnionsIn field.ExtFieldType do
                                    match nested with
                                    | ExternalUnion(nestedTypeName, _, _, _) -> emitUnionSchema nestedTypeName
                                    | _ -> ()
                        | ExternalRecord _
                        | ExternalEmpty -> ()

                    // This module is emitted at file top level. A private alias bearing the union's short
                    // type name keeps generated references local; `open type` exposes a plain union's own case tags unqualified (verified
                    // separately), which is safe from this module's own vocabulary (Reified.SchemaDSL's
                    // exports are all lowercase `let`-bindings; F# case tags must be capitalized, so no
                    // collision there is possible by grammar). Two things are NOT closed-world safe and are
                    // checked below: a case tag colliding with the small set of PascalCase names this
                    // module's own emitted code needs (UnionCase, Schema, UnionRepresentation,
                    // UnionPayloadStyle), or with the bare module name of another same-file record/union this
                    // union's own cases reference unqualified (e.g. `Volume.schema` for a case payload of
                    // record type `Volume` - if a case were ALSO named `Volume`, `open type` would shadow that
                    // reference). A hand-written extension member on the union elsewhere that this tool never
                    // parses remains a residual risk; `[<DeriveUnion(FullyQualified = true)>]` opts out of.
                    let unionTypeName = typeName
                    let unionModuleName = localName typeName
                    let unionTypeAlias = ownedTypeAlias typeName

                    let reservedNames = Set.ofList [ "Model"; "UnionCase"; "Schema"; "UnionRepresentation"; "UnionPayloadStyle" ]

                    let bareReferencedNames =
                        (referencesIn fieldType
                         |> List.choose (fun reference -> if schemaScope reference = None then Some(refSchemaName reference) else None))
                        @ (externalUnionsIn fieldType
                           |> List.choose (function
                                | ExternalUnion(nestedTypeName, _, _, _) when nestedTypeName <> typeName && unionSchemaScope nestedTypeName = None ->
                                    Some(localName nestedTypeName)
                                | _ -> None))
                        @ (namedSchemaTypesIn fieldType
                           |> List.choose (fun namedTypeName ->
                               if namedTypeScope namedTypeName = None then Some(localName namedTypeName) else None))
                        |> Set.ofList

                    let occupiedNames = Set.union reservedNames bareReferencedNames

                    let referencedNameBindings =
                        (referencesIn fieldType
                         |> List.map (fun reference -> refSchemaName reference |> localName, schemaScope reference))
                        @ (externalUnionsIn fieldType
                         |> List.choose (function
                                | ExternalUnion(nestedTypeName, _, _, _) when nestedTypeName <> typeName ->
                                    Some(localName nestedTypeName, unionSchemaScope nestedTypeName)
                                | _ -> None))
                        @ (namedSchemaTypesIn fieldType
                           |> List.map (fun namedTypeName -> localName namedTypeName, namedTypeScope namedTypeName))

                    let locallyOccupied =
                        cases
                        |> List.collect (fun case ->
                            [ case.ExtFsCase; "try" + case.ExtFsCase + "Case"; case.ExtFsCase + "CasePayload" ])
                        |> Set.ofList
                        |> Set.union reservedNames

                    let ambiguousSchemaNames =
                        let scoped =
                            referencedNameBindings
                            |> List.choose (fun (name, scope) -> scope |> Option.map (fun scope -> name, scope))
                            |> List.distinct

                        let acrossScopes =
                            scoped
                            |> List.countBy fst
                            |> List.choose (fun (name, count) -> if count > 1 then Some name else None)

                        let againstLocal =
                            scoped |> List.map fst |> List.filter locallyOccupied.Contains

                        Set.ofList (acrossScopes @ againstLocal)

                    let referencedSchemaScopes =
                        referencedNameBindings
                        |> List.choose (fun (name, scope) -> if Set.contains name ambiguousSchemaNames then None else scope)
                        |> List.distinct

                    let compactRefSchemaName reference =
                        let name = refSchemaName reference |> localName
                        if Set.contains name ambiguousSchemaNames then refSchemaName reference else name

                    let compactUnionSchemaName nestedTypeName =
                        let name = localName nestedTypeName
                        if Set.contains name ambiguousSchemaNames then unionSchemaName nestedTypeName else name + ".schema"

                    let compactNamedSchemaName namedTypeName =
                        namedSchemaName namedTypeName
                        |> Option.map (fun fullName ->
                            let name = localName namedTypeName
                            if Set.contains name ambiguousSchemaNames then fullName else name + ".schema")

                    let compactKeyMapName namedTypeName =
                        keyMapName namedTypeName
                        |> Option.map (fun fullName ->
                            let name = localName namedTypeName
                            if Set.contains name ambiguousSchemaNames then fullName else name + ".map")

                    let useShortCaseAccess =
                        not fullyQualified
                        && cases |> List.forall (fun case -> not (Set.contains case.ExtFsCase occupiedNames))

                    // Keep all generated references local to this schema module. `TypeName.Case` remains
                    // qualification-safe when short access is unavailable, without repeating the raw
                    // union's namespace at every construction and match site.
                    let casePrefix =
                        if useShortCaseAccess then
                            ""
                        else
                            unionTypeAlias + "."

                    line ""
                    line "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
                    line "[<RequireQualifiedAccess>]"
                    line $"module {unionModuleName} ="
                    line ""
                    line $"    type private {unionTypeAlias} = {ownedTypeTarget typeName}"
                    if useShortCaseAccess then
                        line $"    open type {unionTypeAlias}"
                    for scope in referencedSchemaScopes do
                        line $"    open {scope}"

                    if cases |> List.exists (fun case -> match case.ExtPayload with ExternalFields _ -> true | _ -> false) then
                        line ""
                    let mutable emittedExtractor = false
                    for case in cases do
                        match case.ExtPayload with
                        | ExternalFields fields ->
                            if emittedExtractor then line ""
                            emittedExtractor <- true
                            let payloadType = case.ExtFsCase + "CasePayload"
                            let extractor = "try" + case.ExtFsCase + "Case"

                            match fields with
                            | [ field ] ->
                                let name = camel field.ExtFieldName
                                let pattern = $"{casePrefix}{case.ExtFsCase} {name}"
                                line $"    let private {extractor} = function"
                                line $"        | {pattern} -> Some {name}"
                                if List.length cases > 1 then line "        | _ -> None"
                            | _ ->
                                line $"    type private {payloadType} = {{"
                                for field in fields do
                                    let suffix = if field.ExtOptional then " option" else ""
                                    line $"        {field.ExtFieldName}: {fsType refTypeName unionTypeName case.ExtFsCase field.ExtFieldType}{suffix}"
                                line "    }"
                                let names = fields |> List.map (fun field -> camel field.ExtFieldName) |> String.concat ", "
                                let pattern = $"{casePrefix}{case.ExtFsCase}({names})"
                                let record = fields |> List.map (fun field -> $"{field.ExtFieldName} = {camel field.ExtFieldName}") |> String.concat "; "
                                line ""
                                line $"    let private {extractor} = function"
                                line $"        | {pattern} -> Some {{ {record} }}"
                                if List.length cases > 1 then line "        | _ -> None"
                        | ExternalRecord _
                        | ExternalEmpty -> ()

                    let representationExpression =
                        match representation with
                        | GeneratedInternal discriminator -> $"UnionRepresentation.Internal \"{escapeString discriminator}\""
                        | GeneratedAdjacent(discriminator, payload, style) -> $"UnionRepresentation.Adjacent(\"{escapeString discriminator}\", \"{escapeString payload}\", UnionPayloadStyle.{style})"
                        | GeneratedExternal(style, unwrapFieldless) ->
                            let unwrap = if unwrapFieldless then "true" else "false"
                            $"UnionRepresentation.External(UnionPayloadStyle.{style}, {unwrap})"

                    line ""
                    line "    let schema ="
                    match representation with
                    | GeneratedInternal "type" -> line "        Schema.union ["
                    | _ -> line $"        Schema.unionWith ({representationExpression}) ["
                    cases
                    |> List.iteri (fun index case ->
                        match case.ExtPayload with
                        | ExternalEmpty ->
                            let predicate =
                                if List.length cases = 1 then $"(function {casePrefix}{case.ExtFsCase} -> true)"
                                else $"(function {casePrefix}{case.ExtFsCase} -> true | _ -> false)"
                            line $"            UnionCase.empty \"{escapeString case.ExtTag}\" {casePrefix}{case.ExtFsCase} {predicate}"
                        | ExternalRecord reference ->
                            let extractor =
                                if List.length cases = 1 then $"(function {casePrefix}{case.ExtFsCase} payload -> Some payload)"
                                else $"(function {casePrefix}{case.ExtFsCase} payload -> Some payload | _ -> None)"
                            line $"            UnionCase.fields \"{escapeString case.ExtTag}\" {casePrefix}{case.ExtFsCase} {extractor} {compactRefSchemaName reference}.schema"
                        | ExternalFields fields ->
                            let extractor = "try" + case.ExtFsCase + "Case"
                            line $"            case \"{escapeString case.ExtTag}\" {{"
                            line $"                tryExtract {extractor}"
                            for field in fields do
                                let getter = if List.length fields = 1 then "id" else "_." + field.ExtFieldName
                                if not field.ExtOptional && hasCanonicalSchema field.ExtFieldType then
                                    line $"                fieldAs \"{escapeString field.ExtWireName}\" {getter}"
                                else
                                    let expression = baseValueExpr compactRefSchemaName compactUnionSchemaName compactNamedSchemaName compactKeyMapName ("", 0) field.ExtFieldName field.ExtFieldType
                                    let expression = if field.ExtOptional then $"Schema.option {parenthesize expression}" else expression
                                    line $"                fieldAs \"{escapeString field.ExtWireName}\" {getter} {{"
                                    line $"                    withSchema {parenthesize expression}"
                                    line "                }"
                            let parameters = fields |> List.map (fun field -> camel field.ExtFieldName) |> String.concat " "
                            let arguments = fields |> List.map (fun field -> camel field.ExtFieldName) |> String.concat ", "
                            match fields with
                            | [ _ ] -> line $"                construct {casePrefix}{case.ExtFsCase}"
                            | _ -> line $"                construct (fun {parameters} -> {casePrefix}{case.ExtFsCase}({arguments}))"
                            line "            }")
                    line "        ]"
                | _ -> ()

        let contractTypeNameOf (contract: ContractDecl) =
            if file.Module.IsSome then
                typeNameOf contract.QualifiedName contract.Version
            else
                contract.ExternalTypeName
                |> Option.map localName
                |> Option.defaultValue (typeNameOf contract.QualifiedName contract.Version)

        let contractModuleNameOf (contract: ContractDecl) =
            contract.ExternalTypeName
            |> Option.map localName
            |> Option.defaultValue (contractTypeNameOf contract)

        // Names this file already binds at top level, independent of any `open`: every union module this
        // file owns (emitUnionSchema emits them here), and every contract's own generated module. A cross-file
        // scope that happens to export one of these same short names would silently shadow it (or be shadowed
        // by it) once opened, so these names are folded into the ambiguity check below alongside cross-scope
        // collisions.
        let locallyOccupiedNames =
            let unionModuleNames =
                unionOwners
                |> Map.toList
                |> List.choose (fun (typeName, (source, _)) ->
                    if source.FilePath = file.FilePath then Some(localName typeName) else None)

            let namedTypeModuleNames =
                [ yield! ownedEnums |> List.map (fst >> localName)
                  yield! ownedTransparents |> List.map (fun (typeName, _, _, _) -> localName typeName) ]

            Set.ofList (unionModuleNames @ namedTypeModuleNames @ (file.Contracts |> List.map contractModuleNameOf))

        for contract in file.Contracts do
            for field in contract.Fields do
                for union in externalUnionsIn field.FieldType do
                    match union with
                    | ExternalUnion(typeName, _, _, _) -> emitUnionSchema typeName
                    | _ -> ()

            let contractTypeName = contractTypeNameOf contract
            let contractModuleName = contractModuleNameOf contract
            let contractRef = $"{fileName}, {contract.ContractName}.v{contract.Version}"
            let schemaTypeName = localName contractTypeName

            // Every cross-file name this contract would otherwise open unqualified, paired with the scope
            // it comes from. Two different scopes contending for the same short name would silently shadow
            // one another once opened (F# does not error on ambiguous opens), so any such name is detected
            // here and forced back to full qualification instead of being risked.
            let recordNameBindings =
                contract.Fields
                |> List.collect (fun field -> referencesIn field.FieldType)
                |> List.map (fun reference -> refSchemaName reference |> localName, schemaScope reference)

            let unionNameBindings =
                contract.Fields
                |> List.collect (fun field -> externalUnionsIn field.FieldType)
                |> List.choose (function
                    | ExternalUnion(typeName, _, _, _) -> Some(localName typeName, unionSchemaScope typeName)
                    | _ -> None)

            let namedTypeBindings =
                contract.Fields
                |> List.collect (fun field -> namedSchemaTypesIn field.FieldType)
                |> List.distinct
                |> List.map (fun typeName -> localName typeName, namedTypeScope typeName)

            let allNameBindings = recordNameBindings @ unionNameBindings @ namedTypeBindings

            let ambiguousNames =
                let scopedOccurrences =
                    allNameBindings
                    |> List.choose (fun (name, scope) -> scope |> Option.map (fun scope -> name, scope))
                    |> List.distinct

                let ambiguousAcrossScopes =
                    scopedOccurrences
                    |> List.countBy fst
                    |> List.choose (fun (name, count) -> if count > 1 then Some name else None)

                let ambiguousAgainstLocal =
                    scopedOccurrences
                    |> List.map fst
                    |> List.filter locallyOccupiedNames.Contains

                Set.ofList (ambiguousAcrossScopes @ ambiguousAgainstLocal)

            let referencedSchemaScopes =
                allNameBindings
                |> List.choose (fun (name, scope) -> if Set.contains name ambiguousNames then None else scope)
                |> List.distinct

            let compactRefSchemaName reference =
                let name = refSchemaName reference |> localName
                if Set.contains name ambiguousNames then refSchemaName reference else name

            let compactUnionSchemaName typeName =
                let name = localName typeName
                if Set.contains name ambiguousNames then unionSchemaName typeName else name + ".schema"

            let compactNamedSchemaName typeName =
                namedSchemaName typeName
                |> Option.map (fun fullName ->
                    let name = localName typeName
                    if Set.contains name ambiguousNames then fullName else name + ".schema")

            let compactKeyMapName typeName =
                keyMapName typeName
                |> Option.map (fun fullName ->
                    let name = localName typeName
                    if Set.contains name ambiguousNames then fullName else name + ".map")

            // User-owned record fields are referenced verbatim; generated records normalize to PascalCase.
            let fsFieldName (field: FieldDecl) =
                if contract.OwnsType then pascal field.FieldName else field.FieldName

            let caseFields =
                contract.Fields
                |> List.filter (fun field ->
                    match field.FieldType with
                    | LiteralUnion _
                    | UnionBlock _ -> true
                    | ExternalEnum(typeName, _) -> namedSchemaName typeName |> Option.isNone
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
            line $"/// Schema for {schemaTypeName}."
            line "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
            line "[<RequireQualifiedAccess>]"
            line $"module {contractModuleName} ="
            line ""
            if contract.Fields |> List.exists (fieldLevelConstraints >> List.isEmpty >> not) then
                line "    open Reified.ConstraintDSL"
            let schemaTypeAlias = ownedTypeAlias contractTypeName
            line $"    type private {schemaTypeAlias} = {ownedTypeTarget contractTypeName}"
            line $"    open type {schemaTypeAlias}"
            for scope in referencedSchemaScopes do
                line $"    open {scope}"

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
                            $"        {opener}UnionCase.fields \"{escapeString case.CaseTag}\" {du}.{duCaseName case.CaseTag} {extractor} {compactRefSchemaName case.CaseRef}.schema{closer}")
                | ExternalEnum(typeName, cases) ->
                    // Always fully qualified: F# does not allow a local `open` inside a plain let binding
                    // (only at module scope or inside a function/module body that already sequences
                    // declarations), so there is no safe place to open the enum's raw namespace just for
                    // this binding. Qualifying it here instead is always valid and carries no shadowing risk.
                    let enumTypeName = typeName

                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""
                        line $"        {opener}EnumCase.create \"{escapeString case.EnumTag}\" {enumTypeName}.{case.EnumFsCase}{closer}")
                | _ -> ()

            let recursion = if contract.Fields |> List.exists (fun field -> hasSelfReference contract field.FieldType) then " rec" else ""

            line ""
            line $"    let{recursion} schema ="

            let parameters =
                contract.Fields
                |> List.map (fun field -> escapeIdent (camel field.FieldName))
                |> fun names -> String.Join(" ", names)

            let schemaBuilder =
                if recursion = " rec" then
                    "SchemaDSL.schema"
                else
                    "schema"

            line $"        {schemaBuilder}<{schemaTypeAlias}> {{"

            for field in contract.Fields do
                let wire = FieldDecl.wireName field
                let getter = $"_.{escapeIdent (fsFieldName field)}"
                let constraints = fieldLevelConstraints field
                let supplies = declaresSupply field

                if canInferField field && List.isEmpty constraints && not supplies then
                    line $"            fieldAs \"{escapeString wire}\" {getter}"
                else
                    line $"            fieldAs \"{escapeString wire}\" {getter} {{"

                    if requiresExplicitSchema field then
                        let value = valueExpr compactRefSchemaName compactUnionSchemaName compactNamedSchemaName compactKeyMapName (contract.QualifiedName, contract.Version, schemaTypeName) field
                        line $"                withSchema {parenthesize value}"
                    else
                        match field.Doc with
                        | [] -> ()
                        | doc -> line $"                describe \"{escapeString (joinedDoc doc)}\""

                        match field.Format with
                        | Some format -> line $"                format (SchemaFormat.create \"{escapeString format}\")"
                        | None -> ()

                        match field.Default with
                        | Some literal ->
                            let renderedDefault = renderedFieldDefault schemaTypeName field literal
                            line $"                defaultValue {renderedDefault}"
                        | None -> ()

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
                    line $"                {opener}{escapeIdent (fsFieldName field)} = {escapeIdent (camel field.FieldName)}{closer}")

            line "        }"

            match contract.Doc with
            | [] -> ()
            | doc -> line $"        |> Schema.describe \"{escapeString (joinedDoc doc)}\""

            line ""
            line "    let validate = Schema.check schema"
            line "    let parse = Schema.parse schema"

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

        // A union can be declared beside a derived record without that record mentioning it. Emit those
        // remaining owner-file unions after the contracts: a union may depend on a local record schema,
        // while unions that a local contract needs were already emitted immediately before that contract.
        for KeyValue(typeName, (source, _)) in unionOwners do
            if source.FilePath = file.FilePath then
                emitUnionSchema typeName

        builder.ToString().Replace("\r\n", "\n")
