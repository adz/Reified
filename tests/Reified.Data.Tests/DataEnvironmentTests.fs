namespace Reified.Tests

open System
open Reified
open Swensen.Unquote
open Xunit

module DataEnvironmentTests =
    [<Fact>]
    let ``double underscore separates sections and every leaf is text`` () =
        let input =
            Data.ofEnvironment [ "Port", "8080"; "Logging__Level__Default", "Debug"; "Logging__Console", "true" ]

        test
            <@
                input =
                    Data.Object
                        [ "Logging",
                          Data.Object
                              [ "Console", Data.Text "true"
                                "Level", Data.Object [ "Default", Data.Text "Debug" ] ]
                          "Port", Data.Text "8080" ]
            @>

    [<Fact>]
    let ``a prefix selects variables ordinally and is stripped`` () =
        let input =
            [ "MYAPP_Port", "8080"; "PATH", "/usr/bin"; "myapp_Port", "1"; "MYAPP_Db__Host", "db" ]
            |> Data.ofEnvironmentWith (fun options -> { options with Prefix = Some "MYAPP_" })

        test <@ input = Data.Object [ "Db", Data.Object [ "Host", Data.Text "db" ]; "Port", Data.Text "8080" ] @>

    [<Fact>]
    let ``the separator is configurable`` () =
        let input =
            Data.ofEnvironmentWith (fun options -> { options with Separator = "_" }) [ "DB_HOST", "db"; "DB_PORT", "5432" ]

        test <@ input = Data.Object [ "DB", Data.Object [ "HOST", Data.Text "db"; "PORT", Data.Text "5432" ] ] @>

    [<Fact>]
    let ``names keep their case, so differently cased names are different fields`` () =
        let input = Data.ofEnvironment [ "PORT", "1"; "Port", "2" ]

        test <@ input = Data.Object [ "PORT", Data.Text "1"; "Port", Data.Text "2" ] @>

    [<Fact>]
    let ``numeric segments become list indexes as in ofConfiguration`` () =
        let input =
            Data.ofEnvironment [ "Servers__1__Host", "b"; "Servers__0__Host", "a" ]

        test
            <@
                input =
                    Data.Object
                        [ "Servers",
                          Data.List [ Data.Object [ "Host", Data.Text "a" ]; Data.Object [ "Host", Data.Text "b" ] ] ]
            @>

        test <@ input = Data.ofConfiguration [ "Servers:1:Host", "b"; "Servers:0:Host", "a" ] @>

    [<Fact>]
    let ``the root is always an object, even when every name is numeric`` () =
        test <@ Data.ofEnvironment [ "0", "a"; "1", "b" ] = Data.Object [ "0", Data.Text "a"; "1", Data.Text "b" ] @>
        test <@ Data.ofEnvironment [] = Data.Object [] @>

    [<Fact>]
    let ``a section beats a value at the same name whatever the input order`` () =
        let expected = Data.Object [ "A", Data.Object [ "B", Data.Text "2" ] ]

        test <@ Data.ofEnvironment [ "A", "1"; "A__B", "2" ] = expected @>
        test <@ Data.ofEnvironment [ "A__B", "2"; "A", "1" ] = expected @>

    [<Fact>]
    let ``a repeated name keeps its last value`` () =
        test <@ Data.ofEnvironment [ "A", "1"; "B", "x"; "A", "2" ] = Data.Object [ "A", Data.Text "2"; "B", Data.Text "x" ] @>

    [<Fact>]
    let ``names with an empty segment cannot address a path and are skipped`` () =
        let input =
            Data.ofEnvironment
                [ "__CF_USER_TEXT_ENCODING", "0x1F5"
                  "A____B", "1"
                  "TRAILING__", "1"
                  "", "1"
                  "Kept", "yes" ]

        test <@ input = Data.Object [ "Kept", Data.Text "yes" ] @>

        let exactPrefix =
            Data.ofEnvironmentWith (fun options -> { options with Prefix = Some "MYAPP_" }) [ "MYAPP_", "1" ]

        test <@ exactPrefix = Data.Object [] @>

    [<Fact>]
    let ``invalid options and null input are rejected`` () =
        raises<ArgumentException> <@ Data.ofEnvironmentWith (fun options -> { options with Separator = "" }) [] @>
        raises<ArgumentException> <@ Data.ofEnvironmentWith (fun options -> { options with Prefix = Some "" }) [] @>
        raises<ArgumentNullException> <@ Data.ofEnvironment (Unchecked.defaultof<seq<string * string>>) @>
        raises<ArgumentNullException> <@ Data.ofEnvironment [ null, "1" ] @>
