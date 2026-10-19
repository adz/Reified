---
weight: 85
title: Runnable Examples
description: Executable schema, refined, diagnostics, and policy examples mirrored back into the docs.
targetFramework: net8.0
---

# Runnable Examples

The docs build runs these examples from the repository projects and writes their output back into this page. Each is a
complete runnable program in `examples/Reified.Schema.Examples`; the focused excerpts below show the interesting part.
Run an example to see its full output.

## Refined Catalog Example

A request boundary that parses strings, builds refined numeric/text/collection values, chooses a domain union case, and
rejects invalid input before the domain record is created.

Run it:

```bash
REIFIED_EXAMPLE=refined-catalog dotnet run --project examples/Reified.Schema.Examples/Reified.Schema.Examples.fsproj --nologo
```

Source:

- [RefinedCatalogExample.fs](https://github.com/adz/Reified/blob/main/examples/Reified.Schema.Examples/RefinedCatalogExample.fs)

The admission pipeline is `Parse` → `Refinement.create` (or a checked `create`) → the domain record. Each step maps its
own error so a caller can tell a parse failure from a rule failure:

```fsharp no-check reason="Excerpt from a whole source file, not a fragment."
result {
    let! parsedId = Parse.int rawId |> Result.mapError (sprintf "%A")
    let! id = ProductId.create parsedId |> Result.mapError Violation.render
    let! slug = Slug.create rawSlug |> Result.mapError Violation.render
    let! displayName = Refine.nonBlankString rawDisplayName |> Result.mapError Violation.render
    return { Id = id; Slug = ProductSlug slug; DisplayName = DisplayName displayName; ... }
}
```

## Refined Value Schema Example

Total domain conversions built with `Schema.convert`, composed into a record schema, and lowered to executable checks.
The `create` helpers in that file are unchecked wrappers over the example's own private types; the executable rules live
on the schemas, and `SchemaCheck` or `Schema.parse` enforce them. At a real boundary, admit input with `Schema.parse`,
or define a `Refinement.define`d constructor when the type itself must carry the guarantee.

Run it:

```bash
REIFIED_EXAMPLE=refined-value-schema dotnet run --project examples/Reified.Schema.Examples/Reified.Schema.Examples.fsproj --nologo
```

Source:

- [RefinedValueSchemaExample.fs](https://github.com/adz/Reified/blob/main/examples/Reified.Schema.Examples/RefinedValueSchemaExample.fs)

A refined field type contributes one canonical schema through a static member; a bare `field` then resolves it:

```fsharp no-check reason="Excerpt from a whole source file, not a fragment."
type Quantity =
    private
    | Quantity of int

    static member Schema(_: Quantity) : Schema<Quantity> =
        Schema.int
        |> Schema.constrain (Constraint.greaterThan 0)
        |> Schema.convert Quantity (fun (Quantity value) -> value)
```

See [Refined Schemas](/schema/refined-values.html) for the full pattern behind these excerpts.
