---
title: JsonSchema
api:
  family: Reified.JsonSchema
  sections:
    - id: models
      title: Model schemas
      order: 10
      members: [generate]
      facets:
        task: [export]
        capability: [json-schema]
        audience: [common]
    - id: values
      title: Value schemas
      order: 20
      members: [generateValue]
      facets:
        task: [export]
        capability: [json-schema]
---

# `JsonSchema`

`JsonSchema.generate` publishes a schema as JSON Schema Draft 2020-12. Field names, required properties, nested shapes,
union tags, and portable interpreted constraints come from the same declaration used by parsing and JSON codecs.

Only rules the target can enforce are lowered to JSON Schema keywords. Runtime-only or non-portable constraints remain
visible as prose and `x-reified-runtime-constraints`; omitted rules are not marked as enforced.

`JsonSchema.generateValue` runs the same lowering for a standalone value schema — a primitive, a collection, a union,
or a refined field — rather than a record model schema. It returns the same compact JSON Schema Draft 2020-12 document
string as `generate`; the two names exist so a call site states whether it is exporting a model or a value. Use
`generateValue` when you hold a value schema without a surrounding record and want its document directly.
