# `Schema<'model>`

A `Schema<'model>` is a reusable declaration of a structured boundary. It records the input shape, field names,
value schemas, constraints, refinements, and the constructor that produces `'model`.

Parsing executes that declaration against untrusted `Data`. Every independent field is attempted, failures retain
their paths, and the constructor runs only after all fields succeed. The result is either `'model` or `SchemaErrors`.

Other interpreters read the same declaration, and each one makes a different claim about it:

| Interpreter | Produces | Claim |
| --- | --- | --- |
| `Inspect.model` | finite schema metadata | Describes the declaration; runs no checks |
| `JsonSchema.generate` | a JSON Schema document | Exports the portable rules the target format can express |
| `Json.compile` | a compiled JSON codec | Decodes the wire shape only; does not run constraint metadata |

`Inspect.model` reads the retained shape. `JsonSchema.generate` publishes what a portable consumer enforces and keeps
the rest as prose and `x-reified-runtime-constraints`. `Json.compile` is the trusted path: it decodes bytes for
producers and storage you trust, so it enforces the wire shape but does not admit untrusted values the way
`Schema.parse` does.

A schema guarantees values produced through the schema. It cannot stop callers constructing a public record directly.
Use refined fields or a private model when the invariant must hold for every value of the type.

The [Schema quickstart](/schema/quickstart.html) introduces schema composition.
