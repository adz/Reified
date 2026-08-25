// Schema<'model> itself: a sealed wrapper over a completed definition (model or value) plus the
// optional compiled record plan, with the public UnionCase/EnumCase companions. A schema is a
// description — behavior lives in interpreters (parsing, checking, codecs, JSON Schema, docs).
namespace Reified

open Reified

open System
open System.Collections.Generic

/// <summary>
/// Describes a typed value's portable structure and construction for schema interpreters.
/// </summary>
/// <remarks>
/// <para>
/// A schema records shape and construction metadata without tying that metadata to input parsing,
/// diagnostics, validation, codecs, UI generation, or workflow execution.
/// </para>
/// <para>
/// Primitive, collection, optional, union, refined, and record declarations all produce <c>Schema&lt;'value&gt;</c>.
/// Record declarations use the <c>schema&lt;'value&gt; { }</c> computation expression. Each <c>field</c> may contain
/// <c>withSchema</c>, <c>constrain</c>, <c>constraints</c>, <c>refine</c>, and <c>validate</c> operations before the declaration finishes
/// with <c>construct</c> or <c>constructResult</c>.
/// </para>
/// </remarks>
// No class-level `as this` self-identifier here: combined with the secondary constructor it makes
// F# emit safe-initialization IL that Fable cannot compile. The one member that needs the instance
// uses a member-level self-identifier instead.
[<Sealed>]
type Schema<'model> internal (definition: SchemaDefinition<'model>, recordPlan: ICompiledRecordPlan<'model> option) =
    internal new(definition: SchemaDefinition<'model>) = Schema(definition, None)

    member internal _.Definition = definition
    member internal _.RecordPlan = recordPlan

    member internal this.ValueDefinition =
        match definition with
        | ValueDefinition value -> value
        | ModelDefinition model ->
            { Shape = NestedValueDefinition(ModelSchemaErasure.erase model, box this)
              Format = None
              Rules = []
              Description = None
              Default = None }
        | PendingDefinition -> invalidOp "Expected a completed schema definition."

/// <summary>Functions for defining explicit tagged union schema cases.</summary>
[<RequireQualifiedAccess>]
module UnionCase =
    /// <summary>
    /// Describes one tagged union case from a tag, a payload constructor, a payload extractor, and a payload schema.
    /// </summary>
    /// <remarks>
    /// Union schemas are explicit and reflection-free. The constructor builds the union case after the payload parses,
    /// while the extractor lets validation and encoding-oriented interpreters identify the active case of an existing
    /// trusted union value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="tag" /> is empty or whitespace.</exception>
    /// <exception cref="T:System.ArgumentNullException">
    /// Thrown when <paramref name="tag" />, <paramref name="construct" />, <paramref name="tryPayload" />, or
    /// <paramref name="payload" /> is null.
    /// </exception>
    let private createValueCase tag (construct: 'payload -> 'union) (tryPayload: 'union -> 'payload option) (payload: Schema<'payload>) : UnionCase<'union> =
        if isNull tag then
            nullArg (nameof tag)

        if isNull (box construct) then
            nullArg (nameof construct)

        if isNull (box tryPayload) then
            nullArg (nameof tryPayload)

        if isNull (box payload) then
            nullArg (nameof payload)

        UnionCase(
            { Tag = ExternalFieldName.create tag |> ExternalFieldName.value
              Payload = ValueUnionCase payload.ValueDefinition
              Construct = fun value -> value |> unbox<'payload> |> construct |> box
              TryInspect = fun value -> value |> unbox<'union> |> tryPayload |> Option.map box }
        )

    /// <summary>Describes one payload-less case in a tagged union.</summary>
    /// <remarks>The case remains part of the tagged object when it belongs to a mixed union; it is not lowered to a string.</remarks>
    let empty tag (value: 'union) (isCase: 'union -> bool) : UnionCase<'union> =
        if isNull tag then
            nullArg (nameof tag)

        if isNull (box isCase) then
            nullArg (nameof isCase)

        UnionCase(
            { Tag = ExternalFieldName.create tag |> ExternalFieldName.value
              Payload = EmptyUnionCase
              Construct = fun _ -> box value
              TryInspect = fun candidate -> if candidate |> unbox<'union> |> isCase then Some(box ()) else None }
        )

    /// <summary>Describes a case with one arbitrary value payload.</summary>
    let value tag construct tryPayload payload = createValueCase tag construct tryPayload payload

    /// <summary>Describes a case whose payload schema is a record model and therefore has authored fields.</summary>
    let fields tag (construct: 'payload -> 'union) (tryPayload: 'union -> 'payload option) (payload: Schema<'payload>) : UnionCase<'union> =
        let case = createValueCase tag construct tryPayload payload
        UnionCase({ case.Definition with Payload = FieldsUnionCase payload.ValueDefinition })

/// <summary>Functions for defining explicit payload-less enum schema cases.</summary>
[<RequireQualifiedAccess>]
module EnumCase =
    /// <summary>Describes one payload-less enum case from a tag and the union case value it represents.</summary>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="tag" /> is empty or whitespace.</exception>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="tag" /> is null.</exception>
    let create tag (value: 'enum) : EnumCase<'enum> =
        if isNull tag then
            nullArg (nameof tag)

        EnumCase({ Tag = ExternalFieldName.create tag |> ExternalFieldName.value; Value = box value })
