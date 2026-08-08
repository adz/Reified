// The record-schema computation expression. The outer builder separates fields and retains a typed
// constructor chain; the optional inner field builder transforms one Schema<_> value.
namespace Reified.Schema

open Reified

#nowarn "64"

open System.ComponentModel
open Reified.Refinements

open Microsoft.FSharp.Quotations

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldInitial<'model, 'target> internal (name: string, getter: 'model -> 'target) =
    member internal _.Name = name
    member internal _.Getter = getter

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldWorking<'model, 'target, 'current> internal
    (
        initial: FieldInitial<'model, 'target>,
        schema: Schema<'current>
    ) =
    member internal _.Initial = initial
    member internal _.Schema = schema

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldConfigured<'model, 'target> internal
    (
        initial: FieldInitial<'model, 'target>,
        configure: Schema<'target> -> Schema<'target>
    ) =
    member _.Initial = initial
    member _.Configure = configure

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldRefining<'model, 'target, 'raw> internal
    (
        initial: FieldInitial<'model, 'target>,
        rawSchema: Schema<'raw>,
        validations: ('target -> Result<unit, SchemaError>) list
    ) =
    member internal _.Initial = initial
    member internal _.RawSchema = rawSchema
    member internal _.Validations = validations

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldDeclaration<'model, 'value> internal (definition: FieldDefinition<'model, 'value>) =
    member internal _.Definition = definition

[<EditorBrowsable(EditorBrowsableState.Never)>]
type ConfiguredFieldDeclaration<'model, 'target> internal
    (
        initial: FieldInitial<'model, 'target>,
        configure: Schema<'target> -> Schema<'target>
    ) =
    member _.Initial = initial
    member _.Configure = configure

[<EditorBrowsable(EditorBrowsableState.Never)>]
type RefiningFieldDeclaration<'model, 'raw, 'target> internal
    (
        initial: FieldInitial<'model, 'target>,
        rawSchema: Schema<'raw>,
        validations: ('target -> Result<unit, SchemaError>) list
    ) =
    member internal _.Initial = initial
    member internal _.RawSchema = rawSchema
    member internal _.Validations = validations

/// Compile-time resolution of one canonical refinement for a known underlying and destination type.
/// <exclude />
[<EditorBrowsable(EditorBrowsableState.Never)>]
type RefinementDefaults =
    static member inline Resolve() : Refinement<^underlying, ^refined> =
        let inline call (witness: ^w, underlying: ^underlying, refined: ^refined) =
            ((^w or ^refined):
                (static member Refinement:
                    ^underlying * ^refined -> Refinement<^underlying, ^refined>)
                    (underlying, refined))

        call (
            Unchecked.defaultof<RefinementDefaults>,
            Unchecked.defaultof<^underlying>,
            Unchecked.defaultof<^refined>
        )

/// <summary>
/// Declares one field inside <c>schema&lt;'model&gt; { }</c>, and configures it when followed by a block.
/// </summary>
/// <remarks>
/// <para>
/// <c>field _.Email</c> derives the wire name from the property, camelCased. Use <c>fieldAs</c> when the wire
/// name differs from the property.
/// </para>
/// <para>
/// Deriving the name reads a quotation of the getter, once, while the schema value is built. That works on .NET
/// and on the Fable targets with quotation support — JavaScript, TypeScript, Python, and BEAM need Fable 5.10 or
/// later, Dart needs 5.13. Fable's Rust and PHP targets have no quotation support, so those use <c>fieldAs</c>.
/// </para>
/// <para>
/// This is a type rather than a function in <c>Syntax</c> so that <c>field</c> stays unqualified under an
/// ordinary <c>open</c>. See <c>dev-docs/derived-field-names.md</c>.
/// </para>
/// </remarks>
type field<'model, 'target> internal (name: string, getter: 'model -> 'target) =
    member internal _.Name = name
    member internal _.Getter = getter

    /// <summary>Declares a field, deriving its camel-cased wire name from the property getter.</summary>
    /// <example><code>field _.Email  // wire name "email"</code></example>
#if FABLE_COMPILER
    // Fable does not implement Expr.WithValue, so it takes the plain attribute and recompiles the getter.
    new([<ReflectedDefinition>] getter: Expr<'model -> 'target>) =
        let name, get = GetterName.split getter
        field<'model, 'target>(name, get)
#else
    new([<ReflectedDefinition(includeValue = true)>] getter: Expr<'model -> 'target>) =
        let name, get = GetterName.split getter
        field<'model, 'target>(name, get)
#endif

    member _.Yield(()) : FieldInitial<'model, 'target> =
        FieldInitial(name, getter)

    static member private ConstrainAll
        (
            constraints: Constraint<'value> list,
            schema: Schema<'value>
        ) : Schema<'value> =
        if isNull (box constraints) then nullArg (nameof constraints)

        constraints
        |> List.iter (fun constraint' ->
            if isNull (box constraint') then
                nullArg (nameof constraints))

        schema
        |> SchemaCore.constrainAll constraints

    /// <summary>Supplies the schema transformed by the remaining operations in this field block.</summary>
    [<CustomOperation("withSchema")>]
    member _.WithSchema
        (
            initial: FieldInitial<'model, 'target>,
            schema: Schema<'current>
        ) : FieldWorking<'model, 'target, 'current> =
        if isNull (box schema) then nullArg (nameof schema)
        FieldWorking(initial, schema)

    /// <summary>Supplies the field value when the input omits it.</summary>
    [<CustomOperation("defaultValue")>]
    member _.DefaultValue
        (
            initial: FieldInitial<'model, 'target>,
            value: 'target
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(initial, SchemaCore.withDefault value)

    /// <summary>Supplies the field value when the input omits it.</summary>
    [<CustomOperation("defaultValue")>]
    member _.DefaultValue
        (
            source: FieldConfigured<'model, 'target>,
            value: 'target
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(source.Initial, source.Configure >> SchemaCore.withDefault value)

    /// <summary>Supplies the field value when the input omits it.</summary>
    [<CustomOperation("defaultValue")>]
    member _.DefaultValue
        (
            source: FieldWorking<'model, 'target, 'current>,
            value: 'current
        ) : FieldWorking<'model, 'target, 'current> =
        FieldWorking(source.Initial, source.Schema |> SchemaCore.withDefault value)

    /// <summary>Adds human-readable description metadata to the field's schema.</summary>
    [<CustomOperation("describe")>]
    member _.Describe
        (
            initial: FieldInitial<'model, 'target>,
            text: string
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(initial, SchemaCore.describe text)

    /// <summary>Adds human-readable description metadata to the field's schema.</summary>
    [<CustomOperation("describe")>]
    member _.Describe
        (
            source: FieldConfigured<'model, 'target>,
            text: string
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(source.Initial, source.Configure >> SchemaCore.describe text)

    /// <summary>Adds human-readable description metadata to the field's current schema.</summary>
    [<CustomOperation("describe")>]
    member _.Describe
        (
            source: FieldWorking<'model, 'target, 'current>,
            text: string
        ) : FieldWorking<'model, 'target, 'current> =
        FieldWorking(source.Initial, source.Schema |> SchemaCore.describe text)

    /// <summary>Adds format metadata to the field's schema.</summary>
    [<CustomOperation("format")>]
    member _.Format
        (
            initial: FieldInitial<'model, 'target>,
            format: SchemaFormat
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(initial, SchemaCore.withFormat format)

    /// <summary>Adds format metadata to the field's schema.</summary>
    [<CustomOperation("format")>]
    member _.Format
        (
            source: FieldConfigured<'model, 'target>,
            format: SchemaFormat
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(source.Initial, source.Configure >> SchemaCore.withFormat format)

    /// <summary>Adds format metadata to the field's current schema.</summary>
    [<CustomOperation("format")>]
    member _.Format
        (
            source: FieldWorking<'model, 'target, 'current>,
            format: SchemaFormat
        ) : FieldWorking<'model, 'target, 'current> =
        FieldWorking(source.Initial, source.Schema |> SchemaCore.withFormat format)

    /// <summary>Adds a portable constraint to the field's current schema value.</summary>
    [<CustomOperation("constrain")>]
    member _.Constrain
        (
            initial: FieldInitial<'model, 'target>,
            constraint': Constraint<'target>
        ) : FieldConfigured<'model, 'target> =
        if isNull (box constraint') then nullArg (nameof constraint')
        FieldConfigured(initial, SchemaCore.constrain constraint')

    /// <summary>Adds another portable constraint to an inferred field schema.</summary>
    [<CustomOperation("constrain")>]
    member _.Constrain
        (
            source: FieldConfigured<'model, 'target>,
            constraint': Constraint<'target>
        ) : FieldConfigured<'model, 'target> =
        if isNull (box constraint') then nullArg (nameof constraint')
        FieldConfigured(
            source.Initial,
            source.Configure >> SchemaCore.constrain constraint'
        )

    /// <summary>Adds a portable constraint to the field's current schema value.</summary>
    [<CustomOperation("constrain")>]
    member _.Constrain
        (
            source: FieldWorking<'model, 'target, 'current>,
            constraint': Constraint<'current>
        ) : FieldWorking<'model, 'target, 'current> =
        if isNull (box constraint') then nullArg (nameof constraint')
        FieldWorking(source.Initial, source.Schema |> SchemaCore.constrain constraint')

    /// <summary>Requires this field's boundary input to be supplied.</summary>
    /// <example><code>field _.Name { mustSupply }</code></example>
    [<CustomOperation("mustSupply")>]
    member _.MustSupply(initial: FieldInitial<'model, 'target>) : FieldConfigured<'model, 'target> =
        FieldConfigured(initial, ValueSchema.mustSupply)

    /// <summary>Requires this field's boundary input to be supplied.</summary>
    [<CustomOperation("mustSupply")>]
    member _.MustSupply(source: FieldConfigured<'model, 'target>) : FieldConfigured<'model, 'target> =
        FieldConfigured(source.Initial, source.Configure >> ValueSchema.mustSupply)

    /// <summary>Requires this field's boundary input to be supplied.</summary>
    [<CustomOperation("mustSupply")>]
    member _.MustSupply(source: FieldWorking<'model, 'target, 'current>) : FieldWorking<'model, 'target, 'current> =
        FieldWorking(source.Initial, source.Schema |> ValueSchema.mustSupply)

    /// <summary>Allows this option-typed field's boundary input to be omitted.</summary>
    /// <example><code>field _.Nickname { mayOmit }</code></example>
    [<CustomOperation("mayOmit")>]
    member _.MayOmit(source: FieldWorking<'model, 'target, 'current option>) : FieldWorking<'model, 'target, 'current option> =
        FieldWorking(source.Initial, source.Schema |> ValueSchema.mayOmit)

    /// <summary>Adds portable constraints to the field's inferred schema in declaration order.</summary>
    [<CustomOperation("constraints")>]
    member _.Constraints
        (
            initial: FieldInitial<'model, 'target>,
            constraints: Constraint<'target> list
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(initial, fun schema -> field<'model, 'target>.ConstrainAll(constraints, schema))

    /// <summary>Adds portable constraints to the field's inferred schema in declaration order.</summary>
    [<CustomOperation("constraints")>]
    member _.Constraints
        (
            source: FieldConfigured<'model, 'target>,
            constraints: Constraint<'target> list
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(
            source.Initial,
            source.Configure
            >> fun schema -> field<'model, 'target>.ConstrainAll(constraints, schema)
        )

    /// <summary>Adds portable constraints to the field's current schema value in declaration order.</summary>
    [<CustomOperation("constraints")>]
    member _.Constraints
        (
            source: FieldWorking<'model, 'target, 'current>,
            constraints: Constraint<'current> list
        ) : FieldWorking<'model, 'target, 'current> =
        FieldWorking(
            source.Initial,
            field<'model, 'target>.ConstrainAll(constraints, source.Schema)
        )

    /// <summary>Refines the current raw schema with an explicit refinement.</summary>
    [<CustomOperation("refine")>]
    member _.Refine
        (
            source: FieldWorking<'model, 'target, 'raw>,
            refinement: Refinement<'raw, 'target>
        ) : FieldWorking<'model, 'target, 'target> =
        FieldWorking(source.Initial, source.Schema |> SchemaCore.refine refinement)

    /// <summary>Refines the current raw schema with the destination type's canonical refinement.</summary>
    [<CustomOperation("refine")>]
    member _.Refine
        (source: FieldWorking<'model, 'target, 'raw>)
        : FieldRefining<'model, 'target, 'raw> =
        FieldRefining(source.Initial, source.Schema, [])

    /// <summary>Adds executable validation to the field's current schema value.</summary>
    [<CustomOperation("validate")>]
    member _.Validate
        (
            initial: FieldInitial<'model, 'target>,
            validation: 'target -> Result<unit, SchemaError>
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(initial, SchemaCore.validate validation)

    /// <summary>Adds another executable validation to an inferred field schema.</summary>
    [<CustomOperation("validate")>]
    member _.Validate
        (
            source: FieldConfigured<'model, 'target>,
            validation: 'target -> Result<unit, SchemaError>
        ) : FieldConfigured<'model, 'target> =
        FieldConfigured(source.Initial, source.Configure >> SchemaCore.validate validation)

    /// <summary>Adds executable validation to the field's current schema value.</summary>
    [<CustomOperation("validate")>]
    member _.Validate
        (
            source: FieldWorking<'model, 'target, 'current>,
            validation: 'current -> Result<unit, SchemaError>
        ) : FieldWorking<'model, 'target, 'current> =
        FieldWorking(source.Initial, source.Schema |> SchemaCore.validate validation)

    /// <summary>Adds executable validation after the pending refinement.</summary>
    [<CustomOperation("validate")>]
    member _.Validate
        (
            source: FieldRefining<'model, 'target, 'raw>,
            validation: 'target -> Result<unit, SchemaError>
        ) : FieldRefining<'model, 'target, 'raw> =
        FieldRefining(source.Initial, source.RawSchema, source.Validations @ [ validation ])

    member _.Run
        (source: FieldWorking<'model, 'target, 'target>)
        : FieldDeclaration<'model, 'target> =
        FieldDeclaration(
            { ExternalName = ExternalFieldName.create source.Initial.Name
              Order = FieldOrder.create 0
              Getter = source.Initial.Getter
              ValueSchema = source.Schema.ValueDefinition
              Rules = [] }
        )

    member _.Run
        (source: FieldConfigured<'model, 'target>)
        : ConfiguredFieldDeclaration<'model, 'target> =
        ConfiguredFieldDeclaration(source.Initial, source.Configure)

    member _.Run
        (source: FieldRefining<'model, 'target, 'raw>)
        : RefiningFieldDeclaration<'model, 'raw, 'target> =
        RefiningFieldDeclaration(source.Initial, source.RawSchema, source.Validations)

    [<CompilerMessage(
        "A field block must finish with the getter type. Add `refine` after raw-schema operations.",
        12001,
        IsError = true
    )>]
    member _.Run
        (source: FieldWorking<'model, 'target, 'current>)
        : FieldDeclaration<'model, 'target> =
        invalidOp $"Field '{source.Initial.Name}' has an unfinished raw schema."

[<EditorBrowsable(EditorBrowsableState.Never)>]
type FieldStep<'model, 'value> internal (definition: FieldDefinition<'model, 'value>) =
    member internal _.Definition = definition

[<EditorBrowsable(EditorBrowsableState.Never)>]
type ConstructorStep<'model, 'constructor> internal (constructor: 'constructor) =
    member internal _.Constructor = constructor

[<EditorBrowsable(EditorBrowsableState.Never)>]
type CheckedConstructorStep<'model, 'constructor> internal (constructor: 'constructor) =
    member internal _.Constructor = constructor

type internal ICeFields<'model, 'remaining, 'constructed> =
    abstract member GetFields: int -> obj list * int
    abstract member Apply: 'remaining * obj array * int -> 'constructed
    abstract member Build<'constructor, 'result> :
        factory: IRecordPlanCompiler<'model, 'result> *
        state: IRecordPlanState<'model, 'constructor, 'remaining> *
        order: int ->
            IRecordPlanState<'model, 'constructor, 'constructed> * int

type internal CeFieldsEmpty<'model, 'constructed>() =
    interface ICeFields<'model, 'constructed, 'constructed> with
        member _.GetFields(index) = [], index
        member _.Apply(constructed, _, _) = constructed
        member _.Build(_, state, order) = state, order

type internal CeFieldsCons<'model, 'field, 'tail, 'constructed>
    (
        field: FieldDefinition<'model, 'field>,
        tail: ICeFields<'model, 'tail, 'constructed>
    ) =
    interface ICeFields<'model, 'field -> 'tail, 'constructed> with
        member _.GetFields(index) =
            let descriptor: FieldDescriptor<'model> =
                { ExternalName = field.ExternalName
                  Order = FieldOrder.create index
                  Getter = fun model -> field.Getter model |> box
                  ValueSchema = field.ValueSchema
                  Rules = field.Rules }

            let rest, next = tail.GetFields(index + 1)
            box descriptor :: rest, next

        member _.Apply(constructor, arguments, index) =
            let next = constructor (unbox<'field> arguments[index])
            tail.Apply(next, arguments, index + 1)

        member _.Build(factory, state, order) =
            let typedField =
                Field(
                    { field with
                        Order = FieldOrder.create order }
                )

            let next = factory.OnField(order, typedField, state)
            tail.Build(factory, next, order + 1)

[<EditorBrowsable(EditorBrowsableState.Never)>]
type SchemaPlan<'model, 'expected, 'constructed, 'actual> internal
    (
        fields: obj,
        constructor: 'actual,
        finish: 'constructed -> Result<'model, string>
    ) =
    member internal _.Fields = fields
    member internal _.Constructor = constructor
    member internal _.Finish = finish

type internal CeCompiledRecordPlan<'model, 'constructor, 'constructed>
    (
        constructor: 'constructor,
        fields: ICeFields<'model, 'constructor, 'constructed>,
        finish: 'constructed -> Result<'model, string>
    ) =
    interface ICompiledRecordPlan<'model> with
        member _.CompilePlan(factory) =
            let initial = factory.OnEnd<'constructor>()
            let completed, _ = fields.Build(factory, initial, 0)
            factory.OnComplete(constructor, completed, finish)

[<RequireQualifiedAccess>]
module internal SchemaBuilderInternals =
    let close
        (constructor: 'constructor)
        (fields: ICeFields<'model, 'constructor, 'constructed>)
        (finish: 'constructed -> Result<'model, string>)
        : Schema<'model> =
        let descriptors, count =
            fields.GetFields 0
            |> fun (values, count) ->
                values |> List.map unbox<FieldDescriptor<'model>>, count

        let tryApply arguments =
            ConstructorApplication.ensureArgumentCount count arguments
            fields.Apply(constructor, arguments, 0) |> finish

        let application =
            { ArgumentCount = count
              ApplyTrusted =
                fun arguments ->
                    match tryApply arguments with
                    | Ok model -> model
                    | Error message -> invalidOp message
              TryApplyTrusted = tryApply }

        let compiled =
            CeCompiledRecordPlan<'model, 'constructor, 'constructed>(constructor, fields, finish)
            :> ICompiledRecordPlan<'model>

        Schema(ModelDefinition(ModelSchemaDefinition.create application descriptors), Some compiled)

/// <summary>Builds a typed record schema from ordered field declarations and a final constructor.</summary>
/// <exclude />
[<EditorBrowsable(EditorBrowsableState.Never)>]
type SchemaBuilder<'model>() =
    static member DefaultField
        (
            source: field<'model, 'value>,
            schema: Schema<'value>
        ) : FieldStep<'model, 'value> =
        FieldStep(
            { ExternalName = ExternalFieldName.create source.Name
              Order = FieldOrder.create 0
              Getter = source.Getter
              ValueSchema = schema.ValueDefinition
              Rules = [] }
        )

    static member RefinedField
        (
            source: RefiningFieldDeclaration<'model, 'raw, 'target>,
            refinement: Refinement<'raw, 'target>
        ) : FieldStep<'model, 'target> =
        let schema =
            source.Validations
            |> List.fold
                (fun current validation -> SchemaCore.validate validation current)
                (source.RawSchema |> SchemaCore.refine refinement)

        FieldStep(
            { ExternalName = ExternalFieldName.create source.Initial.Name
              Order = FieldOrder.create 0
              Getter = source.Initial.Getter
              ValueSchema = schema.ValueDefinition
              Rules = [] }
        )

    static member ConfiguredField
        (
            source: ConfiguredFieldDeclaration<'model, 'target>,
            schema: Schema<'target>
        ) : FieldStep<'model, 'target> =
        SchemaBuilder<'model>.DefaultField(
            field<'model, 'target>(source.Initial.Name, source.Initial.Getter),
            source.Configure schema
        )

    member inline _.Yield(field: field<'model, ^value>) : FieldStep<'model, ^value> =
        let schema: Schema< ^value> = SchemaDefaults.Resolve()
        SchemaBuilder<'model>.DefaultField(field, schema)

    member _.Yield(source: FieldDeclaration<'model, 'value>) =
        FieldStep(source.Definition)

    member inline _.Yield
        (field: RefiningFieldDeclaration<'model, ^raw, ^target>)
        : FieldStep<'model, ^target> =
        let refinement: Refinement<^raw, ^target> = RefinementDefaults.Resolve()
        SchemaBuilder<'model>.RefinedField(field, refinement)

    member inline _.Yield
        (field: ConfiguredFieldDeclaration<'model, ^target>)
        : FieldStep<'model, ^target> =
        let schema: Schema< ^target> = SchemaDefaults.Resolve()
        SchemaBuilder<'model>.ConfiguredField(field, schema)

    member _.Yield(step: ConstructorStep<'model, 'constructor>) =
        SchemaPlan<'model, 'model, 'model, 'constructor>(
            box (CeFieldsEmpty<'model, 'model>() :> ICeFields<'model, 'model, 'model>),
            step.Constructor,
            Ok
        )

    member _.Yield(step: CheckedConstructorStep<'model, 'constructor>) =
        SchemaPlan<'model, Result<'model, string>, Result<'model, string>, 'constructor>(
            box (
                CeFieldsEmpty<'model, Result<'model, string>>()
                :> ICeFields<'model, Result<'model, string>, Result<'model, string>>
            ),
            step.Constructor,
            id
        )

    member _.Combine
        (
            source: FieldStep<'model, 'value>,
            plan: SchemaPlan<'model, 'tail, 'constructed, 'constructor>
        ) =
        let tail =
            unbox<ICeFields<'model, 'tail, 'constructed>> plan.Fields

        let fields =
            CeFieldsCons<'model, 'value, 'tail, 'constructed>(source.Definition, tail)
            :> ICeFields<'model, 'value -> 'tail, 'constructed>

        SchemaPlan<'model, 'value -> 'tail, 'constructed, 'constructor>(
            box fields,
            plan.Constructor,
            plan.Finish
        )

    member _.Delay(factory: unit -> 'state) =
        factory()

    member _.Run
        (plan: SchemaPlan<'model, 'constructor, 'constructed, 'constructor>)
        : Schema<'model> =
        let fields =
            unbox<ICeFields<'model, 'constructor, 'constructed>> plan.Fields

        SchemaBuilderInternals.close plan.Constructor fields plan.Finish

/// <summary>
/// The concise schema-definition vocabulary: the record computation expression, its field and constructor
/// forms, and the collection-schema operations.
/// </summary>
/// <remarks>
/// <para>
/// Optional and opt-in, in the same shape as <c>Reified.DataSyntax</c> and <c>Reified.ConstraintSyntax</c>:
/// <c>open Reified.Schema</c> for <c>Schema</c>, then <c>open Reified.Schema.Syntax</c> for this vocabulary.
/// </para>
/// <para>
/// There is no constraint catalogue here. One <c>Constraint</c> vocabulary serves direct checking, refinement,
/// and Schema, so a field block reaches for <c>Constraint.email</c> or an opened
/// <c>Reified.ConstraintSyntax</c> exactly as standalone code does. Boundary supply is Schema-owned and stays
/// here as <c>mustSupply</c> and <c>mayOmit</c>.
/// </para>
/// </remarks>
module Syntax =
    /// <summary>Record-schema computation expression.</summary>
    let schema<'model> = SchemaBuilder<'model>()

    /// <summary>
    /// Declares a field under an explicit wire name, for when the name differs from the property and for
    /// portable code.
    /// </summary>
    /// <remarks>
    /// <c>field _.Email</c> derives the wire name from the property; reach for <c>fieldAs</c> when the wire name
    /// is not the camelCased property name, or when the code must also compile under Fable, which cannot run the
    /// quotation the derived form reads. Explicit names are never transformed.
    /// </remarks>
    /// <example><code>fieldAs "email_address" _.Email</code></example>
    let fieldAs (name: string) (getter: 'model -> 'value) =
        if isNull name then nullArg (nameof name)
        if isNull (box getter) then nullArg (nameof getter)
        field<'model, 'value>(name, getter)

    /// <summary>Closes a record schema with a total constructor.</summary>
    let construct<'model, 'constructor> (constructor: 'constructor) =
        if isNull (box constructor) then nullArg (nameof constructor)
        ConstructorStep<'model, 'constructor>(constructor)

    /// <summary>Closes a record schema with a checked constructor.</summary>
    let constructResult<'model, 'constructor> (constructor: 'constructor) =
        if isNull (box constructor) then nullArg (nameof constructor)
        CheckedConstructorStep<'model, 'constructor>(constructor)

    /// <summary>Adds a constraint to every item described by a list schema.</summary>
    /// <example><code>Schema.list () |> constrainItems Constraint.present</code></example>
    let constrainItems (constraint': Constraint<'item>) (schema: Schema<'item list>) : Schema<'item list> =
        if isNull constraint' then nullArg (nameof constraint')
        SchemaCore.constrainItems constraint' schema

    /// <summary>Adds a constraint to every value described by a string-keyed map schema.</summary>
    /// <example><code>Schema.map () |> constrainValues Constraint.present</code></example>
    let constrainValues (constraint': Constraint<'item>) (schema: Schema<Map<string, 'item>>) : Schema<Map<string, 'item>> =
        if isNull constraint' then nullArg (nameof constraint')
        SchemaCore.constrainValues constraint' schema
