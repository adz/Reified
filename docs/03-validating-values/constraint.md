---
weight: 20
title: Constraint
type: docs
notoc: true
description: Declare a rule once and get the check, the failure, and its message from that one declaration.
targetFramework: net8.0
---

# Constraint

Most validation stacks make you write every rule twice: once as the check, and once as the message that
explains it to a person. The two live in different places — an attribute and a resource key, a builder call
and a message override, a predicate and a string literal — and they drift. Someone widens a length limit
and the error still quotes the old one.

`Reified.Constraint` has no second place to write it. A `Constraint<'value>` is a reusable description of
valid values, and the failure it produces is derived from that same description. **check** is the operation
that runs it.

```fsharp
open Reified

let retryCount : Constraint<int> =
    Constraint.between 0 10

3 |> Constraint.test retryCount   // true

42
|> Constraint.check retryCount
|> Result.mapError Violation.render
// Error "expected a value between 0 and 10, but was 42"

3 |> Constraint.guard retryCount  // Ok 3
```

Nobody wrote `"expected a value between 0 and 10, but was 42"`. Change the bounds and the message changes
with them, because it was never a separate artefact to keep in step.

## When you only want your own error

Plenty of code does not want a `Violation` at all. It wants a function that returns the application's own error case,
and nothing else. `Constraint.guard` keeps the checked value, and `Result.orError` throws the violation away in favour
of your error:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let validateEmail raw : Result<string, SignupError> =
    raw
    |> Constraint.guard Constraint.email
    |> Result.orError InvalidEmail
```

That is the whole function. It is no longer, and no more ceremonious, than the equivalent hand-written predicate — and
unlike the predicate, `Constraint.email` is still the same value you can later put in a refinement, a schema, or a
JSON Schema document without rewriting the rule.

`Result.orError` comes from `Reified.Result`. Without that package, `Result.mapError (fun _ -> InvalidEmail)` does the
same thing.

## Where the rules for a model live

Once there is more than one rule, they belong together in a module rather than scattered across the code that
uses them. That is also where `Reified.ConstraintDSL` earns its place: it exposes the same constructors
without the `Constraint.` prefix, which reads well when the module name already says what these rules are for.

```fsharp
open Reified

module SignupRules =
    open Reified.ConstraintDSL

    let emailAddress : Constraint<string> = Constraint.all [ present; email; maxLength 254 ]
    let age : Constraint<int> = atLeast 13
```

Open it inside the module, not at the top of a file — `present` and `email` are ordinary words, and their
meaning should be obvious from the two lines above them. The DSL changes vocabulary, not semantics: every name
returns the same `Constraint<'value>` the qualified name returns. Code elsewhere refers to `SignupRules.age`,
and the rest of this page uses `Constraint.` spellings so each example stands on its own.

→ [ConstraintDSL](/validating-values/dsl.html)

Reach for the structured path when the extra facts earn their keep — when you want to classify failures, render
messages in more than one language, or report several field failures at once. Then keep the violation and
`Result.mapError` it into a case that carries it:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
raw
|> Constraint.guard Constraint.email
|> Result.mapError InvalidEmail   // InvalidEmail of Violation
```

The rest of this page is about that second path. Nothing here forces you onto it.

## The failure carries facts, not prose

A `Violation` holds the failing constraint atom and, where Reified can represent it, the actual value. It carries no
language and no formatting. That is what keeps it comparable data you can retain, assert on in a test, and
pass across a boundary without dragging a culture or a `ResourceManager` along with it.

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
match "ab" |> Constraint.check (Constraint.minLength 3: Constraint<string>) with
| Ok () -> ()
| Error violation ->
    Violation.tryExpectation violation
    // Some (CardinalityAtom (Cardinality.Minimum 3))

    Violation.tryActual violation
    // Some (ConstraintValue.Integer 2L)
```

Application code can classify a failure without parsing a sentence, and a test can assert on the fact
rather than on the wording.

## One language, or many, from the same violation

Prose happens at the rendering edge. `Violation.render` is the zero-dependency English default and needs no
setup at all. When you need more, a `Renderer` carries the language and the document context while the
violation carries the facts:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let field = renderer |> Renderer.context "signup" |> Renderer.attribute "name"

violation |> Violation.message field      // "must be present"
violation |> Violation.fullMessage field  // "Name must be present"
```

Give the renderer a different culture and the identical violation reads `"Le nom doit être renseigné"`,
with contextual fallback, and without any application code walking a violation tree or reproducing Reified's
key catalogue.

Translation is cheap here because it is not a feature bolted on afterwards — it is the same split that
removed the duplicated message in the first place. Shipping in one language still gets the benefit; you
simply never build the resources.

## The same declaration is read by everything downstream

There is one vocabulary, not several. A rule named in a module works unchanged in a refinement and in a
schema, and the DSL spelling reads the same in all three places:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
module RetryRules =
    open Reified.ConstraintDSL

    let count : Constraint<int> = Constraint.between 0 10

let retryCountRefinement =
    Refinement.define RetryRules.count RetryCount _.Value

let schema =
    Schema.int |> Schema.constrain RetryRules.count
```

A constraint built from named parts lowers to JSON Schema, generates test data, and renders localizable
messages; the equivalent hand-written lambda does none of those things, and says so honestly rather than
pretending.

The catalogue resolves across text, collections, options, and maps by the type it is used at, so most uses
need nothing extra:

```fsharp
Schema.text |> Schema.constrain Constraint.present
```

A standalone binding is the exception: the annotation is the only type information there, so it is what
selects the shape.

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
module SignupRules =
    open Reified.ConstraintDSL

    let requiredName : Constraint<string> = present
    let selectedPlan : Constraint<string option> = present
```

Both are `present`; the annotation is what decides whether it means non-blank text or a supplied option.

The same value facts appear at three further levels:

- [Refined values](/domain-types/domain-values.html) use a constraint to construct invariant-carrying domain types.
- [Schema](/modelling/refined-values.html) adds structured input, paths, accumulation, and wire interpreters.
- JSON Schema publishes what the target can enforce, and documents the rest.

## Where to go next

Weighing this against DataAnnotations, FluentValidation, or Validus? Read
[How it compares](/how-it-compares/dataannotations-fluentvalidation-comparison.html).

Otherwise take [ConstraintDSL](/validating-values/dsl.html) for the full vocabulary and how to write a rule module,
then [Using constraints](/validating-values/overview.html) for composition and keeping the input, [Working with
violations](/validating-values/violations.html) for rendering and inspecting failures, [Interpreted and opaque](/validating-values/constraints.html)
for what makes a rule inspectable and what an escape hatch costs, and [Localization](/validating-values/localization/index.html) for
translating failures — with [Adding a language](/validating-values/adding-a-language.html) for the working order of a new
translation and [Fable support](/validating-values/fable.html) for the JavaScript target.
