---
weight: 30
title: Build variations and matrices
type: docs
description: Derive named test cases from one structured baseline.
targetFramework: net8.0
---

# Build variations and matrices

Keep one representative baseline and describe each test case as a strict immutable edit.

```fsharp
open Reified
open Reified.DataDSL

let baseline =
    data [
        "name" => "Ada"
        "plan" => "free"
        "region" => "au"
        "roles" => [ "author" ]
    ]
```


## The three types

```fsharp
type DataVariation = { Name: string; Edits: DataEdit list }
type DataDimension = { Name: string; Variations: DataVariation list }
type DataCase = { Name: string; Value: Data }
```


`variant` builds a `DataVariation`: a name and the edits, with no baseline attached yet. It is a description, so the
same variation list can be applied to more than one baseline. `dimension` groups variations into one independent axis
of a matrix.

`variants` and `matrix` apply those descriptions to a baseline and return a `DataCase list`: each case is a name and
the value that resulted. A `DataCase` is what a test iterates over.

### Why not just `Data list`

A bare list of values loses case names, so a failure reports index `5` instead of
`plan: pro / region: US / roles: admin`, and reordering the list silently renumbers every test. Carrying the name with
the value means:

- Test-framework case names and assertion messages come straight from `case.Name`.
- `matrix` composes names from its dimensions, so a case identifies itself by the choice made on every axis rather
  than by a position in a Cartesian product.
- `variants` can reject duplicate names, which catches two cases that were meant to differ but describe the same thing.

`case.Value` is plain `Data`, so `Data.patch`, `matching`, `Data.compare`, and `Data.Json.render` apply directly.

```fsharp no-check reason="cases, submit and test belong to the host application's test suite."
for case in cases do
    test case.Name (fun () -> case.Value |> submit |> matching [ at "status" "rejected" ])
```


## Build independent cases

```fsharp
let cases =
    baseline
    |> variants [
        variant "valid" []
        variant "missing name" [ remove "name" ]
        variant "blank name" [ replace "name" "" ]
        variant "wrong name shape" [ replace "name" [ "Ada" ] ]
    ]
```


`variants` rejects duplicate names and preserves declaration order.

The result has four `DataCase` values in the shown order:

```text
valid             name = "Ada"
missing name      name is absent
blank name        name = ""
wrong name shape  name = ["Ada"]
```


## Build a Cartesian matrix

```fsharp
let matrixCases =
    baseline
    |> matrix [
        dimension "plan" [
            variant "free" []
            variant "pro" [ replace "plan" "pro" ]
        ]

        dimension "region" [
            variant "AU" []
            variant "US" [ replace "region" "us" ]
        ]

        dimension "roles" [
            variant "none" [ replace "roles" ([]: string list) ]
            variant "admin" [ replace "roles" [ "admin" ] ]
        ]
    ]
```


Names follow dimension order, such as `plan: pro / region: AU / roles: admin`.

The matrix limit is 256 combinations. The product is checked before cases are materialized. Exceeding it raises
`System.ArgumentException` with the message `"The matrix would create N cases; the maximum is 256."`, before any
case is built. A dimension with no variations does not throw: its product is zero, so `matrix` returns an empty case
list instead. When an empty dimension is itself a test-authoring bug, check `dimension.Variations` before calling
`matrix`.

This matrix produces eight cases. The first is
`plan: free / region: AU / roles: none`; its value has `plan = "free"`, `region = "au"`, and `roles = []`.

The last is `plan: pro / region: US / roles: admin`; its value has `plan = "pro"`, `region = "us"`, and
`roles = ["admin"]`.

```fsharp
matrixCases |> List.map _.Name
// =>
// [ "plan: free / region: AU / roles: none"
//   "plan: free / region: AU / roles: admin"
//   "plan: free / region: US / roles: none"
//   "plan: free / region: US / roles: admin"
//   "plan: pro / region: AU / roles: none"
//   "plan: pro / region: AU / roles: admin"
//   "plan: pro / region: US / roles: none"
//   "plan: pro / region: US / roles: admin" ]
```


## Inspect patch failures from an edit list

```fsharp no-check reason="edits and runCase belong to the host application's test suite."
match Data.tryPatch edits baseline with
| Ok value -> runCase value
| Error failures ->
    failures
    |> List.iter (fun failure ->
        printfn "Edit %d at %s: %s" failure.EditIndex failure.Path failure.Message)
```


`Data.patch` raises `DataPatchException` with the same failures.

For `append "name" "Grace"`, the result is one failure at edit index `0`, path `name`, with a message stating that a
list was expected but text was found. The baseline remains unchanged.

```fsharp
Data.tryPatch [ append "name" "Grace" ] baseline
// => Error [
//      { EditIndex = 0
//        Path = "name"
//        Message = "Expected a list but found text." }
//    ]
```
