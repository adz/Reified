---
weight: 90
title: Comparisons
description: How Schema relates to FluentValidation and zod, and how Reified's constraint and result APIs relate to adjacent libraries.
targetFramework: net8.0
---

# Comparisons

How Schema relates to other validation and parsing libraries. Three scopes are compared, and they are different:

- [DataAnnotations and FluentValidation vs Constraint](/comparisons/dataannotations-fluentvalidation-comparison.html) —
  value rules as inspectable data, independent of the model a field belongs to.
- [FluentValidation vs Schema](/comparisons/fluentvalidation-comparison.html) — parsing constructs a trusted model
  instead of validating one that already exists.
- [zod vs Schema](/comparisons/zod-comparison.html) — the same parse-don't-validate idea in F#, with no reflection
  and a compiled trusted path.

[FSharpPlus/FsToolkit.ErrorHandling vs Result](/comparisons/fstoolkit-comparison.html) compares the independent
`Result` composition tools.
