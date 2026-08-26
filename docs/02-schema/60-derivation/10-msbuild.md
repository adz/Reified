---
title: Build Generation
linkTitle: Build Generation
description: Configure MSBuild to generate schemas from marked F# records automatically.
weight: 10
targetFramework: net8.0
---

# Build Generation

Add the runtime Schema package and the private build-time generator package:

```xml
<ItemGroup>
  <PackageReference Include="Reified.Schema" Version="..." />
  <PackageReference Include="Reified.Schema.Contracts.Build" Version="..." PrivateAssets="all" />
</ItemGroup>
```


Put `[<DeriveSchema>]` records in ordinary `.fs` files already listed as `<Compile>` items. No generator item, target,
or manual generated-file `<Compile>` entry is required. Before `CoreCompile`, the package:

1. examines the project's F# compile files in order;
2. generates a companion for every file containing marked records;
3. inserts each generated file immediately after its source file; and
4. lets the F# compiler catch stale field names and incompatible generated code.

By default output is written below `obj/`, so it should not be committed.

## Complete project example

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Wire.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Reified.Schema" Version="..." />
    <PackageReference Include="Reified.Schema.Contracts.Build" Version="..." PrivateAssets="all" />
  </ItemGroup>
</Project>
```


`Wire.fs` may use a namespace or a file-level module. Place marked records directly in that container, not in a nested module:

```fsharp no-check reason="Declares its own namespace as the first thing in the file, which cannot follow the site's F# prelude opens; not independently checkable."
namespace MyApp.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type Order = { Sku: string; Quantity: int }
```


An ordinary `dotnet build` now generates and compiles the companion schema.

## MSBuild properties

| Property | Default | Purpose |
| --- | --- | --- |
| `ReifiedSchemaGenEnabled` | `true` | Set to `false` to skip generation. |
| `ReifiedSchemaNaming` | `camel` | Default wire naming: `camel`, `snake`, or `verbatim`. |
| `ReifiedSchemaGeneratedFiles` | `Intermediate` | `Intermediate` writes under `obj`; `CheckedIn` writes sibling `.g.fs` files. |
| `ReifiedSchemaGenToolPath` | packaged tool | Override the generator task assembly path, mainly for repository development. |

### Checked-in output

Use checked-in output when generated source must be reviewed or consumed outside the normal build:

```xml
<PropertyGroup>
  <ReifiedSchemaGeneratedFiles>CheckedIn</ReifiedSchemaGeneratedFiles>
</PropertyGroup>
```


The build writes `Wire.g.fs` beside `Wire.fs` and still manages F# compile ordering. Do **not** add the `.g.fs` file to
`<Compile>` yourself.

## Troubleshooting

- **No output:** confirm generation is enabled, the source is a `<Compile>` item, and the record has
  `[<DeriveSchema>]`.
- **Container diagnostic:** each derived source file must use one namespace or one file-level module, with marked
  records declared directly inside it.
- **Duplicate compile item:** remove a manually listed `.g.fs`; the target inserts it.
- **A stale checked-in file fails compilation:** rebuild to regenerate it. This failure is intentional drift detection.
