namespace Reified.FableProbe

open System
open Reified.Constraint
open Reified.Schema
open Reified.Schema.Syntax
open Reified.Schema.Json

/// The Reified surface that must behave identically on .NET and on Fable JavaScript. Each function returns a
/// bool so the same assertions run on both targets from `Program.fs`; the Flow benchmarks these checks used to
/// travel with went to the Axial repository.
[<RequireQualifiedAccess>]
module Checks =

    type SchemaContact =
        {
            Name: string
            Age: int
        }


    type private SchemaFieldSummary =
        {
            Order: int
            ExternalName: string
        }

    type private SummaryChainResult<'model, 'constructorIn, 'constructorOut>(value: obj) =
        interface IRecordPlanState<'model, 'constructorIn, 'constructorOut> with
            member _.Value = value

    type private SummaryFactory<'model>() =
        interface IRecordPlanCompiler<'model, string list> with
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
                chain.Value
                :?> SchemaFieldSummary list
                |> List.map (fun field -> $"{field.Order}:{field.ExternalName}")

    let private contactSchema =
        schema<SchemaContact> {
            field "name" _.Name
            field "age" _.Age
            construct (fun name age -> { Name = name; Age = age })
        }

    let buildSchemaPlanSummary () =
        Schema.compilePlan (SummaryFactory<SchemaContact>()) contactSchema

    /// Exercises the type-directed constraint catalogue under Fable. The SRTP dispatchers behind `present`,
    /// `blank`, the cardinality family, and `optional` are the part of the design most at risk on this target,
    /// and code-point text sizing must agree with .NET on supplementary characters.
    /// Fable erases a Guid to a plain string and a TimeSpan to a number, so a boxed type test labels them
    /// `Text` and `Integer` here while .NET labels them correctly. Operand projection resolves on the static

    /// type instead; this asserts the two platforms describe the same constraint the same way.
    let runOperandAgreement () =
        let guid = Guid.Parse "4f489f3b-cd3c-4f53-b99b-fca552f8994d"
        let span = TimeSpan.FromMinutes 1.0

        let describes (expected: ConstraintAtom) (constraint': Constraint<'value>) =
            (Constraint.inspect constraint').Expression = ConstraintExpression.Atom expected

        [ describes (RelationAtom(Compared(Equal, ConstraintValue.Guid guid))) (Constraint.equalTo guid)
          describes (RelationAtom(Compared(AtLeast, ConstraintValue.TimeSpan span))) (Constraint.atLeast span)
          describes (MembershipAtom(OneOf [ ConstraintValue.Guid guid ])) (Constraint.oneOf [ guid ])
          describes (RelationAtom(Compared(Equal, ConstraintValue.Text "ada"))) (Constraint.equalTo "ada")
          describes (RelationAtom(Compared(AtLeast, ConstraintValue.Integer 3L))) (Constraint.atLeast 3) ]
        |> List.forall id

    /// The rendering edge under Fable: `Renderer.ofLookup` is the portable constructor, and contextual
    /// fallback, interpolation, nouns, and group joining must all behave as they do on .NET. The
    /// resource-manager constructors are deliberately absent here rather than compiling to a silent no-op.
    let runLocalizationSurface () =
        let failure rule value =
            match Constraint.check rule value with
            | Error violation -> violation
            | Ok() -> failwith "Expected the constraint to reject the value."

        let translations =
            Map
                [ "signup.name.constraint.presence.present", "doit être renseigné"
                  "attribute.signup.name", "Le nom"
                  "constraint.cardinality.minimum.other", "doit contenir au moins {minimum} éléments" ]

        let renderer =
            Renderer.ofLookup translations.TryFind
            |> Renderer.context "signup"
            |> Renderer.attribute "name"

        let present = failure (Constraint.present: Constraint<string>) ""
        let tags = failure (Constraint.minLength 3: Constraint<string list>) []
        let group = All(present, [ failure (Constraint.lengthBetween 2 40: Constraint<string>) "" ])

        let isbn =
            Constraint.customLocalized "books.isbn.invalid" "must be a valid ISBN" (fun (value: string) ->
                value.Length = 13)

        [ Violation.message renderer present = "doit être renseigné"
          Violation.fullMessage renderer present = "Le nom doit être renseigné"
          // Plural selection, named interpolation, and value rendering, all without a .NET culture.
          Violation.message (Renderer.ofLookup translations.TryFind) tags
              = "doit contenir au moins 3 éléments, but was 0"
          // The noun composes once around a whole group, not once per leaf.
          Violation.fullMessage (Renderer.english |> Renderer.attribute "firstName") group
              = "First name must be present and must have a size between 2 and 40, but was 0"
          Violation.message Renderer.english (failure isbn "short") = "must be a valid ISBN"
          Renderer.Advanced.attributeCandidates renderer = [ "attribute.signup.name"; "attribute.name" ]
          // Opaque descriptors keep structural equality on this runtime too.
          MessageDescriptor.Advanced.create "books.isbn.invalid" Map.empty
              = MessageDescriptor.Advanced.ofSegments [ "books"; "isbn"; "invalid" ] Map.empty
          Catalogue.keys |> List.forall (fun key -> Catalogue.english.ContainsKey key) ]
        |> List.forall id

    let runConstraintSurface () =
        let name: Constraint<string> = Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]
        let tags: Constraint<string list> = Constraint.all [ Constraint.minLength 1; Constraint.distinct ]
        let nickname: Constraint<string option> = Constraint.optional (Constraint.minLength 2)
        let ttl: Constraint<int> = Constraint.any (Constraint.equalTo -1) [ Constraint.atLeast 1 ]
        let emoji: Constraint<string> = Constraint.length 1
        let reserved: Constraint<string> = Constraint.noneOf [ "admin"; "root" ]
        let excluded: Constraint<string list> = Constraint.notContains "internal"

        [ Constraint.test name "Ada"
          not (Constraint.test name " ")
          Constraint.test tags [ "a"; "b" ]
          not (Constraint.test tags [ "a"; "a" ])
          Constraint.test nickname None
          Constraint.test nickname (Some "Ada")
          not (Constraint.test nickname (Some "A"))
          Constraint.test (Constraint.blank: Constraint<int voption>) ValueNone
          Constraint.test (Constraint.present: Constraint<int voption>) (ValueSome 1)
          Constraint.test ttl -1
          Constraint.test ttl 5
          not (Constraint.test ttl 0)
          // One code point, two UTF-16 units: JavaScript and .NET must agree.
          Constraint.test emoji "\U0001F600"
          Constraint.test Constraint.numeric "345"
          not (Constraint.test Constraint.numeric "\u0663\u0664\u0665")
          // Blankness must mean the same thing on both runtimes, or the exported pattern is sound on one and
          // not the other. U+FEFF is the character JavaScript calls whitespace and .NET Core does not.
          not (Constraint.test (Constraint.present: Constraint<string>) "\ufeff")
          not (Constraint.test (Constraint.present: Constraint<string>) "\u0085")
          not (Constraint.test Constraint.trimmed "\ufeffAda")
          Constraint.test Constraint.trimmed "Ada"
          Constraint.test reserved "ada"
          not (Constraint.test reserved "admin")
          Constraint.test excluded [ "public" ]
          not (Constraint.test excluded [ "internal" ])
          (match Constraint.check name "" with
           | Error violation -> Violation.render violation <> ""
           | Ok() -> false) ]
        |> List.forall id

    let runCodecRoundTrip () =
        let codec = Json.compile contactSchema
        let original = { Name = "Ada"; Age = 37 }
        let json = Json.serialize codec original
        Json.deserialize codec json
