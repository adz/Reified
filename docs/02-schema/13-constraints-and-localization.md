---
weight: 13
title: Constraints and localization in schemas
description: Reuse value rules in Schema and render path-aware failures in the user's language.
targetFramework: net8.0
---

# Constraints and localization in schemas

Install `Reified.Schema` (`dotnet add package Reified.Schema`); it depends on `Reified.Constraint`.
Open `Reified` for `Schema`, `Constraint`, `Data`, `Renderer`, and the error types; open `Reified.SchemaDSL` for record fields and `Reified.ConstraintDSL` for unqualified rule names.

A field block's `constrain` and `constraints` operations attach the same `Constraint<'value>` you can use with `Constraint.check`. `Schema.constrain` attaches one to a standalone value schema. Refined fields can use a refinement's constraint and add boundary-specific rules. A successfully parsed value passes those rules; a failed rule produces a `Violation`, not a parse error. `Schema.parse` returns `SchemaErrors`: each `SchemaIssue` pairs a `SchemaPath` with a `SchemaError`, including `SchemaError.Violation` for failed constraints. Parse, missing-field, and structural errors remain distinct cases.

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup = { Name: string }

let signup =
    schema<Signup> {
        field _.Name {
            constrain present
        }
        construct (fun name -> { Name = name })
    }

let french =
    Map.ofList
        [ "constraint.presence.present", "doit être renseigné"
          "attribute.name", "Nom" ]

let renderer = Renderer.ofLookup french.TryFind |> Renderer.context "signup"

match Schema.parse signup (Data.ofNameValues [ "name", "" ]) with
| Error errors ->
    let messages = errors |> SchemaErrors.fullMessages renderer
    let path, message = List.head messages
    assert (SchemaPath.format path = "name")
    assert (message = "Nom doit être renseigné")
| Ok _ -> failwith "Expected a missing name"
```

`SchemaErrors.messages` returns bare predicates alongside paths for forms with their own labels; `fullMessages` includes the localized attribute noun. Schema supplies the field path as the renderer attribute, so provide the document context, not `Renderer.attribute` for each issue. Untranslated keys fall back to neutral English. Choose `Renderer.english` for English without resources, `Renderer.ofLookup` for portable translations, or a resource-manager constructor on .NET with an explicit culture. Custom rules need `Constraint.customLocalized` and their own catalogue entry to translate; authored prose stays as written. To add a language, generate and fill catalogue entries for both `Catalogue` (constraints) and `SchemaMessages` (schema failures).

Read [Constraints](/constraints/index.html) for the value-rule vocabulary and [Localization](/localization/index.html) for renderer setup, context/fallback, custom rules, and the language workflow. [Redisplay and field errors](/schema/redisplay-and-field-errors.html) covers presenting the paths in a form.
