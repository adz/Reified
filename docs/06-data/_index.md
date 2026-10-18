---
weight: 6
title: Data
linkTitle: Data
type: docs
notoc: true
description: Build, change, compare, and test structured data without repetitive constructors.
targetFramework: net8.0
---

# Data

`Reified.Data` builds and changes structured data. Its objects, lists, text, numbers, Booleans, and null
map directly to JSON, and also work for test fixtures, configuration, command-line input, form values,
events, and other tree-shaped data.

Build one value, derive related cases without copying it, and test the complete result or selected fields. This avoids
large JSON strings, nested constructors, and near-identical fixtures. Failures identify the path that differs.

## Install

Install the package with:

```sh
dotnet add package Reified.Data
```


## Build, change, and check one value

```fsharp
open Reified
open Reified.DataDSL

let baseline =
    data [
        "name" => "Ada"
        "plan" => "free"
        "address" => [
            "city" => "Adelaide"
            "postcode" => 5000
        ]
        "roles" => [ "author" ]
    ]

let request =
    baseline
    |> Data.patch [
        replace "plan" "pro"
        append "roles" "admin"
    ]
```


`request` contains the changed plan and the additional role. `baseline` is unchanged.

```fsharp
Data.render request
// => "{ name: \"Ada\", plan: \"pro\", address: { city: \"Adelaide\", postcode: 5000 }, roles: [\"author\", \"admin\"] }"
```


Check selected parts of the result:

```fsharp
request
|> matching [
    at "name" "Ada"
    at "address" (containing [ "postcode" => 5000 ])
    at "roles" (containingItems [ "admin" ])
    absent "error"
]
// succeeds
```


If an expectation fails, `matching` raises `DataMatchException` with the mismatched path and values. Extra fields and
the additional `author` role are allowed because these patterns check only the values named here.

## Basic syntax

Use `data` to build a `Data` value. Fields declared with `name => value` make an object; ordinary values in the
same list position make a `Data.List` instead.

```fsharp
open Reified
open Reified.DataDSL

let person = data [ "name" => "Ada"; "active" => true ]
let roles = [ "author"; "admin" ]
```


Use `?=>` for an optional field. `Some value` includes the field; `None` leaves the field out altogether. Use
`name => nil` when the field must be present with a null value.

```fsharp
let nickname : string option = None
let account = data [ "nickname" ?=> nickname; "deletedAt" => nil ]

Data.render account
// => "{ deletedAt: null }"
```


## Build data with ordinary F# control flow

`data` takes an F# list of fields, so the whole list-expression vocabulary is available inside it. Include another
object's fields with `yield!`, add a field only under a condition with `if`, and generate fields from a sequence with
`for`. This needs no intermediate dictionary or null filtering.

With `common = data [ "tenant" => "acme"; "region" => "au" ]`, `customerId = "c-1"`, `includeDebug = true`, and
`names = [ "ada"; "grace" ]`:

```fsharp
let common = data [ "tenant" => "acme"; "region" => "au" ]
let customerId = "c-1"
let includeDebug = true
let names = [ "ada"; "grace" ]

let event =
    data [
        "kind" => "example"
        "customerId" => customerId
        yield! fields common

        if includeDebug then
            "debug" => true

        for name in names do
            $"user-{name}" => name
    ]

Data.render event
// => "{ kind: \"example\", customerId: \"c-1\", tenant: \"acme\", region: \"au\", debug: true, user-ada: \"ada\", user-grace: \"grace\" }"
```


Fields appear in yield order, including conditional and generated fields.
The same works for lists: `data [ "ids" => [ for id in ids -> id * 10 ] ]`.

## Learn and solve tasks

- [Tutorial: build, vary, and test structured data](tutorial/)
- [What Data represents](what-it-does/)
- [DataDSL](dsl/)
- [Numbers](numbers/)
- [Declare, render, and edit data](declaring-and-editing/)
- [Convert data and parse JSON](converting-data/)
- [Match selected parts of data](how-to-test-produced-json/)
- [Build variations and matrices](how-to-build-test-cases/)
- [Compare complete data](compare-data/)
- [API reference](/api/Reified.Data.html)
