---
weight: 40
title: Localization
type: docs
description: Rendering constraint failures as sentences, in one language or many.
---

# Localization

A `Violation` carries no language. Built-in failures carry Reified-owned identities and operands; opaque failures
carry the prose their author wrote. Turning either into a sentence happens at the rendering edge, through a
`Renderer`.

## If you never translate

`Violation.render` needs no renderer, no resources, and no setup:

```fsharp
42
|> Constraint.check (Constraint.between 0 10)
|> Result.mapError Violation.render
// Error "expected a value between 0 and 10, but was 42"
```

`Renderer.english` is the same idea with the renderer mechanics available — bare predicates, composed nouns, and
group joining — still without a resource file:

```fsharp
violation |> Violation.fullMessage Renderer.english
// "value must be at least 13, but was 11"
```

Everything below is for applications that want something other than Reified's English.

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

Anything the map does not answer falls back to Reified's neutral English, so a partial translation is a working
translation. Add keys as you need them; you never have to complete the catalogue before shipping.

Look keys up in [the catalogue](./catalogue/).

## The two messages

`Violation.message` renders a bare predicate. `Violation.fullMessage` composes the attribute noun around it once.

```fsharp
let field = renderer |> Renderer.context "signup" |> Renderer.attribute "name"

violation |> Violation.message field      // "must be present"
violation |> Violation.fullMessage field  // "Name must be present"
```

Use `message` where a label already names the field — a form row, or a Schema result whose returned path identifies
it. Use `fullMessage` for API payloads, logs, and anywhere the message stands alone.

The noun is composed once, never per leaf, so a group of three failures still names the field once:

```fsharp
let name : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]

"" |> Constraint.check name |> Result.mapError (Violation.fullMessage field)
// Error "Name must be present and must have a size between 2 and 40, but was 0"
```


## Building a renderer

Four constructors cover the ordinary cases.

```fsharp
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

```fsharp
services.AddSingleton(Renderer.ofCurrentCulture resources) |> ignore
```


## Recipes

Small things applications ask for, in the order they usually ask.

### Do not show the rejected value

By default a failure that carries an actual value reads "must be at least 13, but was 11". The value clause is its
own entry, so removing it is one override:

```fsharp
let quiet = Map.ofList [ "constraint.actual", "{message}" ]

violation |> Violation.message (Renderer.ofLookup quiet.TryFind)
// "must be at least 13"
```

### Translate only the rules you actually use

The four-key map above is the whole technique. Translate the identities your application produces, leave the rest,
and check what is missing with [Adding a language](../adding-a-language/).

### Translate one of your own rules

Give the rule a key and it joins the same lookup — see [Custom rules](./custom-rules/).

### Do not translate the English sentence

It is technically possible to render an English message and pass that whole string to a translation service at your
own edge. It works, and it is worse than it looks: the string has already lost the rule's identity and its operands,
so nothing can key a cached translation, agree on terminology across two messages, or format `13` the way the target
language formats numbers. Look up the identity instead; that is what the identity is for.

## Where next

- [Context and fallback](./context-and-fallback/) — scoping a renderer to a document and a field, and how lookup
  falls back.
- [Custom rules](./custom-rules/) — making your own constraints translatable.
- [Advanced rendering](./advanced-rendering/) — composition, interpolation, groups, plurals, and resolvers.
- [The key catalogue](./catalogue/) — every key Reified can produce.
- [Adding a language](../adding-a-language/) — the working order for a new translation, and proving coverage.
- [Fable support](../fable/) — what the rendering edge does and does not do in JavaScript.

Schema renders its own parse and structural failures through these same mechanics, with the field path supplied for
you: see [Redisplay and field errors]({{% relref "/schema/redisplay-and-field-errors" %}}).
