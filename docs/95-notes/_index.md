---
weight: 95
title: Notes
linkTitle: Notes
type: docs
notoc: true
description: Package inventory, platform support, benchmarks, and compiler-directed design.
menu:
  main:
    weight: 6
targetFramework: net8.0
---

# Notes

These pages cover package contents, platform support, benchmarks, and runtime portability.

If you have not written a declaration yet, read [Getting started](/getting-started/index.html) instead.

## Pages

- [Packages and platforms](/notes/packages-and-platforms.html), package contents, dependencies, and .NET and Fable JavaScript support.
- [Benchmarks](/notes/benchmarks.html), measured parse and codec numbers against `System.Text.Json` on .NET.
- [Compiler-Directed, AOT, and Fable](/notes/aot-trimming-fable.html), explicit declarations under NativeAOT,
  aggressive trimming, and Fable, plus NativeAOT compiler limits for records with many fields.
