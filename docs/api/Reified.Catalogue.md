# `Catalogue`

`Catalogue` is the built-in message catalogue for `Reified.Constraint`: one canonical source for every interpreted
rule's key segments, argument names, neutral English predicate, and plural operand. Violation rendering and the public
coverage data both read this table, so a rule cannot ship with its message undeclared. Entries are bare predicates,
never whole sentences; the attribute noun and the optional actual-value clause are separate composition entries.

Every built-in key Reified can produce is listed below. `Constraint.customLocalized` and `customLocalizedWith` name your
own catalogue entries, which no table here can enumerate. `actual` is not listed as an argument on any predicate: it
arrives through the separate `constraint.actual` entry.

## Built-in keys

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.presence.present` | n/a | n/a | must be present |
| `constraint.presence.blank` | n/a | n/a | must be blank |
| `constraint.cardinality.exact` | `expected` | `expected` | must have a size of exactly {expected} |
| `constraint.cardinality.minimum` | `minimum` | `minimum` | must have a size of at least {minimum} |
| `constraint.cardinality.maximum` | `maximum` | `maximum` | must have a size of at most {maximum} |
| `constraint.cardinality.between` | `minimum`, `maximum` | n/a | must have a size between {minimum} and {maximum} |
| `constraint.relation.equal` | `expected` | n/a | must be {expected} |
| `constraint.relation.notEqual` | `expected` | n/a | must not be {expected} |
| `constraint.relation.greaterThan` | `expected` | n/a | must be greater than {expected} |
| `constraint.relation.lessThan` | `expected` | n/a | must be less than {expected} |
| `constraint.relation.atLeast` | `expected` | n/a | must be at least {expected} |
| `constraint.relation.atMost` | `expected` | n/a | must be at most {expected} |
| `constraint.relation.within` | `minimum`, `maximum` | n/a | must be between {minimum} and {maximum} |
| `constraint.membership.oneOf` | `choices` | n/a | must be one of {choices} |
| `constraint.membership.noneOf` | `choices` | n/a | must not be one of {choices} |
| `constraint.membership.contains` | `item` | n/a | must contain {item} |
| `constraint.membership.notContains` | `item` | n/a | must not contain {item} |
| `constraint.uniqueness` | n/a | n/a | must not contain duplicate values |
| `constraint.format.email` | n/a | n/a | must be an email address |
| `constraint.format.trimmed` | n/a | n/a | must not have leading or trailing whitespace |
| `constraint.format.numeric` | n/a | n/a | must contain digits only |
| `constraint.format.alphanumeric` | n/a | n/a | must contain letters and digits only |
| `constraint.format.pattern` | `pattern` | n/a | must match {pattern} |
| `constraint.number.multipleOf` | `divisor` | n/a | must be a multiple of {divisor} |
| `constraint.number.finite` | n/a | n/a | must be a finite number |

## Unsupported-operand keys

A built-in whose operand Reified cannot describe reports the relation rather than approximating the operand. These
carry no arguments:

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.unsupportedOperand.relation.equal` | n/a | n/a | must equal the required value |
| `constraint.unsupportedOperand.relation.notEqual` | n/a | n/a | must not equal the excluded value |
| `constraint.unsupportedOperand.relation.greaterThan` | n/a | n/a | must be greater than the required value |
| `constraint.unsupportedOperand.relation.lessThan` | n/a | n/a | must be less than the required value |
| `constraint.unsupportedOperand.relation.atLeast` | n/a | n/a | must be at least the required value |
| `constraint.unsupportedOperand.relation.atMost` | n/a | n/a | must be at most the required value |
| `constraint.unsupportedOperand.within` | n/a | n/a | must be within the required range |
| `constraint.unsupportedOperand.contains` | n/a | n/a | must contain the required value |
| `constraint.unsupportedOperand.multipleOf` | n/a | n/a | must be a multiple of the required value |

## Composition and joining keys

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.attribute.default` | n/a | n/a | value |
| `constraint.actual` | `message`, `actual` | n/a | {message}, but was {actual} |
| `constraint.fullMessage` | `attribute`, `message` | n/a | {attribute} {message} |
| `constraint.group.all.pair` | `first`, `second` | n/a | {first} and {second} |
| `constraint.group.all.start` | `first`, `rest` | n/a | {first}, {rest} |
| `constraint.group.all.middle` | `first`, `rest` | n/a | {first}, {rest} |
| `constraint.group.all.end` | `first`, `second` | n/a | {first} and {second} |
| `constraint.group.any.pair` | `first`, `second` | n/a | {first} or {second} |
| `constraint.group.any.start` | `first`, `rest` | n/a | {first}, {rest} |
| `constraint.group.any.middle` | `first`, `rest` | n/a | {first}, {rest} |
| `constraint.group.any.end` | `first`, `second` | n/a | {first} or {second} |
| `constraint.list.pair` | `first`, `second` | n/a | {first} and {second} |
| `constraint.list.start` | `first`, `rest` | n/a | {first}, {rest} |
| `constraint.list.middle` | `first`, `rest` | n/a | {first}, {rest} |
| `constraint.list.end` | `first`, `second` | n/a | {first} and {second} |

## Runtime access

The same data is available at runtime, so a coverage test never has to copy this page:

```fsharp
let keys : string list = Catalogue.keys
let arguments : Map<string, string list> = Catalogue.arguments
let english : Map<string, string> = Catalogue.english
let pluralArgument : Map<string, string option> = Catalogue.pluralArgument
```

Arguments are `ConstraintValue`, the closed value model every interpreter understands: text, char, boolean, integer,
decimal, big integer, float, GUID, timespan, date-time, date-time-offset, null, and lists of those.
`ConstraintValue.render` gives the invariant rendering of one; a renderer formats through its value culture instead.

See [The key catalogue](/localization/catalogue.html) for how a language skeleton consumes these entries.
