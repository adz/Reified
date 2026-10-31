namespace Reified.Tests

open System
open Reified
open Xunit

module DataCliNestingTests =
    [<Theory>]
    [<InlineData(":")>]
    [<InlineData("__")>]
    let ``CLI paths build bounded nested lists and objects`` separator =
        let actual = Data.ofCliArgs ["--Db" + separator + "Servers" + separator + "1" + separator + "Port=5432"; "input"]
        Assert.Equal(Data.Object ["Db", Data.Object ["Servers", Data.List [Data.Null; Data.Object ["Port", Data.Text "5432"]]]; "_", Data.List [Data.Text "input"]], actual)

    [<Fact>]
    let ``CLI equivalent separators preserve repeated option order`` () =
        let actual = Data.ofCliArgs ["--Db:Port"; "1"; "--Db__Port=2"]
        Assert.Equal(Data.Object ["Db", Data.Object ["Port", Data.List [Data.Text "1"; Data.Text "2"]]], actual)

    [<Fact>]
    let ``CLI expansion and depth use configuration defaults`` () =
        for key in ["X:10000"; "X__2147483647"; List.replicate 65 "X" |> String.concat ":"] do
            Assert.Throws<ArgumentException>(fun () -> Data.ofCliArgs ["--" + key + "=v"] |> ignore) |> ignore

    [<Fact>]
    let ``CLI literal numeric aliases retain both sections`` () =
        Assert.Equal(Data.Object ["X", Data.Object ["0", Data.Object ["A", Data.Text "a"]; "00", Data.Object ["B", Data.Text "b"]]], Data.ofCliArgs ["--X:0:A=a"; "--X:00:B=b"])
