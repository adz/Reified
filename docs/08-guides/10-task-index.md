---
weight: 10
title: Task index
linkTitle: Task index
type: docs
description: I want to do X — which page do I read.
targetFramework: net8.0
---

# Task index

Route by the job, not by the package. Each row names the one canonical page for that task.

| I want to… | Read |
| --- | --- |
| Turn untrusted structured input (form, query, JSON body) into a model with path-aware errors | [Schema quickstart](/schema/quickstart.html) |
| Decide between `Schema.parse`, `Schema.check`, the JSON codec, and a versioned contract | [Choosing an API](/guides/choosing-an-api.html) |
| Put a reusable value rule on a field or value | [Constraints](/constraints/constraints.html) |
| Record "this value passed its rule" in the type itself | [Refined values](/refined/index.html) |
| Parse serialized primitive text (`"42"`, `"true"`, `"2026-06-01"`) without losing why it failed | [Parsing](/parsing/index.html) |
| Build, change, compare, and test structured data | [Data](/data/index.html) |
| Compose, recover, or accumulate errors over the standard F# `Result` | [Result handling](/result-handling/index.html) |
| Serialize and deserialize JSON for producers I trust | [JSON codecs](/schema/json-codecs.html) |
| Publish a JSON Schema document from a declaration | [JSON Schema API](/api/Reified.JsonSchema.html) |
| Describe a model to a form or admin UI | [Inspect API](/api/Reified.Inspect.html) |
| Generate the schema from a record's attributes at build time | [Derived schemas](/schema/derivation/index.html) |
| Keep reading old wire versions and migrate them to the current model | [Versioned contracts](/schema/versioned-contracts.html) |
| Run one declaration on .NET and Fable JavaScript | [Compiler-Directed, AOT, and Fable](/notes/aot-trimming-fable.html) |
| Know which package to install | [Packages and platforms](/notes/packages-and-platforms.html) |
| Map a failure type I am seeing to its meaning and conversion | [Error taxonomy and recipes](/guides/error-taxonomy.html) |
| Pin down what "trusted", "admission", "opaque", or "wire" mean | [Glossary](/guides/glossary.html) |

The [Getting started](/getting-started/index.html) walkthrough is the end-to-end version of the first row, and
[Where to go next](/getting-started/where-to-go-next.html) chooses by capability instead of by job.
