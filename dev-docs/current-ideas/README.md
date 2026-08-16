# Current Ideas

This folder is for active design sketches and pre-ideas that are not yet accepted architecture.

Keep files here short enough for coding agents to scan quickly. When an idea is accepted, move the rule into
`AGENTS.md` or `dev-docs/PLAN.md` and delete the detailed sketch. When an idea is rejected or superseded, delete it
rather than keeping a historical spec that no longer matches the codebase.

Active sketches:

- `refined-schema-proof.md` — unresolved exploration of refined schema proof and safe record updates, with a prototype
  under `prototypes/refined-private-record/`.
- `architecture-guardrails.md` — proposed adopter-facing architecture guidance and a staged tooling direction for
  schema laws, compile-negative proofs, project roles, and compiled-code/public-surface audits.
- `docs-open-questions.md` — two questions left over from the docs information architecture work, now that the
  reorganisation itself is done: whether `Data` is a Foundation or Schema satellite package, and whether a plain
  ASP.NET serving path needs its own package.
- `format-and-json-runtime.md` — one package per representation format, and a
  shared schema-to-codec compiler over platform-specific .NET and Fable JSON runtimes.
- `database.md` — direction sketch for a typed relational layer: a generated immutable SQL AST, catalog-driven
  table metadata, reflection-free row codecs, and constraint violations translated into schema diagnostics.
  Constructing and mapping SQL only — executing it is explicitly out of scope.
- `schemagen-composes-constraints.md` — `SchemaGen` emits data violating a field's own constraints whenever more
  than one is attached and any of them picks a value. Proposes composing the atoms into one generator, filtering
  finite candidate sets at construction through `Constraint.test`, and reporting unsatisfiable combinations
  rather than sampling from them.
- `contract-as-wire-projection.md` — undecided answer to the parked `.contract` grammar question: keep it as a
  generated, fail-closed wire projection of any schema (review/diff artifact, JSON-Schema-like) rather than a
  second hand-authored declaration surface.

Implemented work and settled decisions do not remain in this folder. Constraint unification and contextual
constraint localization shipped and are recorded in `dev-docs/decisions/README.md` and `AGENTS.md`; the term
language, `FieldReference`, `Origin`, and `ConstraintExpression.Relational` were removed from that milestone
rather than deferred as placeholders, so they return only when a real consumer establishes field identity,
nesting, and proof semantics. Contract generation, versioning, and record-first
derivation outcomes are recorded in `dev-docs/decisions/README.md`; remaining consumer-gated schema work is in
`dev-docs/TASKS.md`. Retiring `Reified.ErrorHandling` and promoting Result to a top-level documentation area
is implemented, and its durable rules are in the decisions summary. The documentation tooling migration from
Hugo/Docsy to FsLiveDocs, and the `./docs` reorganisation into task folders it depended on, are both implemented
and recorded in the decisions summary, including the FsLiveDocs bug found and fixed along the way and the one
still open.

Flow sketches are not kept here. FlowStream proving, transport packages, and the application gate naming
proposal moved to the [Axial repository](https://github.com/adz/Axial) with Flow itself.
