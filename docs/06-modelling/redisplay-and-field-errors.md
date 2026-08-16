---
weight: 30
title: Redisplay And Field Errors
type: docs
description: Failed parses that keep the user's input.
targetFramework: net8.0
---

# Redisplay And Field Errors

This page shows how failed schema parses retain structured data, path-aware field errors, and default display strings.

When boundary input fails to parse, a form should show the user's original text next to each field's errors. Reified's
`RetainedParseResult` keeps both: the structured data exactly as submitted, and diagnostics addressed by path.

## The Handoff Value

Use `Schema.parseRetainingInput` when the boundary needs the submitted representation after parsing:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let parsed = Schema.parseRetainingInput customerSchema raw

parsed.IsValid        // true when a trusted model exists
parsed.Result         // Ok model | Error diagnostics
parsed.Input          // the original Data, always retained
parsed.Errors         // flattened path-aware errors ([] when valid)
```

Schema parsing, schema validation, primitive `Parse` failures, `Refine`
failures, and value-level `Violation` values all lower to the same boundary taxonomy: `SchemaError`.

## Field Error Lookup

`ErrorsFor` addresses errors with the same path text used by structured data, including collection indexes:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
parsed.ErrorsFor "email"                // errors attached exactly to the email field
parsed.ErrorsFor "contacts[1].value"    // errors on the second contact's value
```

`SchemaError` deliberately omits the field name — the diagnostics path already carries it — so the same error value
renders correctly wherever it is attached.

## Redisplay

`Data` addresses submitted values by the same paths:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
Data.redisplayPath "email" parsed.Input          // "not-an-email", exactly as typed
Data.redisplayPath "contacts[1].value" parsed.Input
```

Absent input looks up as `Data.Null` and redisplays as blank text, so form templates never special-case absent fields.

## Rendering A Form

The typical loop over a failed parse:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
for field in formFields do
    let value = Data.redisplayPath field.Path parsed.Input
    let errors = parsed.ErrorsFor field.Path |> List.map SchemaError.render
    render field value errors
```

Because failed parses never construct the model, there is no half-valid object to guard against — the template works
from structured data and diagnostics only.

For summary output, render every failed diagnostic in one line:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let messages = RetainedParseResult.renderErrors parsed
// [ "email: Expected email format."; "age: Must satisfy atLeast 13; got 12." ]
```

## Localized Messages

`SchemaError.render` is the zero-dependency English default. To render in a language, pass a `Renderer` and let
Schema fold its typed path in as the attribute:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
open Reified

let signup = renderer |> Renderer.context "signup"

errors |> SchemaErrors.messages signup
// [ SchemaPath "email",              "must be a valid email"
//   SchemaPath "contacts[1].value",  "must be present" ]

errors |> SchemaErrors.fullMessages signup
// [ SchemaPath "email",              "Email must be a valid email"
//   SchemaPath "contacts[1].value",  "Value must be present" ]

errors |> SchemaErrors.toStringWith signup   // one full message per line
```

`messages` returns bare predicates, which is what a form wants: the returned `SchemaPath` already identifies the field, so
a template that renders its own label does not print the name twice. `fullMessages` composes the attribute noun once
for payloads, logs, and summaries.

You supply only the document context. Index components stay out of resource keys — `contacts[0].value` and
`contacts[1].value` are one field for a translator — and stay in every returned path, so field lookup and redisplay
still work:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
for field in formFields do
    let value = Data.redisplayPath field.Path parsed.Input
    let messages =
        errors
        |> SchemaErrors.messages signup
        |> List.filter (fun (path, _) -> SchemaPath.format path = field.Path)
        |> List.map snd

    render field value messages
```

Constraint failures use Reified's `constraint.*` catalogue; Schema's own parse and structural failures have their own,
below. Both render through the same mechanics, so one renderer covers both. See
[Localization](/validating-values/localization/index.html) for those mechanics and
[Adding a language](/validating-values/adding-a-language.html) for generating a translation.

## The Schema catalogue

`Reified.Schema` owns its own keys for parse, boundary-supply, and structural failures. They stay in that package —
Schema depends on Constraint and never the reverse.

| Key | Arguments | Default English |
| --- | --- | --- |
| `schema.omitted` | — | must be supplied |
| `schema.blank` | — | must be present |
| `schema.expectedScalar` | — | must be a single value |
| `schema.expectedObject` | — | must be an object |
| `schema.expectedMany` | — | must be a collection |
| `schema.invalidFormat` | `expected` | must be a valid {expected} |
| `schema.parseOutOfRange` | `target` | must be within the range of {target} |
| `schema.unknownTag` | `choices` | must be one of {choices} |

`SchemaMessages.keys`, `.arguments`, and `.english` expose the same data. Constructor failures and custom errors
carrying authored prose have no catalogue entry: Schema does not invent a key for text your application wrote.

At `SchemaPath.root`, full rendering uses `constraint.attribute.default` — "value" in English — never the document context.

## Mapping To Domain Errors

`RetainedParseResult.mapErrors` translates interpreter errors into a domain or application error type at the boundary while
preserving the structured data and paths:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let domainParsed = parsed |> RetainedParseResult.mapErrors SignupError.ofSchemaError
```

That mapping is the boundary between Reified's interpreter errors and your application errors. Keep your user-owned error
union in the application, and translate `SchemaError` with one function when the parsed result crosses that boundary.
