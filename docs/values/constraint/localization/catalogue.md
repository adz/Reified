---
weight: 40
title: The key catalogue
type: docs
description: Every constraint message key, its arguments, and its default English.
---

## The key catalogue

Every key Reified can produce. `actual` is not listed as an argument on any predicate: it arrives through the
separate `constraint.actual` entry.

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.presence.present` | — | — | must be present |
| `constraint.presence.blank` | — | — | must be blank |
| `constraint.cardinality.exact` | `expected` | `expected` | must have a size of exactly {expected} |
| `constraint.cardinality.minimum` | `minimum` | `minimum` | must have a size of at least {minimum} |
| `constraint.cardinality.maximum` | `maximum` | `maximum` | must have a size of at most {maximum} |
| `constraint.cardinality.between` | `minimum`, `maximum` | — | must have a size between {minimum} and {maximum} |
| `constraint.relation.equal` | `expected` | — | must be {expected} |
| `constraint.relation.notEqual` | `expected` | — | must not be {expected} |
| `constraint.relation.greaterThan` | `expected` | — | must be greater than {expected} |
| `constraint.relation.lessThan` | `expected` | — | must be less than {expected} |
| `constraint.relation.atLeast` | `expected` | — | must be at least {expected} |
| `constraint.relation.atMost` | `expected` | — | must be at most {expected} |
| `constraint.relation.within` | `minimum`, `maximum` | — | must be between {minimum} and {maximum} |
| `constraint.membership.oneOf` | `choices` | — | must be one of {choices} |
| `constraint.membership.noneOf` | `choices` | — | must not be one of {choices} |
| `constraint.membership.contains` | `item` | — | must contain {item} |
| `constraint.membership.notContains` | `item` | — | must not contain {item} |
| `constraint.uniqueness` | — | — | must not contain duplicate values |
| `constraint.format.email` | — | — | must be an email address |
| `constraint.format.trimmed` | — | — | must not have leading or trailing whitespace |
| `constraint.format.numeric` | — | — | must contain digits only |
| `constraint.format.alphanumeric` | — | — | must contain letters and digits only |
| `constraint.format.pattern` | `pattern` | — | must match {pattern} |
| `constraint.number.multipleOf` | `divisor` | — | must be a multiple of {divisor} |
| `constraint.number.finite` | — | — | must be a finite number |

A built-in whose operand Reified cannot describe reports the relation rather than approximating the
operand. These carry no arguments:

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.unsupportedOperand.relation.equal` | — | — | must equal the required value |
| `constraint.unsupportedOperand.relation.notEqual` | — | — | must not equal the excluded value |
| `constraint.unsupportedOperand.relation.greaterThan` | — | — | must be greater than the required value |
| `constraint.unsupportedOperand.relation.lessThan` | — | — | must be less than the required value |
| `constraint.unsupportedOperand.relation.atLeast` | — | — | must be at least the required value |
| `constraint.unsupportedOperand.relation.atMost` | — | — | must be at most the required value |
| `constraint.unsupportedOperand.within` | — | — | must be within the required range |
| `constraint.unsupportedOperand.contains` | — | — | must contain the required value |
| `constraint.unsupportedOperand.multipleOf` | — | — | must be a multiple of the required value |

The composition and joining entries:

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.attribute.default` | — | — | value |
| `constraint.actual` | `message`, `actual` | — | {message}, but was {actual} |
| `constraint.fullMessage` | `attribute`, `message` | — | {attribute} {message} |
| `constraint.group.all.pair` | `first`, `second` | — | {first} and {second} |
| `constraint.group.all.start` | `first`, `rest` | — | {first}, {rest} |
| `constraint.group.all.middle` | `first`, `rest` | — | {first}, {rest} |
| `constraint.group.all.end` | `first`, `second` | — | {first} and {second} |
| `constraint.group.any.pair` | `first`, `second` | — | {first} or {second} |
| `constraint.group.any.start` | `first`, `rest` | — | {first}, {rest} |
| `constraint.group.any.middle` | `first`, `rest` | — | {first}, {rest} |
| `constraint.group.any.end` | `first`, `second` | — | {first} or {second} |
| `constraint.list.pair` | `first`, `second` | — | {first} and {second} |
| `constraint.list.start` | `first`, `rest` | — | {first}, {rest} |
| `constraint.list.middle` | `first`, `rest` | — | {first}, {rest} |
| `constraint.list.end` | `first`, `second` | — | {first} and {second} |

The same data is available at runtime, so a coverage test never has to copy this page:

```fsharp
Catalogue.keys            // string list
Catalogue.arguments       // Map<string, string list>
Catalogue.english         // Map<string, string>
Catalogue.pluralArgument  // Map<string, string option>
```

A test enumerates the atom union against both this page and `Catalogue`, so a new rule cannot ship with its key
undocumented or unimplemented.


## Argument values

Arguments are `ConstraintValue`, the closed value model every interpreter understands: text, char, boolean, integer, decimal, big integer,
float, GUID, timespan, date-time, date-time-offset, null, and lists of those. `ConstraintValue.render` gives the
invariant rendering of one; a renderer formats through its value culture instead.
