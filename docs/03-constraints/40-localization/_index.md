---
weight: 40
title: Localization
type: docs
description: Rendering constraint failures as sentences, in one language or many.
targetFramework: net8.0
---

# Localization

A `Violation` carries no language. Built-in failures carry Reified-owned identities and operands; opaque failures
carry the prose their author wrote. Turning either into a sentence happens at the rendering edge, through a
`Renderer`.

## If you never translate

`Violation.render` needs no renderer, no resources, and no setup:

```fsharp
open Reified

42
|> Constraint.check (Constraint.between 0 10)
|> Result.mapError Violation.render
// Error "expected a value between 0 and 10, but was 42"
```


`Renderer.english` is the same idea with the renderer mechanics available, bare predicates, composed nouns, and
group joining, still without a resource file:

```fsharp
let violation =
    match Constraint.check (Constraint.atLeast 13) 11 with
    | Error violation -> violation
    | Ok () -> failwith "unreachable"

violation |> Violation.fullMessage Renderer.english
// "value must be at least 13, but was 11"
```


## Translate four keys

A renderer is a key-to-template lookup. The smallest useful one is a map:

```fsharp
let french =
    Map.ofList
        [ "constraint.presence.present", "doit être renseigné"
          "constraint.cardinality.minimum", "doit contenir au moins {minimum} éléments"
          "constraint.format.email", "doit être une adresse e-mail"
          "attribute.name", "Nom" ]

let renderer = Renderer.ofLookup french.TryFind
```


Keys missing from the map fall back to Reified's neutral English. You can translate only the keys your application uses.

Look keys up in [the catalogue](/constraints/localization/catalogue.html).

## The two messages

`Violation.message` renders the failure with no attribute noun; when the violation carries an actual value, it is
appended through the `constraint.actual` composition entry. `Violation.fullMessage` composes the attribute noun
around that once. Using the `violation` and `renderer` from above:

```fsharp
let field = renderer |> Renderer.context "signup" |> Renderer.attribute "name"

violation |> Violation.message field      // "must be at least 13, but was 11"
violation |> Violation.fullMessage field  // "Nom must be at least 13, but was 11"
```


The French map has no `constraint.relation.atLeast` entry, so the predicate falls back to Reified's neutral
English; the `attribute.name` entry does resolve, which is why the noun is `Nom`.


Use `message` where a label already names the field, a form row, or a Schema result whose returned path identifies
it. Use `fullMessage` for API payloads, logs, and anywhere the message stands alone.

The noun is composed once, never per leaf, so a group of three failures still names the field once:

```fsharp
let name : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]

"" |> Constraint.check name |> Result.mapError (Violation.fullMessage field)
// Error "Name must be present and must have a size between 2 and 40, but was 0"
```


## Building a renderer

```fsharp no-check reason="Illustrates each constructor's call shape; translations/resources/culture are the caller's own ResourceManager and CultureInfo, not values this page can construct standalone."
// Any key-to-template lookup. The portable constructor, and the one Fable uses.
Renderer.ofLookup translations.TryFind

// A .NET resource manager and one culture for lookup, plurals, and operand formatting.
Renderer.ofResourceManager resources culture

// Interface language and operand conventions separately: English text, German decimal separators.
Renderer.ofResourceManagerWithCultures resources uiCulture valueCulture

// CurrentUICulture and CurrentCulture, read on every render rather than captured.
Renderer.ofCurrentCulture resources
```


`Renderer.english` needs no resources at all: every entry falls back to Reified's neutral English. It is the default
for tests, tools, and applications that never translate.

`ofCurrentCulture` is the one place ambient culture enters Reified. It reads the thread's cultures per render, so one
renderer registered as a singleton follows a per-request culture. Constraint execution itself stays free of ambient
effects.

Register a renderer as an ordinary immutable value. Reified introduces no renderer interface, no ambient registry,
and no global configuration:

```fsharp no-check reason="Illustrates ASP.NET Core DI registration; services and resources are the host application's own, not values this page can construct standalone."
services.AddSingleton(Renderer.ofCurrentCulture resources) |> ignore
```


## Recipes

### Do not show the rejected value

By default a failure that carries an actual value reads "must be at least 13, but was 11". The value clause is its
own entry, so removing it is one override:

```fsharp
let quiet = Map.ofList [ "constraint.actual", "{message}" ]

violation |> Violation.message (Renderer.ofLookup quiet.TryFind)
// "must be at least 13"
```


### Translate only the rules you actually use

Translate the identities your application produces, leave the rest, and check what is missing with
[Adding a language](/constraints/adding-a-language.html).

### Translate one of your own rules

Give the rule a key and it joins the same lookup, see [Custom rules](/constraints/localization/custom-rules.html).

### Do not translate the English sentence

Rendering an English message before translation loses the rule's identity and operands. Look up the identity to
cache translations, use consistent terminology, and format `13` for the target language.

## Where next

- [Context and fallback](/constraints/localization/context-and-fallback.html), scoping a renderer to a document and a field, and how lookup
  falls back.
- [Custom rules](/constraints/localization/custom-rules.html), making your own constraints translatable.
- [Advanced rendering](/constraints/localization/advanced-rendering.html), composition, interpolation, groups, plurals, and resolvers.
- [The key catalogue](/constraints/localization/catalogue.html), every key Reified can produce.
- [Adding a language](/constraints/adding-a-language.html), the working order for a new translation, and proving coverage.
- [Fable support](/constraints/fable.html), what the rendering edge does and does not do in JavaScript.

Schema renders its own parse and structural failures through these same mechanics, with the field path supplied for
you: see [Redisplay and field errors](/schema/redisplay-and-field-errors.html).
