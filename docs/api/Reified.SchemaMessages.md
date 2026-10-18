# `SchemaMessages`

`SchemaMessages` is `Reified.Schema`'s own message catalogue for parse, boundary-supply, and structural failures. These
keys stay in the Schema package; Schema depends on Constraint and never the reverse. Constraint failures use the
`constraint.*` catalogue (`Catalogue.keys`); Schema's own failures use these keys. Both render through the same
mechanics.

| Key | Arguments | Default English |
| --- | --- | --- |
| `schema.omitted` | n/a | must be supplied |
| `schema.blank` | n/a | must be present |
| `schema.expectedScalar` | n/a | must be a single value |
| `schema.expectedObject` | n/a | must be an object |
| `schema.expectedMany` | n/a | must be a collection |
| `schema.invalidFormat` | `expected` | must be a valid {expected} |
| `schema.parseOutOfRange` | `target` | must be within the range of {target} |
| `schema.unknownTag` | `choices` | must be one of {choices} |

`SchemaMessages.keys`, `.arguments`, and `.english` expose the same data at runtime. Constructor failures and custom
errors carrying authored prose have no catalogue entry: Schema does not invent a key for text your application wrote.

At `SchemaPath.root`, full rendering uses `constraint.attribute.default`, "value" in English, never the document
context.

See [Redisplay And Field Errors](/schema/redisplay-and-field-errors.html) for how these keys render in a form loop.
