# `Inspect`

`Inspect.model` turns a schema into finite metadata without parsing input or running application code. The result
contains field names, nested shapes, union cases, and the inspectable constraints attached to each value.

Forms, admin interfaces, and documentation tools can use this model to choose controls and display rules before a user
submits data. Opaque constraints remain identified as runtime-only because their predicates cannot be exported.
