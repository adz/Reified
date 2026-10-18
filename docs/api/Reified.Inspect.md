---
title: Inspect
api:
  family: Reified.Inspect
  sections:
    - id: models
      title: Model inspection
      order: 10
      members: [model, field]
      facets:
        task: [inspect]
        audience: [common]
    - id: values
      title: Value inspection
      order: 20
      members: [schema]
      facets:
        task: [inspect]
---

# `Inspect`

`Inspect.model` turns a completed model schema into finite metadata: field names, nested shapes, union cases, and the
inspectable constraints attached to each value. Inspection reads the schema declaration and returns plain data — it
never parses input, runs constraint checks, invokes field getters, or constructs the model. Opaque constraints stay
identified as runtime-only because their predicates cannot be exported; inspection reports their author-supplied
description without calling the predicate.

Inspection does force deferred (`Schema.defer`) value declarations so recursive shapes can be described, but executes
nothing else from the declaration.

`Inspect.schema` describes a standalone value schema and returns a `SchemaDescription`: the value's shape, declared
format, portable constraint metadata, supply rule, and description. `Inspect.model` describes a record schema and
returns a `ModelDescription`: the ordered field list, where each `FieldDescription` carries the field name, aliases,
order, supply rule, and its own value `SchemaDescription`. Use `Inspect.model` for forms and admin UIs that need the
field inventory of a model; use `Inspect.schema` when you hold a value schema — a primitive, collection, union, or
refined field — and want its metadata without a surrounding record.

Forms, admin interfaces, and documentation tools can choose controls and display rules from this metadata before a user
submits data.
