---
weight: 20
title: Declare, render, and edit data
type: docs
description: Every literal, rendering, and immutable edit operation in Reified.Data.
---

# Declare, render, and edit data

Open `Reified.Data` for `Data`. Open `Reified.DataDSL` for the concise operators and functions used on this page.

```fsharp
open Reified
open Reified.DataDSL
```

## Declare objects and lists

`data` builds an object from field declarations. `=>` adds a field. A nested field list becomes another object, while
a list of ordinary values becomes `Data.List`.

```fsharp
let customer =
    data [
        "name" => "Ada"
        "active" => true
        "address" => [
            "city" => "Adelaide"
            "postcode" => 5000
        ]
        "roles" => [ "author"; "admin" ]
    ]

Data.render customer
// => "{ name: \"Ada\", active: true, address: { city: \"Adelaide\", postcode: 5000 }, roles: [\"author\", \"admin\"] }"
```

Literal fields accept `Data`, `string`, `bool`, `int`, `int64`, `decimal`, finite `float`, `Guid`, `DateTimeOffset`,
`DateOnly` on .NET 8+, supported lists, and nested field lists.

## Omit a field or write null

`?=>` adds a `Some` value and omits `None`. `nil` writes a present JSON null.

```fsharp
let value =
    data [
        "nickname" ?=> None
        "deletedAt" => nil
    ]

Data.render value
// => "{ deletedAt: null }"
```

## Reuse object fields

`fields` returns the fields of an existing object so they can be included in a new declaration.

```fsharp
let address = data [ "city" => "Adelaide"; "postcode" => 5000 ]
let customer = data [ yield! fields address; "name" => "Ada" ]

Data.render customer
// => "{ city: \"Adelaide\", postcode: 5000, name: \"Ada\" }"
```

## Build fields with control flow

The argument to `data` is an ordinary F# list of `DataField`, so every list-expression form works inside it. Mix
literal fields with `yield!`, `if`, `for`, `match`, and `let` bindings in one declaration.

```fsharp
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
```

With `common = data [ "tenant" => "acme"; "region" => "au" ]`, `customerId = "c-1"`, `includeDebug = true`, and
`names = [ "ada"; "grace" ]`:

```fsharp
Data.render event
// => "{ kind: \"example\", customerId: \"c-1\", tenant: \"acme\", region: \"au\", debug: true, user-ada: \"ada\", user-grace: \"grace\" }"
```

Points worth knowing:

- Fields keep the order they are yielded. A conditional or generated field appears where it is written, not appended
  at the end.
- `yield!` splices a `DataField list`. `fields` produces one from an existing object; `Data.fields` is the explicit
  name. Splicing raises if the value is not an object.
- A bare `field` in the list is an implicit `yield`. F# permits mixing implicit yields with `if`, `for`, and `yield!`
  in the same list, so no `yield` keyword is needed on the plain lines.
- An `if` without `else` contributes nothing when the condition is false. Use `?=>` instead when the choice is
  `Some`/`None` on a single field, and `if`/`for` when the shape of the object varies.
- The same forms build lists: `data [ "ids" => [ for id in ids -> id * 10 ] ]` renders `{ ids: [10, 20, 30] }`.

Control flow decides which fields exist. `Data.patch` changes fields that already exist. Prefer control flow when
building a value from inputs, and patching when deriving a variation from a value you already have.

## Render for people

`Data.render` returns a compact display with unquoted ordinary field names and quoted text. `Data.renderIndented`
returns the same notation with line breaks and indentation. Both preserve object field order, duplicate fields, and
number tokens. Use `Data.Json.render` when the result must be JSON.

```fsharp
Data.renderIndented (data [ "name" => "Ada" ])
// => "{\n  name: \"Ada\"\n}"
```

## Edit without changing the original

Use a direct `Data` operation for one change. It returns the changed tree and leaves the original unchanged.

```fsharp
let renamed = customer |> Data.replace "name" "Grace"

Data.lookupPath "name" renamed
// => Data.Text "Grace"

Data.lookupPath "name" customer
// => Data.Text "Ada"
```

Every edit is available directly:

| Direct operation | Result |
| --- | --- |
| `Data.set path value input` | Replace a value, or add a missing final object field. |
| `Data.replace path value input` | Replace an existing value; fail if it is missing. |
| `Data.remove path input` | Remove an existing field or list item. |
| `Data.append path value input` | Add an item to the end of a list. |
| `Data.prepend path value input` | Add an item to the start of a list. |
| `Data.insert path index value input` | Add an item at a list index. |
| `Data.rename path newName input` | Rename an object field without moving it. |
| `Data.update path function input` | Replace a value with the function result. |

```fsharp
customer
|> Data.set "plan" "pro"
|> Data.append "roles" "admin"
|> Data.rename "address.city" "suburb"
|> Data.remove "active"
```

`Data.replace`, `remove`, `append`, `prepend`, `insert`, `rename`, and `update` require their target to exist.
`Data.set` may add its final object field, but its parent must exist. Shape and path failures raise
`DataPatchException`.

## Apply several edits atomically

`Data.patch` applies a list of edits in order. It returns a new value only when every edit succeeds.

```fsharp
let changed =
    customer
    |> Data.patch [
        replace "name" "Grace"
        set "plan" "pro"
        append "roles" "admin"
    ]
```

Inside `Data.patch`, use the unqualified edit constructors from `Reified.DataDSL`:

| Operation | Result |
| --- | --- |
| `set path value` | Replace a value, or add a missing final object field. |
| `replace path value` | Replace an existing value; fail if it is missing. |
| `remove path` | Remove an existing field or list item. |
| `append path value` | Add an item to the end of a list. |
| `prepend path value` | Add an item to the start of a list. |
| `insert path index value` | Add an item at a list index. |
| `rename path newName` | Rename an object field without moving it. |
| `update path function` | Replace a value with the function result. |

Every operation except a missing final object field handled by `set` requires its target to exist. If an edit fails,
none of the edits are returned as a partial result.

`Data.patch` raises `DataPatchException`. `Data.tryPatch` returns the failure instead:

```fsharp
Data.tryPatch [ append "name" "Grace" ] customer
// => Error [
//      { EditIndex = 0
//        Path = "name"
//        Message = "Expected a list but found text." }
//    ]
```
