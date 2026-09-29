# Reified.Data follow-on phases

## Status

Phase 1 (one expressive data language: literals, edits, variations, matrices, paths, diffs, patterns,
selective proofs, rendering) shipped and is `Reified.Data` as it exists today. Its rationale, the full
phase-1 design (recursive tree shape, what was deliberately left out, and why the package stands alone) is
settled architecture, not a live sketch; it isn't repeated here. See `Reified.Data`'s own docs
(`docs/06-data/`) for the shipped surface, and `dev-docs/decisions/README.md` for the accepted-decision
record.

This file keeps only what's still speculative: phases 2 and later, gated on a demonstrated consumer, not
committed to ship.

# Follow-on phases

Later phases begin only after Phase 1 proves that the base language is coherent. Each phase should be justified by a demonstrated consumer and may be delivered as an adapter rather than core API.

## Phase 2: richer patterns and reusable selectors

Extend the pattern language where real tests require more than Phase 1:

- optional and explicitly absent object fields inside recursive patterns
- regex text patterns
- numeric tolerance with explicit lexical-to-numeric conversion rules
- negation with positive, understandable diagnostics
- exact list, prefix, suffix, unordered multiset, and index-specific patterns
- reusable compiled `DataPath` values
- wildcard selectors such as `items[*].id`

Strict paths and multi-result selectors remain distinct. A selector is not accepted by an operation that requires exactly one target.

Do not grow selectors into JSONPath or JSONata by accident. Filters, recursive descent, projections, grouping, and aggregation require a separate language proposal.

## Phase 3: snapshots, golden files, and redaction

Add deterministic approval testing over the Phase 1 renderer and diff engine:

```fsharp
response
|> redact [
    replace "id" "<id>"
    replace "createdAt" "<timestamp>"
]
|> Snapshot.matchFile "approved/customer-response.json"
```

Core may own pure redaction or replacement transforms. A testing package owns snapshot paths, update modes, approval policy, source locations, and assertion exceptions.

Runner adapters may integrate with xUnit, Expecto, or other frameworks. Environment-variable and filesystem effects must remain explicit at the snapshot boundary.

## Phase 4: captures and relational proofs

Support tests that must reuse a produced value or prove relationships between locations:

```fsharp
let proof =
    proof {
        let! customerId = captureText "customer.id"
        do! equalAt "audit.customerId" customerId
    }
```

Captures require deterministic branch selection, conflict rules for repeated names, and clear behavior inside alternatives and unordered collections.

Ordinary F# bindings remain preferable when they are equally clear. Do not turn `Reified.Data` into a general test computation framework.

## Phase 5: property generation and shrinking

An FsCheck adapter may generate arbitrary owned trees, malformed boundary values, and stable shrunk counterexamples:

```fsharp
DataGen.any
DataGen.withMaximumDepth 5
DataGen.jsonCompatible
```

Generic `Data` generation belongs in an `Reified.Data` testing adapter. Schema-conforming generation remains in `Reified.Schema.Testing`.

The core package must not acquire an FsCheck dependency.

## Phase 6: contracts, examples, recording, and replay

Use `Data` as the common body representation for:

- HTTP request and response examples
- message-bus events and webhooks
- Pact-style consumer expectations
- database document fixtures
- recorded external responses
- schema and OpenAPI examples

Media types, headers, status codes, compatibility policy, and replay I/O remain in HTTP, Schema, or dedicated testing packages.

This phase should reuse structural patterns and differences. It must not introduce a second recursive value or matcher representation.

## Phase 7: standard patch interchange and overlays

Add adapters for RFC 6902 JSON Patch only when interoperability requires them:

```fsharp
DataEdit.ofJsonPatch
DataEdit.toJsonPatch
```

Standard `add`, `remove`, `replace`, `move`, `copy`, and `test` operations do not automatically replace the ergonomic edit vocabulary. Each adapter must account for path and duplicate-field differences.

If object overlay is demonstrated, name its duplicate policy explicitly, such as `overlayLast` or `overlayAll`. Do not add an ambiguous `merge` operation.

JSON Merge Patch is a separate interchange format with different null and omission semantics. It requires its own adapter and must not be inferred from `patch`.

## Phase 8: query and transformation language

Some consumers may eventually need filtering, projection, grouping, aggregation, or construction of new result shapes.

That capability is comparable to JSONPath, JSONata, or `jq`; it is not a small extension to strict paths.

Require a separate design note before adding it. It must define evaluation cardinality, ordering, failure behavior, resource limits, portability, and whether expressions are serializable.

# Continuing exclusions

- No `Data<'scalar>` in the initial public interface.
- No separate .NET and JavaScript recursive trees.
- No `obj`-valued scalar case.
- No reflection-based primitive conversion.
- No implicit `None` to `Null` conversion.
- No automatic creation of missing patch containers.
- No filling list holes with `Null`.
- No implicit normalization of `Number "1"`, `Number "1.0"`, and `Number "1e0"`.
- No vague `contains` or `matches` operation with configurable hidden semantics.
- No general-purpose lenses in the primary authoring surface.
- No object computation expression alongside list literal syntax.
- No snapshot filesystem policy in the representation package.
- No schema validation, transport contracts, or property-test dependency in the core package.

If native typed dynamic scalars become necessary, revisit a generic internal tree plus explicit scalar vocabularies. Do not add parallel recursive unions.

# Documentation consequences

This design gives `Reified.Data` a standalone role and justifies its independent NuGet package.

It remains a dependency of Schema, HTTP input adapters, and contract tooling, while becoming directly useful for fixtures, produced-data proofs, diffs, and bulk variation.

`Reified.Data` does not require a top-level product navigation item. Introduce it under structured Schema input and test authoring, with a focused package and reference page.

When Phase 1 becomes active architecture, teach the end-to-end workflow before listing individual operations. The documentation should show one value moving through construction, variation, transport, and proof.
