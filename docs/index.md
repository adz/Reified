---
title: Reified
description: One F# model for trusted values and structured boundaries, on .NET and Fable JavaScript.
body_class: reified-home
targetFramework: net8.0
---

<div class="docs-home-container reified-landing">

<div class="docs-home-hero">

<div class="docs-home-hero-visual">
<img class="hero-lockup" data-theme-variant="light" src="content/img/reified-logo-light.svg" alt="Reified" width="226" height="64" />
<img class="hero-lockup" data-theme-variant="dark" style="display: none;" src="content/img/reified-logo-dark.svg" alt="Reified" width="226" height="64" />
</div>

<div class="docs-home-copy" style="max-width: 85ch; margin: 0 auto;">

<span class="eyebrow">One F# model for .NET and Fable JavaScript</span>
<h1>Encode invariants once.<br/>
Enforce project wide.</h1>

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

<div class="docs-home-example" style="max-width: 78ch; margin: 0 auto 2rem;">

```fsharp no-check reason="The homepage excerpt shares signupSchema from the preceding example; the complete program is verified in examples/Reified.GettingStarted."
JsonSchema.generate signupSchema
// {"type":"object",
//  "properties":{"email":{"type":"string"},
//                "age":{"type":"integer","minimum":13},
//                "newsletter":{"type":"boolean"}},
//  "required":["email","age","newsletter"]}
```

</div>

<p style="max-width: 78ch; margin: 0 auto 0.5rem; text-align: center;">Every failure message, the JSON codec, the
JSON Schema, and the generated test data come from that one declaration. Nothing above is written twice.</p>

<p style="text-align: center; margin-bottom: 2rem;">
<a class="btn btn-primary" href="getting-started/index.html">Get started &rarr;</a>
</p>

<div class="docs-home-copy docs-home-package-map" style="max-width: 78ch; margin: 0 auto;">

<h2>How the packages fit</h2>

<div class="package-map">

```text
Constraint ─┐
Parse ──────┤
Data ───────┼──> Schema ───> Schema.Json
Refinements ┘             └──> contract tooling
```

</div>

<p>Constraint, Parse, Data, and Refinements can each be used alone without Schema, but they are designed to work
together consistently.</p>

</div>

<h2 class="docs-home-section-title">Choose your starting point</h2>

<div class="docs-home-routes">

- **Building a structured boundary?** Start with [Schema](/schema/index.html).
- **Only need one piece?** Choose [Constraints](/constraints/index.html),
  [Refined](/refined/index.html), [Parsing](/parsing/index.html), or [Data](/data/index.html).
- **Composing ordinary F# failures?** Use [Result handling](/result-handling/index.html) independently.

</div>

<p style="max-width: 78ch; margin: 2rem auto; text-align: center;">Install one focused package or the complete runtime
set. <a href="notes/packages-and-platforms.html">Packages and platforms &rarr;</a></p>

<div class="docs-home-meta" style="margin-bottom: 4rem;">
<a class="docs-chip" href="getting-started/index.html">Getting started</a>
<a class="docs-chip" href="https://github.com/adz/Reified">GitHub</a>
</div>

</div>
