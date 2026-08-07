---
title: Reified
description: F# libraries for parsing untrusted input into domain models, and for composing failures, on .NET and Fable JavaScript.
body_class: reified-home
---

<div class="docs-home-container reified-landing">

<div class="docs-home-hero">

<div class="docs-home-hero-visual">
<img class="hero-lockup hero-lockup--light" src="/content/img/reified-logo-light.svg" alt="Reified" width="452" height="128" />
<img class="hero-lockup hero-lockup--dark" src="/content/img/reified-logo-dark.svg" alt="Reified" width="452" height="128" />
</div>

<div class="docs-home-copy" style="max-width: 78ch; margin: 0 auto;">

<span class="eyebrow">F# libraries for .NET and Fable JavaScript</span>
<h1>Encode each invariant once. Enforce it across the project.</h1>

<script>
(function () {
  var taglines = [
    "If it compiles, the invariant already held.",
    "Types that prove it, not comments that promise it.",
    "Stop trusting that the check ran. Make the type carry it.",
    "Untrusted in, proven out.",
    "Untrusted data stops at the boundary, not three layers in.",
    "Make illegal states unrepresentable — for values and for boundaries."
  ];
  var el = document.getElementById("reified-tagline");
  if (el) {
    el.textContent = taglines[Math.floor(Math.random() * taglines.length)];
  }
})();
</script>

<div class="lede">
<p>Declare a rule once — on a value, a field, or a whole model — and the checking, the diagnostics, the JSON codec, the contract document, and the test data are all read from that one declaration.</p>
</div>

<p id="reified-tagline" class="reified-tagline">If it compiles, the invariant already held.</p>
</div>

</div>

<div class="docs-home-example" style="max-width: 78ch; margin: 0 auto 2rem;">

```fsharp
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

<div class="docs-home-cta-row" style="justify-content: center; margin-bottom: 2rem;">
<a class="docs-home-cta" href="/getting-started/">Get started &rarr;</a>
</div>

<p style="max-width: 78ch; margin: 0 auto 3.5rem; text-align: center;">There are two ways in. Declare a
<a href="/schema/getting-started/">Schema</a> when structured input has to become a domain model with every field
failure reported at once. Use plain <a href="/result/">Result</a> with your own error type when the code is small and
the failures are yours. Everything else below is machinery those two use.</p>

<div class="reified-routes">

<a class="reified-route" href="/schema/getting-started/">
<span class="reified-route-problem">The same rule is repeated in a parser, a validator, a form, and a test</span>
<span class="reified-route-target">Schema &rarr;</span>
</a>

<a class="reified-route" href="/result/">
<span class="reified-route-problem">Failures are exceptions, or a bespoke result type in every project</span>
<span class="reified-route-target">Result &rarr;</span>
</a>

<a class="reified-route" href="/values/constraint/">
<span class="reified-route-problem">Validation boilerplate is everywhere, and invalid values still get through</span>
<span class="reified-route-target">Constraint &rarr;</span>
</a>

<a class="reified-route" href="/values/refined/">
<span class="reified-route-problem">A type says <code>string</code> when it means something narrower</span>
<span class="reified-route-target">Refined values &rarr;</span>
</a>

<a class="reified-route" href="/schema/json-codec/">
<span class="reified-route-problem">Decoding and validation are separate steps that drift apart</span>
<span class="reified-route-target">JSON codecs &rarr;</span>
</a>

<a class="reified-route" href="/schema/http-servers/">
<span class="reified-route-problem">Client and server disagree about the shape of a request</span>
<span class="reified-route-target">HTTP contracts &rarr;</span>
</a>

<a class="reified-route" href="/data/">
<span class="reified-route-problem">Constructing test data by hand is slow and repetitive</span>
<span class="reified-route-target">Data &rarr;</span>
</a>

<a class="reified-route" href="/getting-started/#installing">
<span class="reified-route-problem">You want one small library, not a framework</span>
<span class="reified-route-target">Packages &rarr;</span>
</a>

</div>

<p style="max-width: 78ch; margin: 0 auto 2rem;">Every package is independently installable, and runs on .NET and
on Fable JavaScript.</p>

<div class="docs-home-meta" style="margin-bottom: 4rem;">
<a class="docs-chip" href="/getting-started/">Getting started</a>
<a class="docs-chip" href="https://github.com/adz/Reified">GitHub</a>
</div>

</div>
