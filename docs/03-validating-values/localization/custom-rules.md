---
weight: 20
title: Custom rules
type: docs
description: Making your own constraints translatable, with English prose as the fallback.
targetFramework: net8.0
---

# Custom rules

`Constraint.custom` hands Reified prose and nothing else, so prose is all Reified can give back: it renders verbatim in
every language. Name a key and the rule becomes translatable:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let isbn =
    Constraint.customLocalized
        "books.isbn.invalid"
        "must be a valid ISBN"
        isValidIsbn

let isbnWithLength =
    Constraint.customLocalizedWith
        "books.isbn.invalid"
        "must be a valid ISBN"
        (Map.ofList [ "expectedLength", ConstraintValue.Integer 13L ])
        isValidIsbn
```

The prose stays required and becomes the fallback: an untranslated language still says something true. The key
takes the ordinary contextual chain, so for context `signup` and attribute `book`:

```text
signup.book.books.isbn.invalid
signup.books.isbn.invalid
books.isbn.invalid
```

A key is `segment ("." segment)*`. An empty key or empty segment is rejected at construction — a malformed key
written in source is a defect, and failing at the call site beats failing at a rendering edge in whichever language
nobody tested. `%`, brackets, whitespace, and non-ASCII characters are exact input; you never pre-encode a key.

Custom constraints declare no plural operand, and Reified does not infer one from an argument's name or value.
Guessing would silently change which key a translator has to supply. If you need `.one`/`.other` for a custom rule,
select it in an [advanced resolver](/validating-values/localization/advanced-rendering.html#advanced-resolvers).

Reified never invents a key for `Constraint.custom` prose. A key it made up would name a catalogue entry that does
not exist, and the lookup would fail in production, in the language you did not test.

