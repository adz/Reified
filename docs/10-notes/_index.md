---
weight: 90
title: Notes
linkTitle: Notes
type: docs
notoc: true
description: Package inventory, platform support, measured numbers, and the compiler-directed design — reference material rather than a learning path.
menu:
  main:
    weight: 6
targetFramework: net8.0
---

# Notes

These pages answer questions that come up *after* you have used the libraries: what exactly ships, where it runs,
what it costs, and why the guarantees hold. None of them is a step in the learning path, so they sit here rather
than inside a product area.

If you have not written a declaration yet, start with [Getting started](/getting-started/index.html) instead.

## Pages

- [Packages and platforms](/notes/packages-and-platforms.html) — the full package inventory, what each one gives you, what
  it depends on, and which run on .NET and on Fable JavaScript.
- [Benchmarks](/notes/benchmarks.html) — measured parse and codec numbers against `System.Text.Json`, on .NET and Fable.
- [Compiler-Directed, AOT, and Fable](/notes/aot-trimming-fable.html) — why an explicit, reflection-free declaration keeps
  working under NativeAOT, aggressive trimming, and Fable.
