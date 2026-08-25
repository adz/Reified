# `Schema`

The `Schema` module contains primitive schemas, collection and recursion combinators, union constructors, and the
operations that parse or check a completed declaration.

Use `Schema.parse` for untrusted `Data`. It decodes fields, applies constraints and refinements, accumulates independent
failures as `SchemaErrors`, and invokes the model constructor only after the fields succeed.

Use `Schema.check` when a typed value came from another constructor, an import, or a database mapper. It reads the
declared fields back from the value and checks them.

The same model constructor runs again, so cross-field invariants are checked too. Success returns the checked value
itself.

The [Schema quickstart](/schema/quickstart.html) introduces the workflow. The member catalogue below is the complete
construction and execution vocabulary.
