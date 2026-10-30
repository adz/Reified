namespace Reified.Tests

open System
open Reified
open Xunit

module DataConfigurationLimitsTests =
    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``default conversions reject sparse collection expansion beyond ten thousand items`` environment =
        let key = if environment then "X__10000" else "X:10000"
        let convert = if environment then Data.ofEnvironment else Data.ofConfiguration
        let error = Assert.Throws<ArgumentException>(fun () -> convert [key, "v"] |> ignore)
        Assert.Contains("X", error.Message)
        Assert.Contains("10000", error.Message)

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``default conversions reject paths deeper than sixty four segments`` environment =
        let separator = if environment then "__" else ":"
        let key = List.replicate 65 "X" |> String.concat separator
        let convert = if environment then Data.ofEnvironment else Data.ofConfiguration
        let error = Assert.Throws<ArgumentException>(fun () -> convert [key, "v"] |> ignore)
        Assert.Contains("64", error.Message)
        Assert.Contains("X", error.Message)

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``expanded node budget is shared by every nested collection and root field`` environment =
        let separator = if environment then "__" else ":"
        let pairs = [for i in 0 .. 9 -> "X" + string i + separator + "Inner" + separator + "9999", "v"]
        let convert = if environment then Data.ofEnvironment else Data.ofConfiguration
        let error = Assert.Throws<ArgumentException>(fun () -> convert pairs |> ignore)
        Assert.Contains("100000", error.Message)
        Assert.Contains("Inner", error.Message)
