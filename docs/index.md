---
title: Reified
description: Independent Data, Result, Values, and Schema libraries for F# on .NET and Fable JavaScript.
body_class: reified-home
---

<div class="docs-home-container reified-landing">

<div class="docs-home-hero">

<div class="docs-home-hero-visual">
<img class="hero-lockup hero-lockup--light" src="/content/img/reified-logo-light.png" alt="Reified" width="1159" height="332" />
<img class="hero-lockup hero-lockup--dark" src="/content/img/reified-logo-dark.png" alt="Reified" width="1159" height="332" />
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
    "Make illegal states unrepresentable — for values, boundaries, and effects."
  ];
  var el = document.getElementById("reified-tagline");
  if (el) {
    el.textContent = taglines[Math.floor(Math.random() * taglines.length)];
  }
})();
</script>

<div class="lede">
<p>Reified's goal is to replace repetitive, error-prone code with APIs that are ergonomic for humans and predictable for LLMs, reducing the context both need to produce reliable software.</p>
</div>
<p>F# already provides strong foundations: discriminated unions and records for modelling, immutability by default, and explicit handling of missing values. We build on those foundations to enforce project-wide rules across values, boundaries, and models.</p>

<p id="reified-tagline" class="reified-tagline">If it compiles, the invariant already held.</p>
</div>

</div>

<div class="reified-doors">

<a class="reified-door reified-door--data" href="{{< relref "/data/" >}}">
<span class="reified-door-kicker">Structured values and fixtures</span>
<h2>Reified.Data</h2>
<p>Build, edit, compare, and match portable structured values without repetitive constructors or copied fixtures.</p>
<span class="reified-door-cta">Data documentation &rarr;</span>
</a>

<a class="reified-door reified-door--result" href="{{< relref "/result/" >}}">
<span class="reified-door-kicker">Typed failures</span>
<h2>Reified.Result</h2>
<p>Compose operations that can fail over the standard F# Result type, with <code>result { }</code> for
fail-fast sequencing and accumulating builders for collecting every error at once.</p>
<span class="reified-door-cta">Result documentation &rarr;</span>
</a>

<a class="reified-door reified-door--values" href="{{< relref "/values/" >}}">
<span class="reified-door-kicker">Admitting values</span>
<h2>Values</h2>
<p>Three independently installable packages — Constraint, Refinements, and Parse. Reuse value checks, parse
serialized primitives, and construct refined values, so an invalid value can't reach your domain types in
the first place.</p>
<span class="reified-door-cta">Values documentation &rarr;</span>
</a>

<a class="reified-door reified-door--parse" href="{{< relref "/schema/" >}}">
<span class="reified-door-kicker">Input and domain values</span>
<h2>Reified.Schema</h2>
<p>Declare how structured input becomes a model once, and get JSON codecs, contracts, and validation from that single
definition, so parsers, docs, forms, and tests can't drift out of sync with each other.</p>
<span class="reified-door-cta">Schema documentation &rarr;</span>
</a>

</div>

<p style="max-width: 78ch; margin: 0 auto 2rem;">Install <code>Reified</code> for all of the above at once, or a
focused package when you need one capability. Effects and execution are not here: <a
href="https://github.com/adz/Axial">Axial</a> describes async work with its dependencies and failures in the
signature, and its optional server adapters execute Reified HTTP contracts.</p>

<div class="docs-home-meta" style="margin-bottom: 4rem;">
<a class="docs-chip" href="https://github.com/adz/Reified">GitHub</a>
</div>

</div>
