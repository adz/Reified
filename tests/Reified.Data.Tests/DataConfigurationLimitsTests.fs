namespace Reified.Tests

open System
open Reified
open Xunit

module DataConfigurationLimitsTests =
    let private withLimits environment (configure: DataConversionLimits -> DataConversionLimits) pairs =
        if environment then Data.ofEnvironmentWith (fun options -> { options with Limits = configure options.Limits }) pairs
        else Data.ofConfigurationWith configure pairs

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

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``trusted callers can explicitly raise the collection bound`` environment =
        let key = if environment then "X__10000" else "X:10000"
        let configured = withLimits environment (fun limits -> { limits with MaxCollectionLength = 10001 })
        match configured [key, "v"] with
        | Data.Object ["X", Data.List items] ->
            Assert.Equal(10001, items.Length)
            Assert.Equal(Data.Text "v", List.last items)
        | actual -> failwith (Data.Json.render actual)
        let over = if environment then "X__10001" else "X:10001"
        Assert.Throws<ArgumentException>(fun () -> configured [over, "v"] |> ignore) |> ignore

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``custom limits count root nodes list nodes and null gaps`` environment =
        let separator = if environment then "__" else ":"
        let pairs = ["A" + separator + "1", "a"; "B" + separator + "1", "b"]
        let configure limits = { limits with MaxCollectionLength = 2; MaxExpandedNodes = 7; MaxDepth = 2 }
        let expected = Data.Object ["A", Data.List [Data.Null; Data.Text "a"]; "B", Data.List [Data.Null; Data.Text "b"]]
        Assert.Equal(expected, withLimits environment configure pairs)
        Assert.Throws<ArgumentException>(fun () ->
            withLimits environment (fun limits -> { configure limits with MaxExpandedNodes = 6 }) pairs |> ignore) |> ignore
        Assert.Throws<ArgumentException>(fun () ->
            withLimits environment configure ["A" + separator + "B" + separator + "0", "v"] |> ignore) |> ignore

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``default bounds accept the largest collection and the deepest path`` environment =
        let separator = if environment then "__" else ":"
        let convert = if environment then Data.ofEnvironment else Data.ofConfiguration
        match convert ["X" + separator + "9999", "v"] with
        | Data.Object ["X", Data.List items] -> Assert.Equal(10000, items.Length)
        | actual -> failwith (Data.Json.render actual)
        convert [(List.replicate 64 "X" |> String.concat separator), "v"] |> ignore

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``index length arithmetic cannot overflow at Int32 maximum`` environment =
        let key = if environment then "X__2147483647" else "X:2147483647"
        let convert = if environment then Data.ofEnvironment else Data.ofConfiguration
        Assert.Throws<ArgumentException>(fun () -> convert [key, "v"] |> ignore) |> ignore

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``nonpositive or null conversion limits are rejected even for empty input`` environment =
        for invalid in [0; -1] do
            for configure in [ (fun limits -> { limits with MaxCollectionLength = invalid })
                               (fun limits -> { limits with MaxExpandedNodes = invalid })
                               (fun limits -> { limits with MaxDepth = invalid }) ] do
                Assert.Throws<ArgumentException>(fun () -> withLimits environment configure [] |> ignore) |> ignore
        Assert.Throws<ArgumentNullException>(fun () ->
            withLimits environment (fun _ -> Unchecked.defaultof<DataConversionLimits>) [] |> ignore) |> ignore
