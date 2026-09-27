---
weight: 40
title: The key catalogue
type: docs
description: Every constraint message key, its arguments, and its default English.
targetFramework: net8.0
---

## The key catalogue

Every key Reified can produce. `actual` is not listed as an argument on any predicate: it arrives through the
separate `constraint.actual` entry.

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.presence.present` | n/a | must be present |
| `constraint.presence.blank` | n/a | must be blank |
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
| `constraint.uniqueness` | n/a | must not contain duplicate values |
| `constraint.format.email` | n/a | must be an email address |
| `constraint.format.trimmed` | n/a | must not have leading or trailing whitespace |
| `constraint.format.numeric` | n/a | must contain digits only |
| `constraint.format.alphanumeric` | n/a | must contain letters and digits only |
| `constraint.format.pattern` | `pattern` | n/a | must match {pattern} |
| `constraint.number.multipleOf` | `divisor` | n/a | must be a multiple of {divisor} |
| `constraint.number.finite` | n/a | must be a finite number |

A built-in whose operand Reified cannot describe reports the relation rather than approximating the
operand. These carry no arguments:

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.unsupportedOperand.relation.equal` | n/a | must equal the required value |
| `constraint.unsupportedOperand.relation.notEqual` | n/a | must not equal the excluded value |
| `constraint.unsupportedOperand.relation.greaterThan` | n/a | must be greater than the required value |
| `constraint.unsupportedOperand.relation.lessThan` | n/a | must be less than the required value |
| `constraint.unsupportedOperand.relation.atLeast` | n/a | must be at least the required value |
| `constraint.unsupportedOperand.relation.atMost` | n/a | must be at most the required value |
| `constraint.unsupportedOperand.within` | n/a | must be within the required range |
| `constraint.unsupportedOperand.contains` | n/a | must contain the required value |
| `constraint.unsupportedOperand.multipleOf` | n/a | must be a multiple of the required value |

The composition and joining entries:

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.attribute.default` | n/a | value |
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

The same data is available at runtime, so a coverage test never has to copy this page:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
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
