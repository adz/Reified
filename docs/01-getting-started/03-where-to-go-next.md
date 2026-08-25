---
title: Where to go next
linkTitle: Where to go next
description: Choose the Reified capability that matches the next problem in your code.
weight: 3
type: docs
targetFramework: net8.0
---

# Where to go next

Start with Schema for a whole structured boundary. Otherwise, install the package that provides the API you need.

| Capability | Use it when |
| --- | --- |
| [Schema](/schema/index.html) | A form, request, configuration document, or stored payload must become a model with field-aware diagnostics. |
| [Constraints](/constraints/index.html) | A typed value must satisfy a reusable rule that also carries inspectable metadata and structured violations. |
| [Refined](/refined/index.html) | Successful admission should be recorded in a type that removes a later branch or makes an operation total. |
| [Parsing](/parsing/index.html) | Serialized primitive text must become an F# value without losing why conversion failed. |
| [Data](/data/index.html) | Structured fixtures and produced output need concise construction, editing, and comparison. |
| [Result handling](/result-handling/index.html) | Fallible functions need independent sequencing, recovery, or error accumulation over the standard F# `Result` type. |

Schema also drives the focused wire tools:

- [JSON Codecs](/schema/json-codecs.html) compiles a trusted serializer and deserializer from the declaration.
- [Derived Schemas](/schema/derivation/index.html) generates declarations from F# records during the build.
- [Versioned Contracts](/schema/versioned-contracts.html) migrates frozen wire shapes into the current model.

Install the complete runtime set with:

```sh
dotnet add package Reified
```


See [Packages and platforms](/notes/packages-and-platforms.html) for focused package names and target support.
