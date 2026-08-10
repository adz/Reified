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

<div class="reified-group" style="margin-top: 3.5rem;">
<h2 class="reified-group-title">Rules that live in values</h2>
<p class="reified-group-lede">A rule written as a value can be executed by a checker, explained by a renderer,
exported to JSON Schema, and sampled by a generator. Write it once and the message, the document, and the test
data follow from it — and a type built through the rule never has to be checked again.</p>

<div class="reified-routes">

<a class="reified-route" href="/values/constraint/">
<span class="reified-route-problem">Reusable rules that carry their own explanation, in any language</span>
<span class="reified-route-target">Constraint &rarr;</span>
</a>

<a class="reified-route" href="/values/refined/">
<span class="reified-route-problem">Types that can only hold values the rule admits — <code>NonBlankString</code>, your own <code>CustomerId</code></span>
<span class="reified-route-target">Refined values &rarr;</span>
</a>

<a class="reified-route" href="/values/parse/">
<span class="reified-route-problem">Serialized primitives to typed values, with the failure as data</span>
<span class="reified-route-target">Parse &rarr;</span>
</a>

</div>
</div>

<div class="reified-group">
<h2 class="reified-group-title">Boundaries that produce models</h2>
<p class="reified-group-lede">Declare the model once and it does the whole job: untrusted input becomes the
model — or a set of failures with paths — and the same declaration is also the JSON codec, the published
contract, and the field metadata a form renders. Nothing downstream wonders whether validation ran.</p>

<div class="reified-routes">

<a class="reified-route" href="/schema/quickstart/">
<span class="reified-route-problem">One declaration per model, read by every layer that would otherwise repeat it</span>
<span class="reified-route-target">Schema &rarr;</span>
</a>

<a class="reified-route" href="/what-a-schema-gives-you/">
<span class="reified-route-problem">The seven jobs around a model that one declaration already does</span>
<span class="reified-route-target">What a schema gives you &rarr;</span>
</a>

<a class="reified-route" href="/schema/json-codec/">
<span class="reified-route-problem">Reflection-free JSON, so decoding and validation cannot drift apart</span>
<span class="reified-route-target">JSON codecs &rarr;</span>
</a>

<a class="reified-route" href="/schema/http-servers/">
<span class="reified-route-problem">Endpoints whose OpenAPI is generated from the same declaration they enforce</span>
<span class="reified-route-target">HTTP contracts &rarr;</span>
</a>

<a class="reified-route" href="/schema/contracts/">
<span class="reified-route-problem">Old payload versions migrated into the current model, on purpose</span>
<span class="reified-route-target">Contracts &rarr;</span>
</a>

</div>
</div>

<div class="reified-group">
<h2 class="reified-group-title">Failures and fixtures as ordinary values</h2>
<p class="reified-group-lede">No exception model and no framework result type. Errors are data you can match
on, group by path, translate, or serialize — composed over the standard <code>Result</code> rather than
replacing it. Test data comes from the same declarations, so fixtures cannot drift from the rules.</p>

<div class="reified-routes">

<a class="reified-route" href="/result/">
<span class="reified-route-problem">Fail-fast sequencing and error accumulation over your own error type</span>
<span class="reified-route-target">Result &rarr;</span>
</a>

<a class="reified-route" href="/data/">
<span class="reified-route-problem">Building, editing, and comparing test data without hand-writing every case</span>
<span class="reified-route-target">Data &rarr;</span>
</a>

</div>
</div>

<p style="max-width: 78ch; margin: 0 auto 2rem; text-align: center;">Every package is independently installable
and runs on .NET and on Fable JavaScript — take one capability or the whole set.
<a href="/notes/packages-and-platforms/">Packages and platforms &rarr;</a></p>

<div class="docs-home-meta" style="margin-bottom: 4rem;">
<a class="docs-chip" href="/getting-started/">Getting started</a>
<a class="docs-chip" href="https://github.com/adz/Reified">GitHub</a>
</div>

</div>
