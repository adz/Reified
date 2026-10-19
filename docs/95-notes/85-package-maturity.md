---
weight: 85
title: Package maturity
linkTitle: Package maturity
description: How stable each Reified package is, what the ratings mean, and which application justifies each one.
targetFramework: net8.0
---

# Package maturity

Each package carries a maturity rating: **Experimental**, **Incubating**, or **Stable**. The rating is declared in
the package's project file as `<PackageMaturity>` and derived into the published metadata — a `maturity-<level>`
NuGet tag and a `[Level]` prefix on the package description. The table here is the same rating users see on
NuGet; a test keeps the two in sync.

## Levels

**Experimental** — no real application uses the package. It may change or be removed in any release.

**Incubating** — at least one real application uses it, directly or through a package that application references,
and it is pre-1.0 or used by fewer than two real applications. The API may still change in minor releases.

**Stable** — version 1.0 or later **and** used by at least two real applications. Semantic versioning and
deprecation cycles apply.

## Promotion rule

A package moves up one level at a time, when it meets the next level's bar:

- *Experimental → Incubating*: a real application starts using it.
- *Incubating → Stable*: the package reaches 1.0 **and** a second real application uses it. Until both are true it
  stays Incubating, however long it has been in use.

No package is Stable today: every Reified package is pre-1.0.

## What counts as a real application

A *real application* is an application in active use outside this repository. The current set is **gitkay** and
**Logs Digger**. Examples, samples, tests, and reference apps in this repository do not count, and neither does
another package — an application must sit above the package graph.

## Ratings

| Package | Maturity | Evidence |
| --- | --- | --- |
| `Reified` | Incubating | Used by gitkay |
| `Reified.Schema` | Incubating | Used by Logs Digger |
| `Reified.Data` | Incubating | Used through `Reified` and `Reified.Schema` |
| `Reified.Constraint` | Incubating | Used through `Reified` and `Reified.Schema` |
| `Reified.Parse` | Incubating | Used through `Reified` and `Reified.Schema` |
| `Reified.Refinements` | Incubating | Used through `Reified` and `Reified.Schema` |
| `Reified.Result` | Incubating | Used through `Reified` |
| `Reified.Schema.Http` | Incubating | Used through `Reified` |
| `Reified.SchemaGen.MSBuild` | Experimental | No application uses it yet |

`Reified` is the umbrella package gitkay references; `Reified.Schema` is the package Logs Digger references. Every
runtime package those two reference transitively inherits Incubating, because an application using the umbrella or
the Schema package uses those packages through it. `Reified.SchemaGen.MSBuild` is referenced by no application, so
it stays Experimental even though both applications could generate schemas with it.

The non-packable repository tooling — `Reified.SchemaGen` and `Reified.Schema.Testing` — has no rating because it
is never published.
