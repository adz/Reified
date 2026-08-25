namespace Reified.Tests.Generated

open Reified.DerivedSchema

type WorkflowId = WorkflowId of string

[<DeriveUnion>]
type RecommendedCommand =
    | Stop
    | Move of x: int * y: int

[<DeriveUnion(
    "Case",
    Representation = UnionRepresentationKind.Adjacent,
    PayloadField = "Fields",
    PayloadStyle = UnionPayloadStyleKind.Positional
)>]
type FSharpSystemTextJsonCommand =
    | [<SchemaName "Pause">] Pause
    | [<SchemaName "Scale">] Scale of amount: decimal
    | [<SchemaName "Translate">] Translate of x: int * y: int

[<DeriveUnion(
    Representation = UnionRepresentationKind.External,
    PayloadStyle = UnionPayloadStyleKind.NamedWithUnwrappedSingle,
    UnwrapFieldless = true
)>]
type CompactExternalCommand =
    | Cancel
    | Rename of name: string
    | Resize of width: int * height: int

[<DeriveSchema>]
type UnionCompatibilityEnvelope =
    { WorkflowId: WorkflowId
      Recommended: RecommendedCommand
      FSharpSystemTextJson: FSharpSystemTextJsonCommand
      CompactExternal: CompactExternalCommand }
