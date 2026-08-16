# `.contract` as a generated, fail-closed wire projection

Status: undecided sketch, 2026-08-16. Not a resolution to record in `dev-docs/decisions/README.md` yet.

## The question this answers

`dev-docs/decisions/README.md` (2026-07-17) parked `.contract` as a second, hand-authored declaration
grammar alongside `[<DeriveSchema>]` records, with its removal gated on the config-system dogfood. Carrying two
authored declaration surfaces into 1.0 is a real cost: it doubles the front door and invites drift between them.

## The idea

Stop treating `.contract` as something a person writes. Instead:

- `[<DeriveSchema>]` records stay the one authoring surface, as already decided.
- `.contract`'s *shape* survives as a **generated projection** of any schema — compact, readable, JSON-Schema-like.
  Useful as a review artifact (diffing what a schema change actually does across a PR) and as the substrate for the
  already-noted Phase 30 "schema-as-data" work (`dev-docs/TASKS.md`), rather than new scope.
- It is a **wire format**, not a domain-authoring format, which matches the existing two-tier model (permissive wire
  tier, strict hand-written domain tier) instead of adding a third thing to reconcile.

## Round-tripping is fail-closed, not lossy-silent

If the projection is also read back (edited form re-ingested, or used to reconstruct a schema), any construct it
cannot faithfully represent — custom predicates, composed constraint atoms, whatever a record schema can express
that the grammar can't — must be a hard failure at generation or parse time, never a silent reinterpretation or
degradation.

This isn't a new rule; it's the same honesty already required of the trusted codec ("trusted structural codecs make
no constraint claim") and already rejected for contracts ("no automatic structural migrations, advisory
validation"). Applying it here keeps `.contract` from quietly becoming a second, weaker constraint language.

## What's still open

- Exactly which constructs are representable vs. rejected — needs enumerating against the current `Constraint`
  atom set, not assumed.
- Whether re-ingestion is a real requirement or the projection only ever flows one way (schema → review form). The
  original prompt for this was review/diffing, not editing; bidirectional is not yet a demonstrated need.
- Relationship to `schemagen`'s existing pipeline (AST → resolver → emitter) — whether generation reuses that
  machinery in reverse or is a new, smaller path off `Inspect`.

## Why this isn't in `decisions/README.md` yet

No consumer has forced the round-trip question, and the "keep `.contract`" call itself is still undecided rather
than settled. Move this into the decisions file (and delete this sketch) once the shape is chosen and something
uses it.
