# `DataPattern`

`DataPattern` is the closed vocabulary of produced-data expectations. Each pattern checks one shape of a `Data` node;
combine them with `matching` / `Data.tryMatch` to assert on selected parts of a produced JSON tree without listing
unmentioned fields.

| Form | What it accepts |
| --- | --- |
| `at path pattern` | A present value at the path that satisfies the pattern. |
| `absent path` | No value at the path. |
| a literal such as `"Ada"` | That exact value. |
| `exactly value` | An explicit exact recursive pattern. |
| `containing fields` | An object with at least the listed matching fields. |
| `containingItems patterns` | A list containing each pattern in any order; occurrences are consumed once. |
| `inOrder patterns` | A list containing the patterns as an ordered subsequence. |
| `allItems pattern` | A list where every item matches. |
| `someItem pattern` | A list where at least one item matches. |
| `any` | Any present value. |
| `anyText` | Any text value. |
| `anyNumber` | Any number token. |
| `oneOf patterns` | A value matching at least one alternative. |
| `satisfying description predicate` | A value accepted by a custom predicate. |

`matching expectations actual` returns `unit` or raises `DataMatchException`. `Data.tryMatch expectations actual`
returns `Result<unit, DataMismatch list>` and accumulates mismatches from every expectation, each carrying the
`ExpectationIndex`, `Path`, and actual value.

These patterns check `Data` nodes, not extracted typed values. `Constraint` checks typed values; tests of produced JSON
need the former. `Reified.Data` has no dependencies, so a test project can use patterns without a validation library.

See [Match selected parts of data](/data/how-to-test-produced-json.html) for the walkthrough.
