---
title: Schema
linkTitle: Schema
description: Parse untrusted input through field constraints and domain constructors, or return path-aware diagnostics.
type: docs
weight: 2
targetFramework: net8.0
---

# Schema

Schema describes how untrusted boundary values become a model. If a field or constructor invariant fails, parsing
returns `SchemaErrors` and does not return the model.

The declaration is reusable data. Input parsing executes it; inspection, JSON Schema, codecs, versioned contracts,
and test-data generation interpret the same field names, value shapes, and constraints for their own jobs.

<div class="schema-overview-graphic">
<svg viewBox="0 0 680 300" role="img" xmlns="http://www.w3.org/2000/svg" style="max-width:100%;height:auto">
  <title>Reified.Schema: one declaration drives everything below</title>
  <desc>A single field declaration fans downward into JSON codecs, parsers, contracts, docs and forms, and tests.</desc>
  <g font-size="13">
    <text x="20" y="24" fill="currentColor" opacity="0.55">One declaration drives all of it</text>
    <rect x="240" y="50" width="200" height="100" rx="8" fill="none" stroke="#0b55d9" stroke-width="1.25"/>
    <g fill="#0b55d9" font-family="var(--font-mono, monospace)" font-size="12">
      <text x="258" y="78">type Order =</text>
      <text x="258" y="98">  id : OrderId</text>
      <text x="258" y="118">  items : Item list</text>
      <text x="258" y="138">  total : Money</text>
    </g>
    <g stroke="currentColor" stroke-opacity="0.35">
      <line x1="340" y1="150" x2="76" y2="216"/>
      <line x1="340" y1="150" x2="198" y2="216"/>
      <line x1="340" y1="150" x2="320" y2="216"/>
      <line x1="340" y1="150" x2="442" y2="216"/>
      <line x1="340" y1="150" x2="564" y2="216"/>
    </g>
    <g fill="none" stroke="currentColor" stroke-opacity="0.4">
      <rect x="20" y="216" width="112" height="30" rx="6"/>
      <rect x="142" y="216" width="112" height="30" rx="6"/>
      <rect x="264" y="216" width="112" height="30" rx="6"/>
      <rect x="386" y="216" width="112" height="30" rx="6"/>
      <rect x="508" y="216" width="112" height="30" rx="6"/>
    </g>
    <g fill="currentColor" text-anchor="middle" font-family="var(--font-mono, monospace)" font-size="11">
      <text x="76" y="235">JSON codecs</text>
      <text x="198" y="235">Parsers</text>
      <text x="320" y="235">Contracts</text>
      <text x="442" y="235">Docs, forms</text>
      <text x="564" y="235">Tests</text>
    </g>
  </g>
</svg>
</div>

## Start here

The [Schema quickstart](/schema/quickstart.html) declares one record schema, parses structured input,
reports every field failure with its path, and compiles the same declaration into a JSON codec.

After the quickstart, use these guides as needed:

- [Schema DSL](/schema/dsl.html) — fields, constraints, constructors, and nested schemas.
- [Input Sources](/schema/input-sources.html) — name/value input, JSON-like data, CLI values, and configuration.
- [Construction Guarantees](/schema/trusted-construction.html) — what a schema proves and when a private type is needed.
- [Union Schemas](/schema/union-schemas.html) — the recommended tagged-union format.
- [JSON Codecs](/schema/json-codecs.html) — trusted serialization and deserialization from the same declaration.
- [Derived Schemas](/schema/derivation/index.html) — generate schema declarations during the build.
- [Versioned Contracts](/schema/versioned-contracts.html) — migrate frozen wire shapes into the current model.

Use the [API reference for `Schema`](/api/Reified.Schema.html) when you need the complete constructor and interpreter
catalogue rather than a guided workflow.

Schema controls values produced through Schema. A public F# record can still be constructed directly. Use refined
fields, a private aggregate, or an opaque `.fsi` interface when other code must rely on the invariant without checking
it again; [Construction Guarantees](/schema/trusted-construction.html) compares those choices.
