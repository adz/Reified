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
<p>Declare a rule once, on a value, a field, or a whole model, and the checking, the diagnostics, the JSON codec, the contract document, and the test data are all read from that one declaration. No runtime reflection: the same declaration compiles under NativeAOT, trimming, and Fable.</p>
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

let codec = Json.compile signupSchema     // compile once, typically at startup
Json.serialize codec signup
// {"email":"ada@example.org","age":36,"newsletter":true}
```


</div>

<p style="max-width: 78ch; margin: 0 auto 1rem; text-align: center;">The compiled codec is competitive with
<code>System.Text.Json</code> on serialization and ahead of it on decoding: on canonical UTF-8 input, Reified's
decoder ran about 14% faster on .NET 10 in the recorded <a href="notes/benchmarks.html">benchmark</a>, and
allocated less, because a schema-derived codec skips per-call reflection and boxing.</p>

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

<p style="text-align: center; margin-bottom: 1rem;">
<a class="btn btn-primary" href="getting-started/index.html">Get started &rarr;</a>
</p>

<div class="docs-home-meta" style="margin-bottom: 2rem;">
<a class="docs-chip" href="schema/json-codecs.html">JSON Codecs</a>
<a class="docs-chip" href="schema/index.html">Schema</a>
<a class="docs-chip" href="refined/index.html">Refined</a>
<a class="docs-chip" href="parsing/index.html">Parsing</a>
<a class="docs-chip" href="result-handling/index.html">Result handling</a>
<a class="docs-chip" href="data/index.html">Data</a>
</div>

<div class="docs-home-routes">

- **JSON without reflection.** A schema compiles to a JSON codec that runs under NativeAOT and trimming, and
   the same schema compiles to Fable JavaScript. See [JSON Codecs](/schema/json-codecs.html) and
   [AOT, trimming, and Fable](/notes/aot-trimming-fable.html).
- **A schema you can inspect, not just run.** One declaration produces field-aware parse diagnostics, JSON
   Schema export, and messages a translator can localize without touching the rule. See
   [Schema](/schema/index.html), [Redisplay and Field Errors](/schema/redisplay-and-field-errors.html), and
   [Localization](/constraints/localization/index.html).
- **Refined types.** A value's invariant is proven once, at construction, and every later use relies on the
   proof instead of re-checking it. See [Refined values](/refined/index.html).
- **Parse and Result.** Untrusted text becomes a typed value with a reason attached to failure, and ordinary F#
   `Result` composes without an exception model underneath it. See [Parsing](/parsing/index.html) and
   [Result handling](/result-handling/index.html).
- **Data.** `Schema.parse` reads from it, not from JSON, a form post, or a query string directly, so it is the
   one boundary-input shape untrusted data becomes on the way in, as well as a source-neutral value for building
   fixtures and comparing output. The trusted `Json.compile` codec path bypasses it entirely, string or bytes
   straight to your model, so it is required for the boundary path and optional for the trusted one, not optional
   everywhere. See [Data](/data/index.html).

</div>

<p style="max-width: 78ch; margin: 2rem auto; text-align: center;">Writing a schema for a payload you receive
from somewhere else, or one with old versions still in storage? See
<a href="schema/schema-or-generated-dto.html">Schema or Generated DTO</a> before you start: generation and a
hand-written schema solve different problems, and combining them wrong is the most common way to end up with
validation that silently does not run.</p>

<p style="text-align: center; margin-bottom: 1rem;">
<a class="btn btn-primary" href="getting-started/index.html">Get started &rarr;</a>
</p>

<p style="max-width: 78ch; margin: 0 auto 2rem; text-align: center;"><a href="notes/packages-and-platforms.html">Packages and platforms &rarr;</a></p>

<div class="docs-home-meta" style="margin-bottom: 4rem;">
<a class="docs-chip" href="getting-started/index.html">Getting started</a>
<a class="docs-chip" href="https://github.com/adz/Reified">GitHub</a>
</div>

</div>
