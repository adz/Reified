---
title: Schema
targetFramework: net8.0
api:
  family: Reified.Schema
  name: Schema
  packageSections:
    - package: Reified.Schema
      id: declaration
      title: Declare schemas
      order: 10
      summary: Define model shapes, fields, primitive values, collections, unions, and refinements.
      entities: [Reified.Schema, "Reified.Schema`1", Reified.SchemaDSL, Reified.Field, "Reified.Field`2", "Reified.field`2", Reified.EnumCase, "Reified.EnumCase`1", Reified.UnionCase, "Reified.UnionCase`1", Reified.ExternalFieldName, Reified.ExternalFieldNameModule, Reified.FieldOrder, Reified.FieldOrderModule, Reified.RefinedSchemas, Reified.SchemaDefaults, Reified.SchemaFormat, Reified.SchemaFormatModule, Reified.Supply, Reified.UnionRepresentation, Reified.UnionRepresentations, Reified.UnionPayloadStyle, "Reified.IRecordPlanCompiler`2", "Reified.IRecordPlanState`3"]
    - package: Reified.Schema
      id: execution
      title: Parse and validate
      order: 20
      summary: Run schemas against untrusted input and inspect path-aware failures.
      entities: [Reified.SchemaError, Reified.SchemaErrorModule, Reified.SchemaErrors, Reified.SchemaErrorsModule, Reified.SchemaIssue, Reified.SchemaPath, Reified.SchemaPathModule, Reified.SchemaParseOptions, Reified.RetainedParseResult, "Reified.RetainedParseResult`1", Reified.SchemaCheck, Reified.SchemaMessages, Reified.SchemaValidation]
    - package: Reified.Schema
      id: interpreters
      title: Interpret schemas
      order: 30
      summary: Inspect metadata, compile JSON codecs, publish JSON Schema, and migrate versioned contracts.
      entities: [Reified.Inspect, Reified.Json, "Reified.JsonCodec`1", Reified.JsonCodecException, Reified.JsonIndent, Reified.JsonLineEnding, Reified.JsonWriteOptions, Reified.JsonSchema, Reified.Contract, "Reified.Contract`1", "Reified.ContractBuilder`2", Reified.ContractError, Reified.MigrationError, Reified.VersionSource]
    - package: Reified.Schema
      id: descriptions
      title: Schema descriptions
      order: 40
      summary: Consume the finite metadata exposed by schema inspection.
      entities: [Reified.ModelDescription, Reified.FieldDescription, Reified.SchemaDescription, Reified.SchemaShape, Reified.PrimitiveValueKind, Reified.EnumCaseDescription, Reified.EnumDescription, Reified.UnionCaseDescription, Reified.UnionCaseShape, Reified.UnionDescription]
    - package: Reified.Schema
      id: derivation
      title: Generated schema declarations
      order: 50
      summary: Annotate records and unions for build-time schema generation.
      entities: [Reified.DerivedSchema.AtLeastAttribute, Reified.DerivedSchema.AtMostAttribute, Reified.DerivedSchema.DefaultAttribute, Reified.DerivedSchema.DeriveSchemaAttribute, Reified.DerivedSchema.DeriveUnionAttribute, Reified.DerivedSchema.DistinctAttribute, Reified.DerivedSchema.EmailAttribute, Reified.DerivedSchema.FormatAttribute, Reified.DerivedSchema.GreaterThanAttribute, Reified.DerivedSchema.LengthAttribute, Reified.DerivedSchema.LengthBetweenAttribute, Reified.DerivedSchema.LessThanAttribute, Reified.DerivedSchema.MaxAttribute, Reified.DerivedSchema.MinAttribute, Reified.DerivedSchema.MultipleOfAttribute, Reified.DerivedSchema.PatternAttribute, Reified.DerivedSchema.PresentAttribute, Reified.DerivedSchema.SchemaAliasAttribute, Reified.DerivedSchema.SchemaConstructorAttribute, Reified.DerivedSchema.SchemaNameAttribute, Reified.DerivedSchema.SuppliedAttribute, Reified.DerivedSchema.UnionPayloadStyleKind, Reified.DerivedSchema.UnionRepresentationKind]
  sections:
    - id: primitives
      title: Primitive schemas
      order: 10
      members: [text, date, dateTime, guid]
      facets:
        task: [define]
        audience: [common]
    - id: collections
      title: Collections and optional values
      order: 20
      members: [list, listWith, option, map, mapWith, mapWithKey]
      facets:
        task: [compose]
    - id: transformations
      title: Transformations and invariants
      order: 30
      members: [convert, tryConvert, refine, validate, constrain, constrainAll, admit]
      facets:
        task: [transform]
        capability: [validation]
    - id: unions
      title: Unions and recursion
      order: 40
      members: [union, unionWith, enum, defer]
      facets:
        task: [compose]
    - id: boundary
      title: Boundary behavior and metadata
      order: 50
      members: [mustSupply, mayOmit, withFormat, describe, withDefault, format, description, defaultValue, constraints, supply]
      facets:
        capability: [metadata]
    - id: execution
      title: Parse and check
      order: 60
      members: [parse, parseWith, parseWithOptions, parseRetainingInput, check]
      facets:
        task: [execute]
        audience: [common]
    - id: interpretation
      title: Interpretation
      order: 70
      members: [compilePlan]
      facets:
        audience: [advanced]
---

# `Schema`

The `Schema` module contains primitive schemas, collection and recursion combinators, union constructors, and the
operations that parse or check a completed declaration.

Start from the task:

- Declare a record schema and parse untrusted input: [Schema quickstart](/schema/quickstart.html).
- Fields, field blocks, and constructors: [SchemaDSL](/schema/dsl.html).
- Reusable value rules: [Constraints](/constraints/constraints.html).
- Invariant-carrying field types: [Refined Schemas](/schema/refined-values.html).
- JSON in and out: [JSON Codecs](/schema/json-codecs.html).
- Published shape: [`JsonSchema`](/api/Reified.JsonSchema.html).
- Older wire versions: [Versioned Contracts](/schema/versioned-contracts.html).

Use `Schema.parse` for untrusted `Data`. It decodes fields, applies constraints and refinements, accumulates independent
failures as `SchemaErrors`, and invokes the model constructor only after the fields succeed.

Use `Schema.check` when a typed value came from another constructor, an import, or a database mapper. It reads the
declared fields back from the value and checks them.

The same model constructor runs again, so cross-field invariants are checked too. Success returns the checked value
itself.

Both operations return `Result<_, SchemaErrors>`. The difference is only the input: `Schema.parse` reads untrusted
`Data` and decodes each field, while `Schema.check` reads fields back from a value that already exists. The field
rules, constructor, error cases, and paths are the same.

```fsharp
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup = { Email: string; Age: int }

let signupSchema =
    schema<Signup> {
        field _.Email { constraints [ present; email ] }
        field _.Age { constrain (atLeast 13) }
        construct (fun email age -> { Email = email; Age = age })
    }

let input = Data.ofNameValues [ "email", "ada"; "age", "11" ]

Schema.parse signupSchema input
// Result<Signup, SchemaErrors>: untrusted Data, every field checked and accumulated

Schema.check signupSchema { Email = "ada"; Age = 11 }
// Result<Signup, SchemaErrors>: an existing value, the same field rules and constructor
```

The [Schema quickstart](/schema/quickstart.html) introduces the workflow.

## Constraint equivalents

These are the handwritten operations emitted for derivation attributes. Use them inside a field block with
`constrain`, except for the metadata operations shown directly:

| Purpose | Schema DSL |
| --- | --- |
| Pattern | `constrain (pattern expression)` |
| Minimum, maximum, exact, or bounded natural length | `constrain (minLength n)`, `maxLength`, `length`, `lengthBetween` |
| Present value or supplied input key | `constrain present`, `mustSupply` |
| Inclusive/exclusive numeric bounds | `constrain (atLeast n)`, `greaterThan`, `atMost`, `lessThan` |
| Numeric multiple | `constrain (multipleOf n)` |
| Distinct list elements | `constrain distinct` |
| Email text | `constrain email` |
| Open format metadata | `format (SchemaFormat.create name)` |
| Omitted-input default | `defaultValue value` |

See [Derivation Attributes](/schema/derivation/attributes.html) for the complete attribute mapping.
