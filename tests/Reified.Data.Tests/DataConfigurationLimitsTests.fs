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
