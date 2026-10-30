namespace Reified.Tests

open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL
open Swensen.Unquote
open Xunit

module LayeredConfigurationTests =
    type Database =
        { Host: string
          Port: int }

        static member Schema(_: Database) : Schema<Database> =
            schema<Database> {
                field _.Host { constraints [ present ] }
                field _.Port { constrain (Constraint.between 1 65535) }
                construct (fun host port -> { Host = host; Port = port })
            }

    type AppSettings =
        { Name: string
          Verbose: bool
          Database: Database }

    let private settingsSchema () =
        schema<AppSettings> {
            field _.Name
            field _.Verbose
            field _.Database
            construct (fun name verbose database -> { Name = name; Verbose = verbose; Database = database })
        }

    let private layers (environment: (string * string) list) (args: string list) =
        Data.mergeAll
            [ Json.parseData """{ "name": "orders", "verbose": false, "database": { "host": "localhost", "port": 5432 } }"""
              environment |> Data.ofEnvironmentWith (fun options -> { options with Prefix = Some "APP__" })
              Data.ofCliArgs args ]

    [<Fact>]
    let ``json, environment, and command-line layers decode into one typed record`` () =
        let input =
            layers [ "APP__database__host", "db.internal"; "HOME", "/home/ada" ] [ "--verbose" ]

        test
            <@
                Schema.parse (settingsSchema ()) input =
                    Ok
                        { Name = "orders"
                          Verbose = true
                          Database = { Host = "db.internal"; Port = 5432 } }
            @>

    [<Fact>]
    let ``bad values from different layers accumulate as path-addressed errors`` () =
        let input =
            layers [ "APP__database__port", "eighty" ] [ "--verbose=perhaps" ]

        match Schema.parse (settingsSchema ()) input with
        | Ok settings -> failwith ("Expected errors but parsed " + settings.Name)
        | Error errors ->
            let paths = SchemaErrors.toList errors |> List.map (fun issue -> SchemaPath.format issue.Path)
            test <@ paths = [ "database.port"; "verbose" ] @>
