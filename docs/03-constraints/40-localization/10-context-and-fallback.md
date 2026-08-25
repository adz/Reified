---
weight: 10
title: Context and fallback
type: docs
description: Scoping a renderer to a document and a field, and the order lookup falls back through.
targetFramework: net8.0
---

# Context and fallback

## Context and attribute

A renderer holds two scoping roles, and they behave differently on purpose.

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let signup = renderer |> Renderer.context "signup"     // appends a segment
let name = signup |> Renderer.attribute "name"         // replaces the whole attribute
let email = name |> Renderer.attribute "email"         // "name" is gone, not nested
let bare = email |> Renderer.unscoped                  // both roles cleared
```


`context` appends because a document, model, form, and component nest. `attribute` replaces because a form-scoped
renderer gets reused across sibling fields, and an appending `attribute` would quietly produce
`signup.name.email` on the second field.

The context is never used as a noun. With no attribute, `fullMessage` uses the contextual
`constraint.attribute.default` — "value" in English — so a whole-model failure reads "value must be present"
rather than "Signup must be present".

Schema supplies its typed path as the attribute for you; see
[Redisplay and field errors](/schema/redisplay-and-field-errors.html).

### Message fallback

For identity `constraint.presence.present`, context `signup`, and attribute path `address.postcode`, lookup removes
rightmost specificity one segment at a time:

```text
signup.address.postcode.constraint.presence.present
signup.address.constraint.presence.present
signup.constraint.presence.present
constraint.presence.present
```


The message identity itself is never truncated. `books.isbn.invalid` never degrades to `books.isbn` or `books`:
those are segments of one name, not a namespace to search.

That is what makes a per-field override cheap. Translate `constraint.presence.present` once, then override only the
handful of fields whose wording genuinely differs.

### Attribute nouns

Attribute nouns use their own prefix and their own chain. For context `signup` and attribute path
`address.postcode`:

```text
attribute.signup.address.postcode
attribute.address.postcode
attribute.postcode
```


If no resource resolves, Reified humanizes the final raw attribute segment. Humanization splits camelCase,
`snake_case`, and `kebab-case`, preserves acronym runs, keeps `_id`, and applies invariant sentence casing:

```text
postcode         -> Postcode
firstName        -> First name
postcodeID       -> Postcode ID
billing_address  -> Billing address
```


Humanization applies only to a raw segment Reified invented a noun for. A value that came out of a resource file is
returned exactly as authored — no recasing, trimming, or Unicode normalization, ever.

### Segments are opaque

Context names, attribute names, and Schema path keys are arbitrary text. Reified rejects an explicitly empty segment
and encodes the reserved characters before joining:

```text
% -> %25
. -> %2E
[ -> %5B
] -> %5D
```


`%` is encoded first, so a field literally named `%2E` cannot collide with a field named `.`. Consequently these
two renderers address different resources:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
renderer |> Renderer.context "address.postcode"

renderer |> Renderer.context "address" |> Renderer.context "postcode"
```


Emitted hex digits are uppercase. Lookup never decodes and re-encodes your resource keys.

