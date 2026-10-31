---
weight: 25
title: Input Sources
type: docs
description: HTTP form-like, CLI, JSON-like, and configuration input through one schema.
targetFramework: net8.0
---

# Input Sources

[`Data`](/data/index.html) is the source-agnostic boundary shape: `Null`, `Text`, `Number`, `Bool`, `List`, and
`Object`. Adapters turn common sources into `Data`, and one schema parses them all. (`Data` is its own dependency-free
package, useful beyond schemas, see [its docs](/data/index.html).)

## The Schema

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Address = { City: string }
type Contact = { Kind: string; Value: string }

type Customer =
    { Name: string
      Address: Address
      Contacts: Contact list }

let addressSchema =
    schema<Address> {
        field _.City
        construct (fun city -> { City = city })
    }

let contactSchema =
    schema<Contact> {
        field _.Kind
        field _.Value
        construct (fun kind value -> { Kind = kind; Value = value })
    }

let customerSchema =
    schema<Customer> {
        field _.Name
        field _.Address {
            withSchema addressSchema
        }
        field _.Contacts {
            withSchema (Schema.listWith contactSchema)
            constrain (minLength 1)
        }
        construct (fun name address contacts ->
            { Name = name; Address = address; Contacts = contacts })
    }
```


Here `addressSchema` and `contactSchema` are intentionally local value schemas. If `Address` and `Contact` declare
canonical intrinsic schemas, the `Address` line can use ordinary `field`, and the `Contacts` line can keep its field
block while omitting only `withSchema`: the list still resolves through `Contact`'s canonical schema, but the block's
`constrain (minLength 1)` stays attached to the list field. That constraint constrains the length of the `Contacts`
list, so it belongs on the list field, not on `Contact` itself, which is a single record. A bare `field _.Contacts`
would drop the length check along with `withSchema`.

Nested fields expect object-shaped input and prefix their diagnostics with the field name; collection fields expect
`Data.List`, parse every item, accumulate every item error, and prefix diagnostics with the item index.

Each adapter below produces `Data`. The closing parse example uses the configuration input; the other examples
are isolated so their `raw` bindings do not collide.

## HTTP Form-Like Input

Form posts and query strings are name/value pairs; repeated names become `Data.List` values:

```fsharp isolated
open Reified

let raw =
    Data.ofNameValues
        [ "name", "Ada Lovelace"
          "tag", "vip"
          "tag", "beta" ]      // repeated names accumulate into Data.List
```


`Data.ofMap` handles single-valued maps, and `Data.ofNameValueCollection` adapts
`System.Collections.Specialized.NameValueCollection` directly from ASP.NET-style APIs. Name/value sources are flat; use
the configuration or JSON adapters below when the input carries nested models or indexed collections.

## CLI Arguments

```fsharp isolated
open Reified

let raw = Data.ofCliArgs [ "--name"; "Ada Lovelace"; "--verbose"; "--no-color" ]
```


`--name value`, `--name=value`, `-n value`, boolean flags, `--no-name`, and repeated options are supported; positional
arguments collect under the `_` field. `--Db:Port 5432` and `--Db__Port=5432` create the same nested `Db.Port`
path by default. Sections use the canonical decimal index grammar and bounded expansion described below. Repeated
paths collect values in argument order; `:`/`__` spellings of the same path share that repetition.

## Key case

Parsing is case-sensitive unless configured with `Schema.ignoreKeyCase`. This option matches record field names
and aliases ordinal-ignore-case at every model depth; it does not rename fields or change arbitrary map keys,
tag values, prefix filtering, or compiled JSON codec name matching.

```fsharp isolated
open Reified
open Reified.SchemaDSL

type PortSettings = { Port: int }
let settingsSchema = schema<PortSettings> { field _.Port; construct (fun port -> { Port = port }) }
let input =
    [ Data.ofConfiguration [ "port", "1" ]
      Data.ofEnvironment [ "PORT", "2" ]
      Data.ofCliArgs [ "--port=5432" ] ]
    |> Data.mergeAllWith Data.ordinalIgnoreCase
let parsed = input |> Schema.parseWith Schema.ignoreKeyCase settingsSchema
assert (parsed = Ok { Port = 5432 })
```

Within an input object, equivalent names follow the merge rule: first spelling/position survive, later values win,
and object-valued sections merge recursively. Lists/scalars/null replace whole. A schema whose declared names or
aliases collide under this comparison is rejected with `field.case.ambiguous`, rather than assigning one input to
two fields. Default case-sensitive alias ambiguity checks remain unchanged.

Use the same comparer **during layer merging**, not only at parse time. An exact-name merge can overwrite an early
`port` in place while leaving a later `PORT` beside it; the parser cannot recover the original source order.
`Data.mergeAllWith Data.ordinalIgnoreCase` preserves later-source precedence, including across three such layers.
The comparer is supported on both .NET and Fable; use it instead of the BCL `StringComparer` property in portable code.
`Schema.parseRetainingInputWith` applies the same parse options while retaining input for redisplay; declared secret
field names and aliases are matched ordinal-ignore-case during redaction.

For credentials, use [Secret fields](/refined/secrets.html), not a key-name redaction heuristic.

## JSON Bodies With System.Text.Json

On .NET 8+ targets, adapt a parsed `JsonDocument` or `JsonElement` directly, the natural fit for ASP.NET Core
request bodies:

```fsharp no-check reason="ASP.NET Core request-body handling; the request and its use! context belong to the host application."
use! document = JsonDocument.ParseAsync request.Body
let raw = Data.ofJsonDocument document
```


JSON null, numbers, Booleans, arrays, and objects retain their corresponding `Data` cases. Number tokens keep their
exact lexical representation. The adapter uses the in-box `System.Text.Json`, so the package stays dependency-free.

## Parse JSON portably

For the same lossless behavior on .NET and Fable, use `Json.parseData` from the installed `Reified.Schema` package:

```fsharp isolated
open Reified

let raw = Json.parseData """{"name":"Ada","score":1.20e+3}"""
```


Under Fable, `JSON.parse |> Data.ofJsonValue` is also available when native JavaScript semantics are acceptable.
That route has already discarded duplicate fields and the original spelling of number tokens; use `Json.parseData`
when either distinction matters.

## Configuration

Configuration keys use `:`-separated sections. A section with only canonical ASCII decimal child names
(`0` or `[1-9][0-9]*`) becomes a collection:

```fsharp
let raw =
    Data.ofConfiguration
        [ "name", "Ada Lovelace"
          "address:city", "London"
          "contacts:0:kind", "email"
          "contacts:0:value", "ada@example.com" ]
```


Later pairs override earlier ones at the same path, matching .NET configuration layering: a repeated key keeps its
last value, and a later scalar or section replaces the earlier shape at that key. A section path written after a
scalar at the same key replaces the scalar with the section, and a later scalar at a section key replaces the section;
the one exception is a section key whose value is null, as `IConfiguration.AsEnumerable()` emits alongside a section's
children — that never overrides the children, so real layered `IConfiguration` output round-trips directly. Collections
come from numeric segments, never from repetition; repeated names as multi-value input is a wire convention that
belongs to `ofNameValues`. Numeric segments need not be contiguous: a sparse set such as `contacts:0` and `contacts:2`
becomes a three-item list with `Data.Null` at the missing index 1.

## Environment Variables

Environment variable names use `__` between sections, the Microsoft.Extensions.Configuration convention. The adapter
takes the pairs rather than reading the environment, and an optional prefix selects and strips the application's
variables:

```fsharp isolated
open Reified

let raw =
    [ "SHOP__name", "Ada Lovelace"; "SHOP__address__city", "London"; "HOME", "/home/ada" ]
    |> Data.ofEnvironmentWith (fun options -> { options with Prefix = Some "SHOP__" })
```


Names keep their case, so a field matches `SHOP__name` but not `SHOP__NAME` unless it declares `alias "NAME"`.
Sections use the same canonical decimal index grammar as configuration keys. Names such as `00`, `+1`,
`0x10` and `1_0` remain distinct object fields; they never alias an index. Both adapters default to 10,000 items
per list, 100,000 output nodes (including root, containers and null gaps) and 64 path segments. Exceeding a limit
throws `ArgumentException` before oversized expansion. Use `Data.ofConfigurationWith` or the environment options'
`Limits` record to configure positive bounds explicitly. See
[environment variables](/data/converting-data.html#environment-variables) for ordering/conflicts and
[conversion limits](/data/converting-data.html#conversion-limits) for trusted-input overrides.

## Layered Sources

`Data.mergeAll` combines sources before one parse, with later layers winning field by field. The schema then reports
every failure, whichever layer supplied the bad value:

```fsharp isolated
open Reified

let layered =
    Data.mergeAll
        [ Json.parseData """{"name":"Ada Lovelace","address":{"city":"Paris"},"contacts":[]}"""
          Data.ofEnvironment [ "address__city", "London" ]
          Data.ofCliArgs [ "--name"; "Ada" ] ]
// name "Ada" from the command line, address.city "London" from the environment, contacts [] from the file
```


A later `null` overrides rather than removes, and a later list replaces the earlier list whole. See
[merge layered sources](/data/converting-data.html#merge-layered-sources).

## One Parse For All Of Them

Continuing with the configuration input above:

```fsharp
let parsed = Schema.parseRetainingInput customerSchema raw

match parsed.Result with
| Ok customer -> ignore customer
| Error diagnostics ->
    // same paths for every source:
    parsed.ErrorsFor "contacts[0].value" |> List.map SchemaError.render |> ignore
```


## From C#

Consume-don't-author: F# declares the schema, C# parses and reads diagnostics. Most adapters take plain .NET types
already, `ofNameValueCollection` takes `NameValueCollection`, `ofCliArgs` and `ofData` take ordinary
sequences and values, and call as plain static methods. `ofMap` and `ofConfiguration` take F#-only types (`Map` and
a sequence of F# tuples), so use their C#-friendly equivalents instead:

```csharp
using Reified.Schema;

// ofMap's C# equivalent, takes IDictionary<string, string> instead of an F# Map:
Data raw = DataModule.ofDictionary(new Dictionary<string, string> { ["name"] = "Ada Lovelace" });

// ofConfiguration's C# equivalent, takes the pairs IConfiguration.AsEnumerable() already returns:
Data fromConfig = DataModule.ofConfigurationPairs(configuration.AsEnumerable());

RetainedParseResult<Customer, SchemaError> parsed = Schema.parseRetainingInput(customerSchema, raw);

if (parsed.IsValid)
{
    Customer customer = parsed.Value;
}
else
{
    var errors = parsed.Errors; // FSharpList<Diagnostic<SchemaError>>
}
```


`Schema.parseWith` takes an F# function for its `configure` parameter, which C# cannot pass a lambda to directly. Use
`Schema.parseWithOptions`, which takes a `Func<Options, Options>` instead:

```csharp
var parsed = Schema.parseWithOptions(o => Schema.constructorErrorAt("end", o), dateRangeSchema, raw);
```
