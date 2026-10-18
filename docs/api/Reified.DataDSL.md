# `DataDSL`

`Reified.DataDSL` provides short names and the `=>` and `?=>` operators for data construction and editing. Every DSL
name maps to an explicit `Reified.Data` or `DataEdit` function; each builds the same value the qualified name builds.

## Literal and edit mappings

| DataDSL | Explicit API | Result |
| --- | --- | --- |
| `name => value` | `Data.assoc name value` | `DataField` |
| `name ?=> option` | `Data.optionalAssoc name option` | `DataField` |
| `nil` | `Data.Null` | `Data` |
| `num token` | `Data.number token` | `Data` |
| `data fields` | `Data.data fields` | `Data` |
| `fields value` | `Data.fields value` | `DataField list` |
| `set path value` | `DataEdit.set path value` | `DataEdit` |
| `replace path value` | `DataEdit.replace path value` | `DataEdit` |
| `remove path` | `DataEdit.remove path` | `DataEdit` |
| `append path value` | `DataEdit.append path value` | `DataEdit` |
| `prepend path value` | `DataEdit.prepend path value` | `DataEdit` |
| `insert path index value` | `DataEdit.insert path index value` | `DataEdit` |
| `rename path name` | `DataEdit.rename path name` | `DataEdit` |
| `update path function` | `DataEdit.update path function` | `DataEdit` |
| n/a | `Data.patch edits input` | changed `Data` |

`set` makes the final path contain the value, adding a missing final object field when necessary. `replace` requires
the target to exist, so a misspelled or unexpectedly absent path fails.

## Matching and case-generation names

The matching and case-generation names also live in `Reified.DataDSL`. Without opening it, qualify them as
`Reified.DataDSL.at`, `Reified.DataDSL.containing`, `Reified.DataDSL.variant`, and so on.

| Area | Names |
| --- | --- |
| Paths | `at`, `absent` |
| Object patterns | `exactly`, `containing` |
| List patterns | `containingItems`, `inOrder`, `allItems`, `someItem` |
| General patterns | `any`, `anyText`, `anyNumber`, `oneOf`, `satisfying` |
| Matching | `matching` |
| Cases | `variant`, `variants`, `dimension`, `matrix` |

`Data.tryMatch` is always qualified because it is not part of the DSL.

See [DataDSL](/data/dsl.html) for a full example with and without the DSL open.
