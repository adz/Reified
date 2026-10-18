---
weight: 40
title: The key catalogue
type: docs
description: How to find every constraint message key, its arguments, and its default English.
targetFramework: net8.0
---

## The key catalogue

Every built-in key Reified can produce is listed beside the [`Catalogue` API reference](/api/Reified.Catalogue.html):
the key, its arguments, which argument pluralizes, and its default English. `Constraint.customLocalized` and
`customLocalizedWith` name your own catalogue entries, which no table can enumerate.

The same data is available at runtime, so a coverage test never has to copy the table:

```fsharp
let keys : string list = Catalogue.keys
let arguments : Map<string, string list> = Catalogue.arguments
let english : Map<string, string> = Catalogue.english
let pluralArgument : Map<string, string option> = Catalogue.pluralArgument
```

A test enumerates the atom union against both the reference page and `Catalogue`, so a new rule cannot ship with its
key undocumented or unimplemented.

Arguments are `ConstraintValue`, the closed value model every interpreter understands: text, char, boolean, integer,
decimal, big integer, float, GUID, timespan, date-time, date-time-offset, null, and lists of those.
`ConstraintValue.render` gives the invariant rendering of one; a renderer formats through its value culture instead.
