---
weight: 40
title: Glossary
linkTitle: Glossary
type: docs
description: Precise meanings for the terms Reified uses across packages and guides.
targetFramework: net8.0
---

# Glossary

- **admission** — the boundary operation that turns untrusted input into a value the rest of the application trusts.
  `Schema.parse` is admission for structured input; a refined type's constructor is admission for one value.

- **trusted / trusted path** — a producer whose output is assumed to have already passed admission, such as your own
  storage, an internal service, or a cache. The trusted path is the compiled JSON codec
  (`Json.deserialize`/`tryDeserialize`): it enforces wire shape and required fields but skips constraint metadata,
  because a trusted producer's values already passed those rules. Trust is a property of the producer and its
  admission history, not of a transport.

- **interpreted constraint** — a built-in constraint in the closed, inspectable algebra. Each interpreted constructor
  builds one `ConstraintAtom` and places that same value in both its description and any violation, so execution,
  export, and future proofing read one identity.

- **opaque constraint** — a constraint whose predicate Reified cannot inspect or export (`custom`, `customWith`,
  `notWith`, `contramap`). It runs normally and reports author-supplied prose, but documentation and export see only
  prose and `x-reified-runtime-constraints`, never claimed enforcement.

- **canonical** — the one representation a declaration or schema is defined in, the form every interpreter reads.
  Canonical field names are the boundary-facing names; canonical JSON Schema export lowers only what the target can
  enforce.

- **wire / wire shape** — the external, serialized form of a value: field names, nesting, and JSON types. The wire
  shape is what a JSON codec enforces; value rules such as `email` and `atLeast` are not part of the wire shape.

- **domain / domain model** — the types the application actually computes with, as opposed to a permissive wire DTO.
  A domain model often carries refined field types or a private constructor so its invariants cannot be bypassed.

- **boundary** — the point where untrusted input enters the application. Boundary supply (`mustSupply`/`mayOmit`) is
  decided before a typed value exists, so it is schema-owned, not a value constraint.

- **constraint** — a reusable description of valid values; `Constraint.check` is the operation that runs it. There is
  one public value-rule concept and no second constructor catalogue.

- **violation** — why a value failed its constraint. A diagnostic contract of plain comparable data; it carries no
  closure, no culture, no renderer, and no application context.

- **refinement** — a constraint plus a representation change, admitting a raw value into an invariant-carrying type
  whose constructor records the proof.

- **schema** — a declaration of how structured input becomes a model: fields, their value schemas, constraints,
  supply rules, and the constructor that assembles them.

- **codec** — a compiled JSON encoder/decoder pair built once from a schema and reused for every value.

- **contract** — a versioned chain of wire schemas with migrations, letting old payloads keep decoding into the
  current model.

- **retained input** — the original `Data` kept beside a parse result (`Schema.parseRetainingInput`), so a form can
  redisplay exactly what the user typed next to each error.

- **Data** — the source-neutral structured value (objects, lists, text, numbers, Booleans, null) that `Schema.parse`
  reads; untrusted text and query/form input become `Data` on the way in.
