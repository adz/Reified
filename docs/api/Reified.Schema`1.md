# `Schema<'model>`

A `Schema<'model>` is a reusable declaration of a structured boundary. It records the input shape, field names,
value schemas, constraints, refinements, and the constructor that produces `'model`.

Parsing executes that declaration against untrusted `Data`. Every independent field is attempted, failures retain
their paths, and the constructor runs only after all fields succeed. The result is either `'model` or `SchemaErrors`.

Other interpreters read the same declaration without changing its meaning. `Inspect.model` returns finite metadata;
`JsonSchema.generate` publishes the enforceable wire shape; `Json.compile` builds a trusted JSON codec.

A schema guarantees values produced through the schema. It cannot stop callers constructing a public record directly.
Use refined fields or a private model when the invariant must hold for every value of the type.

Start with the [Schema quickstart](/schema/quickstart.html), then use the members below when composing schemas directly.
