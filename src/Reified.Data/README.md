# Reified.Data

`Reified.Data` builds, patches, compares, and tests structured data. Its data model maps to JSON and supports fixtures,
configuration, command-line input, form values, and events.

```fsharp
open Reified
open Reified.DataDSL

let customer =
    data [
        "name" => "Ada"
        "address" => [
            "city" => "Adelaide"
            "postcode" => 5000
        ]
        "roles" => [ "author" ]
    ]

let promoted =
    customer
    |> Data.patch [
        replace "address.postcode" 5001
        append "roles" "admin"
    ]

promoted
|> matching [
    at "address" (containing [ "postcode" => 5001 ])
    at "roles" (containingItems [ "admin" ])
    absent "error"
]
```

`promoted` contains postcode `5001` and roles `["author","admin"]`; `customer` remains unchanged. The final
`matching` expression returns `unit` because every selected observation succeeds.

`Data.Number` preserves its lexical token. `Data.Object` preserves field order and duplicate names. Exact comparison,
patching, paths, and matching preserve those distinctions.

Install it with `dotnet add package Reified.Data`.

`Data.Json.render` writes JSON on .NET and Fable. For portable JSON text parsing, add `Reified.Schema` and use
`Json.parseData`. On .NET 8+, existing `JsonElement` and `JsonDocument` values can be copied with
`Data.ofJsonElement` and `Data.ofJsonDocument`; under Fable, pass a native `JSON.parse` result to `Data.ofJsonValue`.

## Configuration from layered sources

A configuration file, the environment, and the command line each become a `Data` tree. `Data.mergeAll` layers them,
later sources winning field by field, and a schema from `Reified.Schema` decodes the result into a typed record.
Environment and CLI leaves are text such as `"5432"`, which the schema parses into the field's type, and every bad
value is reported with its path, whichever layer supplied it.

```fsharp
open Reified
open Reified.SchemaDSL

type Database =
    { Host: string
      Port: int }

    static member Schema(_: Database) : Schema<Database> =
        schema<Database> {
            field _.Host
            field _.Port
            construct (fun host port -> { Host = host; Port = port })
        }

type Settings = { Verbose: bool; Database: Database }

let settingsSchema =
    schema<Settings> {
        field _.Verbose
        field _.Database
        construct (fun verbose database -> { Verbose = verbose; Database = database })
    }

let load fileText (environment: seq<string * string>) (argv: string list) =
    Data.mergeAll
        [ Json.parseData fileText
          environment |> Data.ofEnvironmentWith (fun options -> { options with Prefix = Some "APP__" })
          Data.ofCliArgs argv ]
    |> Schema.parse settingsSchema

load """{ "verbose": false, "database": { "host": "localhost", "port": 5432 } }"""
     [ "APP__database__host", "db.internal"; "HOME", "/home/ada" ]
     [ "--verbose" ]
// Ok { Verbose = true; Database = { Host = "db.internal"; Port = 5432 } }

load """{ "verbose": false, "database": { "host": "localhost", "port": 5432 } }"""
     [ "APP__database__port", "eighty" ]
     [ "--verbose=perhaps" ]
// Error: database.port and verbose, both reported
```

On .NET, pass `Environment.GetEnvironmentVariables()` as name/value pairs; under Fable, pass the entries of
`process.env`. `Data.ofEnvironment` itself performs no I/O. Objects merge recursively; lists and scalars replace; a
later `null` overrides the key to `Data.Null` rather than removing it.

Environment and configuration sections become lists only when every child name is a canonical ASCII decimal index
(`0` or `[1-9][0-9]*`). Names such as `00`, `+1`, `0x10` and `1_0` remain object fields, with no aliasing or lost
subtrees. Recognition is identical on .NET and Fable.

CLI option names now nest on `:` and `__` by default (`--Db:Port=5432` / `--Db__Port 5432`); dots remain literal.
Repeated equivalent paths collect values in argument order and positionals stay under `_`. Sections beat scalar
prefixes. CLI sparse, repeated-option and positional lists share the default conversion bounds.

For mixed-case sources, fold layers with `Data.mergeAllWith Data.ordinalIgnoreCase`, then parse with
`Schema.parseWith Schema.ignoreKeyCase`. First name spelling/position survive and later equivalent names merge
recursively. Parsing alone cannot recover precedence lost by an earlier case-sensitive merge. Default merge and
parse behavior stay case-sensitive; map keys, tag values, prefix filters and JSON codec names are unchanged.

Conversions default to 10,000 items per list, 100,000 output nodes across the entire result (root, containers,
leaves and null gaps), and 64 input path segments. Oversized expansion throws `ArgumentException` with a path and
limit. Configure positive bounds explicitly with `Data.ofConfigurationWith` or `DataEnvironmentOptions.Limits`
when larger trusted configurations are required. Hosts must still bound input bytes and pair counts.

See the [Reified.Data documentation](https://adz.github.io/Reified/data/) for tutorials, partial matching, case
matrices, JSON conversion, and the complete API reference.
