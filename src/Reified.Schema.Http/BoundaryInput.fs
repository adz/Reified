namespace Reified.Schema.Http

open System
open Reified


/// <summary>Converts between <see cref="T:Reified.Data" /> and the name/value pairs of query strings and forms.</summary>
/// <remarks>
/// <para>
/// Adapters extract plain name/value pairs from their request type and the conversion rules live here, so every host
/// produces identical structured data for identical wire data.
/// </para>
/// <para>
/// Query strings and forms share one naming convention. Dotted names nest (<c>address.city</c>), numeric segments
/// index lists (<c>tags.0</c>, <c>tags.1</c>), and a repeated name collects its values into a list. Bracketed names read
/// the same, so input from ASP.NET MVC, Rails, PHP or the <c>qs</c> package parses as written: <c>address[city]</c> is
/// <c>address.city</c>, <c>tags[0]</c> is <c>tags.0</c>, and each <c>tags[]</c> takes the next index. A name that appears
/// once stays a scalar: only the schema knows which fields are lists, so a list that may hold one item should be
/// written with indexed names, as <c>toPairs</c> does. An empty value is left out, because a browser sends an empty
/// field as <c>name=</c>: an optional field left blank parses as absent, and a required one reports that it must be
/// present.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module BoundaryInput =
    [<RequireQualifiedAccess>]
    type private Node =
        | Values of string list
        | Fields of Map<string, Node>

    let rec private insert (node: Node) (segments: string list) (value: string) : Node =
        match segments, node with
        | [], Node.Values values -> Node.Values(values @ [ value ])
        | [], Node.Fields _ -> node
        | segment :: rest, Node.Fields fields ->
            let child =
                match fields.TryFind segment with
                | Some child -> child
                | None -> if rest.IsEmpty then Node.Values [] else Node.Fields Map.empty

            Node.Fields(fields.Add(segment, insert child rest value))
        | _ :: _, Node.Values _ -> node

    let rec private dataOf (node: Node) : Data =
        match node with
        | Node.Values [ value ] -> Data.Text value
        | Node.Values values -> values |> List.map Data.Text |> Data.List
        | Node.Fields fields ->
            let indexed =
                fields
                |> Map.toList
                |> List.map (fun (key, child) ->
                    match Int32.TryParse key with
                    | true, index -> Some(index, child)
                    | false, _ -> None)

            if not fields.IsEmpty && indexed |> List.forall Option.isSome then
                indexed
                |> List.choose id
                |> List.sortBy fst
                |> List.map (snd >> dataOf)
                |> Data.List
            else
                fields |> Map.toList |> List.map (fun (name, child) -> name, dataOf child) |> Data.Object

    /// The path a name stands for: <c>address.city</c> and <c>address[city]</c> are the same path, and a trailing
    /// <c>[]</c> is kept as an empty last segment. A name with an unclosed bracket splits on dots only.
    let private segmentsOf (name: string) : string list =
        let first = name.IndexOf '['

        if first < 0 then
            name.Split '.' |> List.ofArray
        else
            let rec rest (text: string) =
                if text = "" then
                    Some []
                elif text.StartsWith "[" then
                    match text.IndexOf ']' with
                    | -1 -> None
                    | close -> rest (text.Substring(close + 1)) |> Option.map (fun tail -> text.Substring(1, close - 1) :: tail)
                elif text.StartsWith "." then
                    let text = text.Substring 1

                    let stop =
                        match text.IndexOfAny [| '.'; '[' |] with
                        | -1 -> text.Length
                        | at -> at

                    rest (text.Substring stop) |> Option.map (fun tail -> text.Substring(0, stop) :: tail)
                else
                    None

            match rest (name.Substring first) with
            | Some tail -> List.ofArray (name.Substring(0, first).Split '.') @ tail
            | None -> name.Split '.' |> List.ofArray

    let private ofPairs (pairs: seq<string * string>) : Data =
        if isNull (box pairs) then
            nullArg (nameof pairs)

        // A trailing [] appends: each one takes the next index under its path, so tags[]=a reads as a one-item list.
        let appended (counts: Map<string list, int>) (segments: string list) =
            match List.rev segments with
            | "" :: parent when not parent.IsEmpty ->
                let parent = List.rev parent
                let index = counts.TryFind parent |> Option.defaultValue 0
                counts.Add(parent, index + 1), parent @ [ string index ]
            | _ -> counts, segments

        pairs
        |> Seq.fold
            (fun (node, counts) (name, value) ->
                if String.IsNullOrEmpty name || String.IsNullOrEmpty value then
                    node, counts
                else
                    let counts, segments = appended counts (segmentsOf name)
                    insert node segments value, counts)
            (Node.Fields Map.empty, Map.empty)
        |> fst
        |> dataOf

    /// <summary>Reads query-string pairs as structured data.</summary>
    /// <example><code>BoundaryInput.ofQuery [ "page", "2"; "terms.0", "red" ]
    /// // Data.Object [ "page", Data.Text "2"; "terms", Data.List [ Data.Text "red" ] ]</code></example>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="pairs" /> is null.</exception>
    let ofQuery (pairs: seq<string * string>) : Data = ofPairs pairs

    /// <summary>Reads posted form pairs as structured data.</summary>
    /// <example><code>BoundaryInput.ofForm [ "address.city", "London"; "tags", "vip"; "tags", "founder" ]
    /// // Data.Object [ "address", Data.Object [ "city", Data.Text "London" ]; "tags", Data.List [ ... ] ]</code></example>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="pairs" /> is null.</exception>
    let ofForm (pairs: seq<string * string>) : Data = ofPairs pairs

    /// <summary>Writes object-shaped data as the name/value pairs <c>ofQuery</c> and <c>ofForm</c> read back to the same data.</summary>
    /// <remarks>
    /// Nested fields get dotted names and list items get indexed names, so a one-item list still reads back as a list.
    /// Null values are left out. Write a model with <c>Json.toData</c> first.
    /// </remarks>
    /// <example><code>Json.toData codec { Page = 2; Terms = [ "red" ] } |> BoundaryInput.toPairs
    /// // [ "page", "2"; "terms.0", "red" ]</code></example>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="data" /> is not an object.</exception>
    let toPairs (data: Data) : (string * string) list =
        let rec walk (name: string) (data: Data) = [
            match data with
            | Data.Text value -> name, value
            | Data.Number token -> name, token
            | Data.Bool value -> name, (if value then "true" else "false")
            | Data.Null -> ()
            | Data.List items ->
                for index, item in List.indexed items do
                    yield! walk $"{name}.{index}" item
            | Data.Object fields ->
                for field, value in fields do
                    yield! walk $"{name}.{field}" value
        ]

        match data with
        | Data.Object fields ->
            [
                for name, value in fields do
                    yield! walk name value
            ]
        | Data.Null -> []
        | _ -> invalidArg (nameof data) "toPairs needs object-shaped data, such as a model written with Json.toData."

    /// <summary>Renders name/value pairs as an escaped query string, without the leading <c>?</c>.</summary>
    /// <example><code>BoundaryInput.queryString [ "q", "red car"; "page", "2" ] // "q=red%20car&amp;page=2"</code></example>
    let queryString (pairs: seq<string * string>) : string =
        pairs
        |> Seq.map (fun (name, value) -> Uri.EscapeDataString name + "=" + Uri.EscapeDataString value)
        |> String.concat "&"
