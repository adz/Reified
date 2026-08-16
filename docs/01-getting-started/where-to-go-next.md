---
title: Where to go next
linkTitle: Where to go next
description: The whole library in three groups — rules that live in values, boundaries that produce models, and failures and fixtures as ordinary values.
weight: 3
type: docs
targetFramework: net8.0
---

Everything in Reified falls into three groups. Each package is independently installable and runs on .NET and
on Fable JavaScript, so you can take one capability without the rest.

## Rules that live in values

A rule written as a value can be executed by a checker, explained by a renderer, exported to JSON Schema, and
sampled by a generator. Write it once and the message, the document, and the test data follow from it — and a
type built through the rule never has to be checked again.

| | |
| --- | --- |
| [Constraint](/validating-values/constraint.html) | Reusable rules that carry their own explanation, in any language |
| [Refined values](/domain-types/index.html) | Types that can only hold values the rule admits — `NonBlankString`, your own `CustomerId` |
| [Parse](/parsing-input/index.html) | Serialized primitives to typed values, with the failure as data |

## Boundaries that produce models

Declare the model once and it does the whole job: untrusted input becomes the model — or a set of failures
with paths — and the same declaration is also the JSON codec, the published contract, and the field metadata a
form renders. Nothing downstream wonders whether validation ran.

| | |
| --- | --- |
| [Schema](/modelling/quickstart.html) | One declaration per model, read by every layer that would otherwise repeat it |
| [What a schema gives you](/getting-started/what-a-schema-gives-you.html) | The seven jobs around a model that one declaration already does |
| [JSON codecs](/json/index.html) | Reflection-free JSON, so decoding and validation cannot drift apart |
| [HTTP contracts](/http-contracts/index.html) | Endpoints whose OpenAPI is generated from the same declaration they enforce |
| [Contracts](/http-contracts/contracts.html) | Old payload versions migrated into the current model, on purpose |

## Failures and fixtures as ordinary values

No exception model and no framework result type. Errors are data you can match on, group by path, translate,
or serialize — composed over the standard `Result` rather than replacing it. Test data comes from the same
declarations, so fixtures cannot drift from the rules.

| | |
| --- | --- |
| [Result](/validating-values/result/index.html) | Fail-fast sequencing and error accumulation over your own error type |
| [Data](/testing/index.html) | Building, editing, and comparing test data without hand-writing every case |

## Installing

```sh
dotnet add package Reified
```

That is the whole set. Every package is also installable on its own — see
[Packages and platforms](/notes/packages-and-platforms.html) for the list, what each one gives you, and which run
on Fable as well as .NET.
