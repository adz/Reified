namespace Reified.Tests

open System
open Reified
open Reified.SchemaDSL
open Xunit

module ConfigurationExtrasTests =
    type Db = { Port: int }
    type Settings = { Db: Db }
    let private settingsSchema () =
        let nested = schema<Db> { fieldAs "port" _.Port; construct (fun port -> { Port = port }) }
        schema<Settings> { fieldAs "db" _.Db { withSchema nested }; construct (fun db -> { Db = db }) }

    [<Fact>]
    let ``case matching is opt in at every model depth`` () =
        let input = Data.ofEnvironment ["DB__PORT", "5432"]
        Assert.True(Schema.parse (settingsSchema ()) input |> Result.isError)
        Assert.Equal(Ok { Db = { Port = 5432 } }, Schema.parseWith Schema.ignoreKeyCase (settingsSchema ()) input)

    [<Fact>]
    let ``case collisions deep merge in first seen order with last value winning`` () =
        let input = Data.Object ["DB", Data.Object ["PORT", Data.Text "1"]; "db", Data.Object ["port", Data.Text "2"]]
        Assert.Equal(Ok { Db = { Port = 2 } }, Schema.parseWith Schema.ignoreKeyCase (settingsSchema ()) input)

    [<Fact>]
    let ``case aware merging preserves three layer precedence`` () =
        let input = [Data.ofConfiguration ["db:port", "1"]; Data.ofEnvironment ["DB__PORT", "2"]; Data.ofCliArgs ["--db:port=3"]]
                    |> Data.mergeAllWith StringComparer.OrdinalIgnoreCase
        Assert.Equal(Ok { Db = { Port = 3 } }, Schema.parseWith Schema.ignoreKeyCase (settingsSchema ()) input)

    [<Fact>]
    let ``case ambiguous schema field definitions are rejected`` () =
        let definitions = schema<int * int> { fieldAs "port" fst; fieldAs "PORT" snd; construct (fun a b -> a, b) }
        Assert.True(Schema.parseWith Schema.ignoreKeyCase definitions (Data.Object ["port", Data.Text "1"]) |> Result.isError)
