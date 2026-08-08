namespace Reified.Tests

open Reified.Schema

[<RequireQualifiedAccess>]
type PathSegment =
    | Key of string
    | Index of int
    | Name of string

[<RequireQualifiedAccess>]
module TestPath =
    let fromLegacy segments =
        segments
        |> List.fold (fun path segment ->
            let next =
                match segment with
                | PathSegment.Key key
                | PathSegment.Name key -> SchemaPath.key key
                | PathSegment.Index index -> SchemaPath.index index

            SchemaPath.append path next) SchemaPath.root
