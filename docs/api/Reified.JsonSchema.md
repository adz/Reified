# `JsonSchema`

`JsonSchema.generate` publishes a schema as JSON Schema Draft 2020-12. Field names, required properties, nested shapes,
union tags, and portable interpreted constraints come from the same declaration used by parsing and JSON codecs.

Only rules the target can enforce are lowered to JSON Schema keywords. Runtime-only or non-portable constraints remain
visible as prose and `x-reified-runtime-constraints`; generation never claims that another tool enforces an omitted rule.
