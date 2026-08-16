---
weight: 30
title: Advanced rendering
type: docs
description: Composition, interpolation, operand formatting, groups, plurals, and resolvers.
targetFramework: net8.0
---

# Advanced rendering

Most applications never need this page. Read it when the default composition is not the sentence your language wants,
or when your localization system, not Reified, should select and render entries.

## Message composition

Localized entries are bare predicates:

```text
constraint.presence.present    = must be present
constraint.relation.atLeast    = must be at least {expected}
constraint.cardinality.between = must have a size between {minimum} and {maximum}
```

The noun and the actual-value clause are separate entries:

```text
constraint.attribute.default = value
constraint.actual            = {message}, but was {actual}
constraint.fullMessage       = {attribute} {message}
```

Rendering one interpreted leaf goes: resolve the predicate, wrap it in `constraint.actual` if the violation carries
an actual value, and — for `fullMessage` only — wrap that in `constraint.fullMessage` with the resolved noun.

Keeping them separate is what makes `{actual}` optional without an optional-placeholder rule, and it lets a locale
put the value or the noun somewhere English would not:

```text
constraint.actual      = reçu {actual} au lieu de « {message} »
constraint.fullMessage = {message} — {attribute}
```


## Named interpolation

Templates use named placeholders with an optional format suffix:

```text
constraint.number.multipleOf = must be a multiple of {divisor:N0}
```

`{{` and `}}` produce literal braces. Placeholder parsing is identical on every target.

Resource defects degrade rather than throw:

- an unknown placeholder name stays literal, so a translator's typo shows up in the message instead of taking down
  the request;
- unmatched or malformed braces make that template unusable, and lookup continues down the fallback chain to the
  neutral English;
- an unknown or unsupported format suffix falls back to ordinary formatting of the value.

Exceptions raised by *your* lookup, resolver, or formatter callbacks propagate untouched. Those are application
defects, not resource misses, and swallowing one would hide the bug in whichever locale nobody exercises.

### Operand formatting

Operands are `ConstraintValue`. The built-in formatter uses the renderer's value culture and honours format
suffixes. Two hooks replace it:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
// One uniform callback. Format suffixes are ignored.
renderer |> Renderer.withValues ConstraintValue.render

// The suffix, when you need it.
renderer
|> Renderer.Advanced.withValueFormatting (fun request -> format request.Value request.Format)
```

A list operand — `choices` on `Constraint.oneOf`, for instance — is joined through the contextual
`constraint.list.*` patterns, and each item goes through the formatter.


## Lists and groups

Joining is pattern-driven, not hard-coded:

```text
constraint.group.all.pair    constraint.group.any.pair    constraint.list.pair
constraint.group.all.start   constraint.group.any.start   constraint.list.start
constraint.group.all.middle  constraint.group.any.middle  constraint.list.middle
constraint.group.all.end     constraint.group.any.end     constraint.list.end
```

`pair` and `end` take `{first}` and `{second}`; `start` and `middle` take `{first}` and `{rest}`. The algorithm is
deterministic:

| Items | Result |
| --- | --- |
| none | `""`, with no lookup |
| one | the item itself, with no lookup |
| two | `pair` |
| three or more | combine the last two with `end`, fold the preceding items right-to-left with `middle`, and apply `start` to the first |

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
Violation.message Renderer.english group
// "must be present, must have a size between 2 and 40 and must be an email address"
```

A language whose joining cannot be expressed as pair/start/middle/end — where the conjunction changes the words of
its members, or the whole group has to be reordered — is not served by these patterns. That case takes
`Violation.toMessageTree` and owns its own traversal. This is a genuine limit, not a gap to work around with a
cleverer pattern.


## Plurals

An entry may declare at most one plural operand. Where one is declared, ordinary lookup tries `<key>.one` when the
operand is exactly one and `<key>.other` otherwise, then the bare key — at each contextual level, before moving
outwards:

```text
signup.tags.constraint.cardinality.minimum.other
signup.tags.constraint.cardinality.minimum
signup.constraint.cardinality.minimum.other
signup.constraint.cardinality.minimum
constraint.cardinality.minimum.other
constraint.cardinality.minimum
```

A bare field-specific entry therefore beats a pluralized model-level entry: the field override is the more
deliberate statement.

`.one`/`.other` covers the languages that need only those two. Full CLDR category selection belongs to an advanced
resolver.


## Advanced resolvers

`Renderer.ofLookup` owns the entire candidate order and asks a plain lookup for exact keys. When your localization
system wants to select plural categories and render entries itself — ICU, for instance — take a resolver instead.
It receives one request per contextual level:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
type MessageRequest =
    { BaseKey: string                              // encoded contextual key, no plural suffix
      Arguments: Map<string, ConstraintValue>
      PluralArgument: string option }

let renderer =
    Renderer.Advanced.ofResolver (fun request ->
        match icu.TryGet request.BaseKey with
        | Some entry -> Some (MessageResolution.Rendered (entry.Format request.Arguments))
        | None -> None)
```

The answer means:

- `None` — continue to the next, less specific level;
- `MessageResolution.Template template` — Reified interpolates and formats it;
- `MessageResolution.Rendered text` — you rendered it; Reified never interpolates that text again, so literal braces
  in it stay literal.

`Rendered` is final for the entry that was requested. Reified may still use its text as the `{message}` of
`constraint.actual`, as a child of a group pattern, or as the `{message}` of `constraint.fullMessage`, because
those are separate entries with their own resolution.

A resolver does not replace group traversal. A system that must reorder or reinterpret a whole group takes
`Violation.toMessageTree`.

### Inspecting what will be asked for

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
Renderer.Advanced.lookupCandidates renderer spec   // exact encoded keys, in order
Renderer.Advanced.messageRequests renderer spec    // one request per contextual level
Renderer.Advanced.attributeCandidates renderer     // encoded attribute-noun keys
Renderer.Advanced.format spec renderer             // render any catalogue's entry
```

These take a `MessageFormatSpec`: a message identity and arguments (`MessageDescriptor`) plus the owning
catalogue's neutral fallback and plural operand. That pairing is what lets Schema — or your own catalogue — reuse
every renderer mechanic without `Reified.Constraint` knowing a single one of its keys.

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let spec =
    MessageDescriptor.Advanced.ofSegments [ "billing"; "cardExpired" ] Map.empty
    |> MessageFormatSpec.Advanced.create "card has expired" None

renderer |> Renderer.Advanced.format spec
```

`lookupCandidates` and the rest return *encoded* resource keys. Canonical unencoded identity comes only from
`MessageDescriptor.key`.


## `Violation.render` compatibility

`Violation.render` remains the resource-free, culture-free path, unchanged. It does not go through `Renderer`.

| Violation | `render` | `message Renderer.english` | `fullMessage Renderer.english` |
| --- | --- | --- | --- |
| presence, no actual | `value must be present` | `must be present` | `value must be present` |
| relation, actual `11` | `expected a value at least 13, but was 11` | `must be at least 13, but was 11` | `value must be at least 13, but was 11` |
| opaque prose | prose unchanged | prose unchanged | noun composed once around the prose |
| group | legacy `; ` and `, or ` join | localized group composed once | noun composed once around the group |

Localized English uses bare predicates and composition, which reads better in a sentence. Exact wording
compatibility is promised for `render` only.

