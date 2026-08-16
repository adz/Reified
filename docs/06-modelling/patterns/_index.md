---
title: Recommended Patterns
weight: 70
description: Practical ways to keep untrusted input, trusted domain values, and legal updates separate.
targetFramework: net8.0
---

# Recommended patterns

These pages show how to use Schema with ordinary F# modules and projects. They focus on problems that appear as an
application grows: public records bypassing checks, wire DTOs entering business code, and updates breaking invariants.

Use only the strength of guarantee the code needs. A plain record is often right for a wire payload or edit form. A
private type is useful when many callers must rely on the same invariant without checking it again.

- [Build a private aggregate](/modelling/patterns/private-aggregates.html) — keep record syntax inside the owning module while callers see
  only safe construction and update functions.
- [Model legal transitions](/modelling/patterns/legal-transitions.html) — replace unrestricted record updates with named operations and typed
  refusals.
- [Separate wire and domain models](/modelling/patterns/wire-and-domain-models.html) — generate permissive wire schemas during the build,
  then admit them into hand-written domain types.
- [Split a larger application](/modelling/patterns/project-structure.html) — use project references to stop boundary and infrastructure
  types from reaching the domain.
- [Test schema guarantees](/testing/testing-schema-guarantees.html) — study the repository-only FsCheck adapter pattern for
  testing constructors, transitions, codecs, and migrations.

Start with [Construction Guarantees](/modelling/trusted-construction.html) if you are deciding whether a public record, refined
field, or private aggregate fits the model.
