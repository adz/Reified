---
title: Reified
description: F# libraries for parsing untrusted input into domain models, and for composing failures, on .NET and Fable JavaScript.
body_class: reified-home
targetFramework: net8.0
---

<div class="docs-home-container reified-landing">

<div class="docs-home-hero">

<div class="docs-home-hero-visual">
<img class="hero-lockup" data-theme-variant="light" src="content/img/reified-logo-light.svg" alt="Reified" width="226" height="64" />
<img class="hero-lockup" data-theme-variant="dark" style="display: none;" src="content/img/reified-logo-dark.svg" alt="Reified" width="226" height="64" />
</div>

<div class="docs-home-copy" style="max-width: 78ch; margin: 0 auto;">

<span class="eyebrow">F# libraries for .NET and Fable JavaScript</span>
<h1>Encode each invariant once. Enforce it across the project.</h1>

<div class="lede">
<p>Declare a rule once — on a value, a field, or a whole model — and the checking, the diagnostics, the JSON codec, the contract document, and the test data are all read from that one declaration.</p>
</div>

</div>

</div>

<div class="docs-home-example" style="max-width: 78ch; margin: 0 auto 2rem;">

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
type Signup = { Email: string; Age: int; Newsletter: bool }

let signupSchema =
    schema<Signup> {
        field _.Email { constraints [ present; email ] }
        field _.Age { constrain (atLeast 13) }
        field _.Newsletter
        construct (fun email age newsletter ->
            { Email = email; Age = age; Newsletter = newsletter })
    }

Schema.parse signupSchema input
// age: Expected a value at least 13, but was 11.
// email: Expected an email address, but was ada.
// newsletter: This value was omitted.

Json.serialize (Json.compile signupSchema) signup
// {"email":"ada@example.org","age":36,"newsletter":true}
```

</div>

<p style="max-width: 78ch; margin: 0 auto 0.5rem; text-align: center;">Every failure message, the JSON codec, the
JSON Schema, and the generated test data come from that one declaration. Nothing above is written twice.</p>

<p style="text-align: center; margin-bottom: 2rem;">
<a class="btn btn-primary" href="getting-started/index.html">Get started &rarr;</a>
</p>

</div>

## Route by symptom

| Problem | Goes to |
| --- | --- |
| Validation boilerplate is everywhere, and invalid values still get through | [Validating values](/validating-values/index.html) |
| The same rule is repeated in a parser, a validator, a form, and a test | [Modelling](/modelling/index.html) |
| Decoding and validation are separate steps that drift apart | [JSON](/json/index.html) |
| Client and server disagree about the shape of a request | [HTTP contracts](/http-contracts/index.html) |
| Constructing test data by hand is slow and repetitive | [Testing](/testing/index.html) |
| You want one small library, not a framework | [Packages and platforms](/notes/packages-and-platforms.html) |

<p style="max-width: 78ch; margin: 2rem auto; text-align: center;">Every package is independently installable
and runs on .NET and on Fable JavaScript — take one capability or the whole set.
<a href="notes/packages-and-platforms.html">Packages and platforms &rarr;</a></p>

<div class="docs-home-meta" style="margin-bottom: 4rem;">
<a class="docs-chip" href="getting-started/index.html">Getting started</a>
<a class="docs-chip" href="https://github.com/adz/Reified">GitHub</a>
</div>
