---
weight: 30
title: Convert data and parse JSON
type: docs
description: Convert JSON, .NET values, configuration, and command-line input into and out of Data.
---

# Convert data and parse JSON

`Data` is a tree of objects, lists, text, numbers, Booleans, and null. Conversion functions copy other representations
into that tree. Extraction and rendering functions take values back out.

```fsharp
open Reified.Data
open Reified.Data.Syntax
```

## Parse JSON text portably

Install `Reified.Schema.Json` when JSON text must be parsed the same way on .NET and Fable. `Json.parseData` reads one
complete JSON value into a `Data` tree while preserving object field order, duplicate field names, and number-token
spelling.

```fsharp
open Reified.Schema.Json

let value = Json.parseData """{"amount":1.20e+3,"active":true}"""

value
// => Data.Object [
//      "amount", Data.Number "1.20e+3"
//      "active", Data.Bool true
//    ]
```

Invalid JSON raises `JsonCodecException`. `parseData` does not decode fields into application-specific record types;
it only converts JSON syntax into the corresponding `Data` cases.

`Data.Json.render` and `Data.Json.renderIndented` produce JSON text. `Data.render` and `Data.renderIndented` instead
produce a concise human-readable display.

```fsharp
Data.Json.render value
// => "{\"amount\":1.20e+3,\"active\":true}"
```

## Use the native .NET parser

On .NET 8+, use `Data.ofJsonElement` or `Data.ofJsonDocument` when JSON has already been parsed with
`System.Text.Json`. These conversion functions are intentionally .NET-only.

```fsharp
use document = System.Text.Json.JsonDocument.Parse("""{"name":"Ada"}""")
let value = Data.ofJsonDocument document

Data.Json.render value
// => "{\"name\":\"Ada\"}"
```

The returned `Data` is a copy and remains usable after the document is disposed.

## Use the native JavaScript parser under Fable

Under Fable, pass the result of the host's `JSON.parse` to `Data.ofJsonValue`:

```fsharp
open Fable.Core

let value = JS.JSON.parse """{"name":"Ada","active":true}""" |> Data.ofJsonValue
```

Native JSON parsing is convenient when its normal JavaScript semantics are acceptable. It discards duplicate object
fields and converts numbers to JavaScript numbers, so it cannot preserve the original number-token spelling. Use
`Json.parseData` from `Reified.Schema.Json` when those distinctions matter.

## Convert F# and .NET values

| Input | Function | Result |
| --- | --- | --- |
| `Map<string, Data>` | `Data.objectOfMap` | Object fields ordered by map key. |
| `(string * Data) list` | `Data.objectOfList` | Object fields in list order, including duplicates. |
| `Map<string, string>` | `Data.ofMap` | Object of text or null values. |
| `IDictionary<string, string>` | `Data.ofDictionary` | Object of text or null values. |
| `seq<string * string>` | `Data.ofNameValues` | Repeated names become lists. |
| `NameValueCollection` | `Data.ofNameValueCollection` | Repeated names become lists. |
| command-line arguments | `Data.ofCliArgs` | Parsed option names and their supplied values. |
| flattened configuration pairs | `Data.ofConfiguration` | Nested objects and lists from colon-separated keys. |
| configuration key/value pairs | `Data.ofConfigurationPairs` | The .NET configuration form, including null values. |

The three source adapters below differ in how they treat repetition and nesting, so each one's rules are worth reading
before you rely on them.

## Command-line arguments

`Data.ofCliArgs` takes `seq<string>` — normally the arguments handed to `main` — and always returns a `Data.Object`.

```fsharp
Data.ofCliArgs
    [ "--name"; "Ada"
      "--tag=fsharp"; "--tag"; "validation"
      "--active"
      "--no-archived"
      "import.csv"
      "--"; "--literal" ]
// => Data.Object [
//      "_", Data.List [ Data.Text "import.csv"; Data.Text "--literal" ]
//      "active", Data.Text "true"
//      "archived", Data.Text "false"
//      "name", Data.Text "Ada"
//      "tag", Data.List [ Data.Text "fsharp"; Data.Text "validation" ]
//    ]
```

The rules:

- `--name value`, `--name=value`, `-n value`, and `-n=value` all set the field `name` (or `n`) to the value.
- The following token is taken as the value only when it does not start with `-`. Otherwise the option is a flag and
  gets the text `"true"`.
- `--no-name` sets the field `name` to the text `"false"`.
- A repeated option collects its values into a `Data.List`, in the order they appeared.
- Anything that is not an option is positional, and positionals are collected under the field `_`.
- `--` ends option parsing: every remaining token is positional, even if it starts with a dash.
- Fields come out ordered by name, not in command-line order. Positional order is preserved within `_`.

Every CLI leaf is text, because that is all a command line carries. `Data.ofCliArgs` identifies structure; it never
decides that `"42"` is a number or `"true"` is a Boolean. That decision belongs to the schema that parses the tree.

## Configuration keys

`Data.ofConfiguration` takes flattened `seq<string * string>` and splits each key on `:` to rebuild the nesting. A
segment that is a non-negative integer is an index, so those siblings become a `Data.List`.

```fsharp
Data.ofConfiguration
    [ "displayName", "Ada"
      "contacts:0:value", "ada@example.com"
      "contacts:1:value", "+61 400 000 000"
      "features:email", "true" ]
// => Data.Object [
//      "contacts", Data.List [
//          Data.Object [ "value", Data.Text "ada@example.com" ]
//          Data.Object [ "value", Data.Text "+61 400 000 000" ]
//      ]
//      "displayName", Data.Text "Ada"
//      "features", Data.Object [ "email", Data.Text "true" ]
//    ]
```

Leaves stay text, or `Data.Null` when the value is null. Pairs are applied in order and later values win, which matches
.NET configuration layering:

```fsharp
Data.ofConfiguration [ "name", "default"; "name", "Ada" ]
// => Data.Object [ "name", Data.Text "Ada" ]

Data.ofConfiguration [ "a", "1"; "a:b", "2" ]
// => Data.Object [ "a", Data.Object [ "b", Data.Text "2" ] ]
```

Repetition never builds a list here — only indexed segments do. One exception protects sections: a null value does not
overwrite an existing section, because `IConfiguration.AsEnumerable()` emits every section key with a null value
alongside that section's children.

`Data.ofConfigurationPairs` has exactly these semantics but accepts `KeyValuePair<string, string>`, which is the shape
`IConfiguration.AsEnumerable()` returns, so it can be piped straight in.

## Name/value pairs

`Data.ofNameValues` (and `Data.ofNameValueCollection`) repeat differently from configuration: a repeated **name** is
the list-building mechanism, because that is how query strings and form posts carry multiple values.

```fsharp
Data.ofNameValues [ "tag", "fsharp"; "tag", "validation"; "name", "Ada" ]
// => Data.Object [
//      "tag", Data.List [ Data.Text "fsharp"; Data.Text "validation" ]
//      "name", Data.Text "Ada"
//    ]
```

## Where this leads

Every adapter produces the same source-neutral `Data`. That tree is a shape, not a domain model: nothing has been
typed, checked, or named in your vocabulary yet. `Schema.parse` takes it the rest of the way, converting leaves to
typed values, applying rules, and reporting failures against field paths.

```fsharp
Data.ofCliArgs argv
|> Schema.parse Registration.schema
```

Start from [the smallest schema]({{< relref "/schema/getting-started" >}}) if you have not declared one yet.

## Read values from Data

Use paths to retrieve nested data. `tryFindPath` returns `None` for a missing path; `lookupPath` returns `Data.Null`.
The parsed-path equivalents are `tryFind` and `lookup`.

```fsharp
let value = data [ "customer" => [ "name" => "Ada" ] ]

Data.tryFindPath "customer.name" value
// => Some (Data.Text "Ada")

Data.lookupPath "customer.missing" value
// => Data.Null
```

Shape-specific extractors return `Some` only for the named case:

```fsharp
Data.tryText (Data.Text "Ada")             // => Some "Ada"
Data.tryBool (Data.Bool true)               // => Some true
Data.tryNumberToken (Data.Number "1.20")   // => Some "1.20"
Data.tryList (Data.List [])                 // => Some []
Data.tryObject (Data.Object [])             // => Some []
```

`tryRedisplay`, `redisplay`, and their `At` and `Path` forms turn scalar values back into form-style text. Use JSON
rendering when the output must remain structured JSON.
