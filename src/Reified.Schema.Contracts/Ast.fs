namespace Reified.Schema.Contracts

/// <summary>A literal value in a contract declaration. Literals are the only values the grammar can express;
/// everything with semantics is a name resolved against F# code at generation time.</summary>
type Literal =
    | LString of string
    | LInt of int
    | LDecimal of decimal
    | LBool of bool

/// <summary>The primitive type heads the grammar knows. <c>Email</c> is a format type: it lowers to text plus the
/// <c>email</c> constraint.</summary>
type PrimitiveType =
    | PText
    | PInt
    | PDecimal
    | PBool
    | PDate
    | PDateTime
    | PGuid
    | PEmail

/// <summary>A reference to another contract at a pinned version, e.g. <c>Geo.v1</c>.</summary>
type ContractRef =
    { RefName: string
      RefVersion: int }

/// <summary>One case of an inline tagged union block.</summary>
type UnionCaseDecl =
    { CaseTag: string
      CaseRef: ContractRef
      CaseLine: int }

/// <summary>One case of a user-owned nullary discriminated union used as an enum field.</summary>
type ExternalEnumCase =
    { EnumTag: string
      EnumFsCase: string }

/// <summary>One named field carried directly by a user-owned union case.</summary>
type ExternalUnionField =
    { ExtFieldName: string
      ExtWireName: string
      ExtFieldType: FieldType
      ExtOptional: bool }

/// <summary>The payload shape of one user-owned internally tagged union case.</summary>
and ExternalUnionPayload =
    | ExternalEmpty
    | ExternalRecord of ContractRef
    | ExternalFields of ExternalUnionField list

/// <summary>One case of a user-owned internally tagged union.</summary>
and ExternalUnionCase =
    { ExtTag: string
      ExtFsCase: string
      ExtPayload: ExternalUnionPayload
      ExtLine: int }

and ExternalUnionRepresentation =
    | GeneratedInternal of discriminator: string
    | GeneratedAdjacent of discriminator: string * payloadField: string * payloadStyle: string
    | GeneratedExternal of payloadStyle: string * unwrapFieldless: bool

/// <summary>A field's declared type. The <c>External*</c> shapes are produced only by the record frontend:
/// they reference user-owned F# union types instead of generating case types.</summary>
and FieldType =
    | Primitive of PrimitiveType
    | Reference of ContractRef
    | ListOf of FieldType
    | MapOf of FieldType
    | LiteralUnion of string list
    | UnionBlock of discriminator: string * cases: UnionCaseDecl list
    | ExternalEnum of typeName: string * cases: ExternalEnumCase list
    | ExternalTransparent of typeName: string * caseName: string * payload: FieldType
    | ExternalUnion of typeName: string * representation: ExternalUnionRepresentation * cases: ExternalUnionCase list

/// <summary>One portable constraint retained by the shared generation pipeline.</summary>
type ConstraintDecl =
    | AtLeast of Literal
    | GreaterThan of Literal
    | AtMost of Literal
    | LessThan of Literal
    | MinSize of int
    | MaxSize of int
    | ExactLength of int
    | LengthRange of minimum: int * maximum: int
    | Present
    | Supplied
    | Pattern of string
    | MultipleOf of Literal
    | Distinct
    | CheckRef of string

/// <summary>An <c>@name literal?</c> annotation attached to a contract or field.</summary>
type Annotation =
    { AnnotationName: string
      AnnotationValue: Literal option
      AnnotationLine: int }

/// <summary>One declared contract field.</summary>
type FieldDecl =
    { FieldName: string
      WireName: string option
      Optional: bool
      FieldType: FieldType
      Constraints: (ConstraintDecl * int) list
      /// Open format metadata supplied by the record-attribute frontend.
      Format: string option
      Default: Literal option
      Doc: string list
      Annotations: Annotation list
      FieldLine: int }

/// <summary>One declared contract at one version. <c>OwnsType</c> is true when generation emits the record
/// and its case types (the .contract path); the record frontend sets it false so emission targets the
/// user-owned type. <c>ExternalTypeName</c> carries the user type's actual name when a chain override means
/// it differs from the conventional generated name.</summary>
type ContractDecl =
    { ContractName: string
      /// Stable project-wide identity. Record-derived contracts use the fully qualified F# type path;
      /// textual contracts use <c>ContractName</c>.
      QualifiedName: string
      Version: int
      Doc: string list
      Annotations: Annotation list
      Fields: FieldDecl list
      OwnsType: bool
      ExternalTypeName: string option
      /// A user function the schema calls to assemble the record instead of a record literal
      /// ([<SchemaConstructor>]; record frontend only, .contract files leave it None).
      Constructor: string option
      ContractLine: int }

/// <summary>A parsed contract source file. <c>Namespace</c> is set by the record frontend from the source
/// file's own namespace declaration; .contract files leave it None and take the CLI namespace.</summary>
type ContractFile =
    { FilePath: string
      Namespace: string option
      /// The source file's direct file-level module, when it has one.
      Module: string option
      Contracts: ContractDecl list }

/// <summary>A line-precise parse or resolution problem.</summary>
type ContractDiagnostic =
    { File: string
      Line: int
      Message: string }

    override this.ToString() = $"{this.File}({this.Line}): error: {this.Message}"

module FieldDecl =
    /// The external (wire) name: the explicit `as "..."` rename when present, the field name otherwise.
    let wireName (field: FieldDecl) =
        field.WireName |> Option.defaultValue field.FieldName
