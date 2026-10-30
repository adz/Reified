---
weight: 30
title: Convert data and parse JSON
type: docs
description: Convert JSON, .NET values, configuration, and command-line input into and out of Data.
targetFramework: net8.0
---

# Convert data and parse JSON

`Data` is a tree of objects, lists, text, numbers, Booleans, and null. Conversion functions copy other representations
into that tree. Extraction and rendering functions take values back out.

```fsharp
open Reified
open Reified.DataDSL
```


## Parse JSON text portably

Install `Reified.Schema` when JSON text must be parsed the same way on .NET and Fable. `Json.parseData` reads one
complete JSON value into a `Data` tree while preserving object field order, duplicate field names, and number-token
spelling.

```fsharp
open Reified

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

```fsharp no-check reason="Data.ofJsonDocument requires .NET 8 or later."
use document = System.Text.Json.JsonDocument.Parse("""{"name":"Ada"}""")
let value = Data.ofJsonDocument document

Data.Json.render value
// => "{\"name\":\"Ada\"}"
```


The returned `Data` is a copy and remains usable after the document is disposed.

## Use the native JavaScript parser under Fable

Under Fable, pass the result of the host's `JSON.parse` to `Data.ofJsonValue`:

```fsharp no-check reason="Fable-only snippet; Fable.Core and JS.JSON are unavailable in a .NET build."
open Fable.Core

let value = JS.JSON.parse """{"name":"Ada","active":true}""" |> Data.ofJsonValue
```


Native JSON parsing is convenient when its normal JavaScript semantics are acceptable. It discards duplicate object
fields and converts numbers to JavaScript numbers, so it cannot preserve the original number-token spelling. Use
`Json.parseData` from `Reified.Schema` when those distinctions matter.

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
| flattened configuration pairs | `Data.ofConfiguration`, `Data.ofConfigurationWith` | Bounded nested objects/lists from colon-separated keys. |
| configuration key/value pairs | `Data.ofConfigurationPairs` | The .NET configuration form, including null values. |
| environment variable pairs | `Data.ofEnvironment`, `Data.ofEnvironmentWith` | Nested objects and lists from `__`-separated names. |

The adapters below handle repetition and nesting differently.

## Command-line arguments

`Data.ofCliArgs` takes `seq<string>`, normally the arguments handed to `main`, and always returns a `Data.Object`.

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

Every CLI leaf is text. `Data.ofCliArgs` identifies structure but leaves `"42"` and `"true"` as text; the schema
parsing the tree decides whether they represent a number or Boolean.

Because a value token must not start with `-`, a negative number as a separate token reads as a positional, and a
trailing option with no following token reads as a flag:

```fsharp
Data.ofCliArgs [ "--offset"; "-5"; "--verbose" ]
// => Data.Object [
//      "_", Data.List [ Data.Text "-5" ]
//      "offset", Data.Text "true"
//      "verbose", Data.Text "true"
//    ]
```

`--offset -5` did not set `offset` to `-5`; it set `offset` to `"true"` and made `-5` a positional. Use the equals
form to pass a negative value: `--offset=-5` sets `offset` to the text `"-5"`.

## Configuration keys

`Data.ofConfiguration` takes flattened `seq<string * string>` and splits each key on `:` to rebuild the nesting. A
section becomes a `Data.List` only when every child name is a canonical ASCII decimal index: `0` or
`[1-9][0-9]*`. Signs, whitespace, hexadecimal prefixes, underscores, Unicode digits and leading zeroes are not
indexes. Those names remain object fields, identically on .NET and Fable.

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


Repetition never builds a list here, only indexed segments do. One exception protects sections: a null value does not
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


## Environment variables

`Data.ofEnvironment` takes `seq<string * string>` and splits each name on `__`, the
Microsoft.Extensions.Configuration convention, because `:` is not valid in a shell variable name. It does not read the
environment itself; pass the pairs in, so the conversion stays a pure function that tests can feed directly.

```fsharp
Data.ofEnvironment
    [ "Logging__Level", "Debug"
      "Servers__0__Host", "a.internal"
      "Servers__1__Host", "b.internal"
      "Port", "8080" ]
// => Data.Object [
//      "Logging", Data.Object [ "Level", Data.Text "Debug" ]
//      "Port", Data.Text "8080"
//      "Servers", Data.List [
//          Data.Object [ "Host", Data.Text "a.internal" ]
//          Data.Object [ "Host", Data.Text "b.internal" ]
//      ]
//    ]
```


`Data.ofEnvironmentWith` takes a function that adjusts the defaults. A `Prefix` selects only the variables that start
with it, compared case-sensitively, and strips it; include the separator in the prefix. `Separator` replaces `__`.

```fsharp
[ "MYAPP__Database__Port", "5432"; "PATH", "/usr/bin" ]
|> Data.ofEnvironmentWith (fun options -> { options with Prefix = Some "MYAPP__" })
// => Data.Object [ "Database", Data.Object [ "Port", Data.Text "5432" ] ]
```


On .NET, read the variables with `Environment.GetEnvironmentVariables()`; under Fable on Node, read `process.env`:

```fsharp no-check reason="Reads the host process environment, which differs on every machine."
let environment =
    System.Environment.GetEnvironmentVariables()
    |> Seq.cast<System.Collections.DictionaryEntry>
    |> Seq.map (fun entry -> string entry.Key, string entry.Value)
    |> Data.ofEnvironmentWith (fun options -> { options with Prefix = Some "MYAPP__" })
```


The rules:

- Supplied values become text, or `Data.Null` for a null string; the schema decides whether `"5432"` is a number.
- Names keep their case. `PORT` and `Port` are different fields, and a schema field `port` matches neither unless it
  declares `alias "PORT"`. Matching is the schema's concern, not the adapter's.
- Sections with only canonical decimal child names become lists, as in `ofConfiguration`; the root is always an
  object. `0` and `00` remain separate fields in an object section, never aliases that discard one child's subtree.
- The environment has no meaningful order, so names apply in ordinal order rather than input order. A name that is
  also a section loses to the section in either order: `A=1` with `A__B=2` gives `A.B = "2"`. A repeated name keeps
  its last value.
- A name that is empty after the prefix, or that contains an empty segment, cannot address a path and is skipped.
  Shells and operating systems set such variables (macOS sets `__CF_USER_TEXT_ENCODING`), so rejecting them would make
  an unprefixed read fail on ordinary machines.

## Conversion limits

Environment and flattened configuration conversion default to **10,000 items per list**, **100,000 output nodes**
across the complete result, and **64 input path segments**. The node budget includes the root, every object/list,
supplied leaves and every null gap in a sparse list. For example, `X__9999` requires 10,000 list slots; `X__10000`
exceeds the default length. Canonical numeric indexes too large for Int32 are rejected, not reinterpreted as object
names. The same bounds apply to `ofConfigurationPairs`.

A limit violation throws `ArgumentException` naming the path and bound before oversized list expansion. Limits
must be positive. Raise them explicitly for larger trusted inputs with `Data.ofConfigurationWith` or
`DataEnvironmentOptions.Limits`:

```fsharp isolated
open Reified

let limits (defaults: DataConversionLimits) =
    { defaults with MaxCollectionLength = 2; MaxExpandedNodes = 7; MaxDepth = 2 }

let configuration = Data.ofConfigurationWith limits [ "A:1", "a"; "B:1", "b" ]
let environment =
    [ "A__1", "a"; "B__1", "b" ]
    |> Data.ofEnvironmentWith (fun options -> { options with Limits = limits options.Limits })
// Both have one root object, two lists and four list slots: seven nodes including the two null gaps.
```

Limits bound conversion's expansion, not source text bytes or the number of supplied pairs. A host handling
untrusted uploads should bound those inputs too. `merge` does not apply these adapter-specific limits to arbitrary
already-built Data values.

## Merge layered sources

`Data.merge` combines two values and the right one wins. Objects merge field by field at every depth. Field names keep
the order they first appear in, and names only the right side has are appended. A right-side list, scalar, or
`Data.Null` replaces whatever the left had, and an object replaces a non-object.

```fsharp
Data.merge
    (data [ "database" => [ "host" => "localhost"; "port" => 5432 ]; "hosts" => [ "a"; "b" ] ])
    (data [ "database" => [ "host" => "db.internal" ]; "hosts" => [ "c" ] ])
// => Data.Object [
//      "database", Data.Object [ "host", Data.Text "db.internal"; "port", Data.Number "5432" ]
//      "hosts", Data.List [ Data.Text "c" ]
//    ]
```


`Data.mergeAll` folds a list from the left, starting from an empty object, so list the layers from lowest to highest
precedence:

```fsharp no-check reason="configFileText, the environment pairs, and argv belong to the host application."
Data.mergeAll
    [ Json.parseData configFileText
      Data.ofEnvironmentWith (fun options -> { options with Prefix = Some "MYAPP__" }) environment
      Data.ofCliArgs argv ]
|> Schema.parse settingsSchema
```


Choices that matter for configuration:

- A later `null` overrides. `{"proxy": null}` in a later layer leaves `proxy` present with the value `Data.Null`, which
  an option field parses as `None`. No merge removes a key. JSON Merge Patch (RFC 7396) deletes on null instead; use
  `Data.patch` with `remove` when a key must go.
- Lists replace whole. An indexed environment variable such as `Servers__0__Host` builds a complete list that replaces
  the file's list, so a layer that changes one element must supply every element.
- A name repeated within an object merges into its first occurrence, recursively through object-valued fields.
  Lists are opaque: objects inside them are not normalized. Even `mergeAll [oneObject]` normalizes duplicates;
  the empty-object identity law applies to already-normalized object trees, not arbitrary duplicate-key inputs.
- Merging is associative while layers agree on which paths hold objects. When a middle layer puts a non-object between
  two objects at one path, grouping changes the result; `mergeAll` always applies layers in order, so prefer it.

A singleton object layer need not be identical to its input:

```fsharp isolated
open Reified

let duplicate = Data.Object [ "x", Data.Text "first"; "x", Data.Text "last" ]
let normalized = Data.mergeAll [ duplicate ]
assert (normalized = Data.Object [ "x", Data.Text "last" ])
```

## Parse adapted data with a schema

Each adapter produces source-neutral `Data`, not a typed domain model. `Schema.parse` converts leaves to typed values,
applies rules, and reports failures against field paths.

```fsharp no-check reason="argv and Registration.schema belong to the host application."
Data.ofCliArgs argv
|> Schema.parse Registration.schema
```


Start from [the smallest schema](/schema/quickstart.html) if you have not declared one yet.

## Read values from Data

Use paths to retrieve nested data. `tryFindPath` returns `None` for a missing path; `lookupPath` returns `Data.Null`.
The parsed-path equivalents are `tryFind` and `lookup`. Prefer `tryFindPath` at a boundary that must distinguish a
missing path from an explicit `Data.Null`; `lookupPath` collapses the two.

```fsharp
let nestedValue = data [ "customer" => [ "name" => "Ada" ] ]

Data.tryFindPath "customer.name" nestedValue
// => Some (Data.Text "Ada")

Data.lookupPath "customer.missing" nestedValue
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
