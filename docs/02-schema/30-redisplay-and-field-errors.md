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

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Customer = { Email: string; Age: int }

let customerSchema =
    schema<Customer> {
        field _.Email { constraints [ present; email ] }
        field _.Age { constrain (atLeast 13) }
        construct (fun email age -> { Email = email; Age = age })
    }

let raw = Data.ofNameValues [ "email", "not-an-email"; "age", "12" ]

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

```fsharp
parsed.ErrorsFor "email"                // errors attached exactly to the email field
parsed.ErrorsFor "contacts[1].value"    // errors on the second contact's value, if this model had one
```


`SchemaError` deliberately omits the field name, the diagnostics path already carries it, so the same error value
renders correctly wherever it is attached.

## Redisplay

`Data` addresses submitted values by the same paths:

```fsharp
Data.redisplayPath "email" parsed.Input          // "not-an-email", exactly as typed
Data.redisplayPath "contacts[1].value" parsed.Input
```


Absent input looks up as `Data.Null` and redisplays as blank text, so form templates never special-case absent fields.

## Rendering A Form

The typical loop over a failed parse:

```fsharp
type FormField = { Path: string }

let formFields = [ { Path = "email" }; { Path = "age" } ]

let render (field: FormField) (value: string) (errors: string list) =
    printfn "%s = %A (%A)" field.Path value errors

for field in formFields do
    let value = Data.redisplayPath field.Path parsed.Input
    let errors = parsed.ErrorsFor field.Path |> List.map SchemaError.render
    render field value errors
```


Because failed parses never construct the model, there is no half-valid object to guard against; the template works
from structured data and diagnostics only.

For summary output, render every failed diagnostic in one line:

```fsharp
let messages = RetainedParseResult.renderErrors parsed
```


## Localized Messages

`SchemaError.render` is the zero-dependency English default. To render in a language, pass a `Renderer` and let
Schema fold its typed path in as the attribute:

```fsharp
let signup = Renderer.english |> Renderer.context "signup"

match parsed.Result with
| Error errors ->
    errors |> SchemaErrors.messages signup |> ignore
    errors |> SchemaErrors.fullMessages signup |> ignore
    errors |> SchemaErrors.toStringWith signup |> ignore   // one full message per line
| Ok _ -> ()
```


`messages` returns bare predicates, which is what a form wants: the returned `SchemaPath` already identifies the field, so
a template that renders its own label does not print the name twice. `fullMessages` composes the attribute noun once
for payloads, logs, and summaries.

You supply only the document context. Index components stay out of resource keys, `contacts[0].value` and
`contacts[1].value` are one field for a translator, and stay in every returned path, so field lookup and redisplay
still work:

```fsharp no-check reason="Continues the match branch above (errors : SchemaErrors is only in scope inside it); shown flattened here to focus on the filter."
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
[Localization](/constraints/localization/index.html) for those mechanics and
[Adding a language](/constraints/adding-a-language.html) for generating a translation.

## The Schema catalogue

`Reified.Schema` owns its own keys for parse, boundary-supply, and structural failures. They stay in that package;
Schema depends on Constraint and never the reverse.

| Key | Arguments | Default English |
| --- | --- | --- |
| `schema.omitted` | n/a | must be supplied |
| `schema.blank` | n/a | must be present |
| `schema.expectedScalar` | n/a | must be a single value |
| `schema.expectedObject` | n/a | must be an object |
| `schema.expectedMany` | n/a | must be a collection |
| `schema.invalidFormat` | `expected` | must be a valid {expected} |
| `schema.parseOutOfRange` | `target` | must be within the range of {target} |
| `schema.unknownTag` | `choices` | must be one of {choices} |

`SchemaMessages.keys`, `.arguments`, and `.english` expose the same data. Constructor failures and custom errors
carrying authored prose have no catalogue entry: Schema does not invent a key for text your application wrote.

At `SchemaPath.root`, full rendering uses `constraint.attribute.default`, "value" in English, never the document context.

## Mapping To Domain Errors

`RetainedParseResult.mapErrors` translates interpreter errors into a domain or application error type at the boundary while
preserving the structured data and paths:

```fsharp no-check reason="SignupError is an application-owned error type this page cannot declare on its own; illustrates the mapping shape, not a standalone unit."
let domainParsed = parsed |> RetainedParseResult.mapErrors SignupError.ofSchemaError
```


That mapping is the boundary between Reified's interpreter errors and your application errors. Keep your user-owned error
union in the application, and translate `SchemaError` with one function when the parsed result crosses that boundary.
