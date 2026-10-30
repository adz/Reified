namespace Reified.Tests

open System
open Reified
open Swensen.Unquote
open Xunit

module DataMergeTests =
    // Seeded generation keeps every property run reproducible. Keys "a", "b", and "c" always hold objects and
    // keys "x", "y", and "z" never do, so generated layers agree on which paths are objects — the condition under
    // which merge is associative (see the counterexample test below).
    let private genLeaf (random: Random) =
        match random.Next 6 with
        | 0 -> Data.Null
        | 1 -> Data.Text("t" + string (random.Next 4))
        | 2 -> Data.Number(string (random.Next 4))
        | 3 -> Data.Bool(random.Next 2 = 0)
        | 4 -> Data.List []
        | _ -> Data.List [ for _ in 1 .. random.Next(1, 3) -> Data.Text("i" + string (random.Next 3)) ]

    let rec private genObject (random: Random) depth =
        let fields =
            [ for key in [ "a"; "b"; "c" ] do
                  if depth < 3 && random.Next 3 = 0 then
                      key, genObject random (depth + 1)
              for key in [ "x"; "y"; "z" ] do
                  if random.Next 2 = 0 then
                      key, genLeaf random ]
            |> List.sortBy (fun _ -> random.Next())

        Data.Object fields

    let private forAllLayers count (property: Data -> Data -> Data -> unit) =
        let random = Random 20261031

        for _ in 1..count do
            property (genObject random 0) (genObject random 0) (genObject random 0)

    let rec private leaves (prefix: string list) (value: Data) =
        match value with
        | Data.Object fields -> fields |> List.collect (fun (name, item) -> leaves (prefix @ [ name ]) item)
        | leaf -> [ prefix, leaf ]

    let rec private at (path: string list) (value: Data) =
        match path, value with
        | [], found -> Some found
        | name :: rest, Data.Object fields ->
            fields |> List.tryFind (fst >> (=) name) |> Option.bind (snd >> at rest)
        | _ -> None

    [<Fact>]
    let ``merge is associative when layers agree on which paths are objects`` () =
        forAllLayers 300 (fun a b c -> test <@ Data.merge (Data.merge a b) c = Data.merge a (Data.merge b c) @>)

    [<Fact>]
    let ``an empty object is a left and right identity for objects`` () =
        forAllLayers 300 (fun a _ _ ->
            test <@ Data.merge (Data.Object []) a = a @>
            test <@ Data.merge a (Data.Object []) = a @>)

    [<Fact>]
    let ``every right-side leaf wins and every left-only leaf survives`` () =
        forAllLayers 300 (fun a b _ ->
            let merged = Data.merge a b

            for path, value in leaves [] b do
                test <@ at path merged = Some value @>

            for path, value in leaves [] a do
                // A leaf survives unless the right side supplies something at its path.
                if (at path b).IsNone then
                    test <@ at path merged = Some value @>)

    [<Fact>]
    let ``mergeAll folds left and merges into an empty object`` () =
        forAllLayers 100 (fun a b c ->
            test <@ Data.mergeAll [ a; b; c ] = Data.merge (Data.merge a b) c @>)

        test <@ Data.mergeAll [] = Data.Object [] @>
        test <@ Data.mergeAll [ Data.Text "only" ] = Data.Text "only" @>

    [<Fact>]
    let ``right wins on scalars and null overrides rather than removes`` () =
        test <@ Data.merge (Data.Text "a") (Data.Number "1") = Data.Number "1" @>
        test <@ Data.merge (Data.Object [ "port", Data.Text "80" ]) (Data.Object [ "port", Data.Text "8080" ]) =
                    Data.Object [ "port", Data.Text "8080" ] @>

        test
            <@
                Data.merge
                    (Data.Object [ "proxy", Data.Object [ "host", Data.Text "p" ]; "port", Data.Text "80" ])
                    (Data.Object [ "proxy", Data.Null ]) =
                    Data.Object [ "proxy", Data.Null; "port", Data.Text "80" ]
            @>

    [<Fact>]
    let ``nested objects merge by key preserving first-seen order and appending new keys`` () =
        let earlier =
            Data.Object
                [ "name", Data.Text "app"
                  "database", Data.Object [ "host", Data.Text "localhost"; "port", Data.Number "5432" ] ]

        let later =
            Data.Object
                [ "logging", Data.Text "debug"
                  "database", Data.Object [ "pool", Data.Number "10"; "host", Data.Text "db.internal" ] ]

        test
            <@
                Data.merge earlier later =
                    Data.Object
                        [ "name", Data.Text "app"
                          "database",
                          Data.Object
                              [ "host", Data.Text "db.internal"
                                "port", Data.Number "5432"
                                "pool", Data.Number "10" ]
                          "logging", Data.Text "debug" ]
            @>

    [<Fact>]
    let ``lists replace instead of merging element-wise`` () =
        test
            <@
                Data.merge
                    (Data.Object [ "hosts", Data.List [ Data.Text "a"; Data.Text "b" ] ])
                    (Data.Object [ "hosts", Data.List [ Data.Text "c" ] ]) =
                    Data.Object [ "hosts", Data.List [ Data.Text "c" ] ]
            @>

    [<Fact>]
    let ``an object and a non-object replace each other`` () =
        test <@ Data.merge (Data.Object [ "a", Data.Text "1" ]) (Data.Text "x") = Data.Text "x" @>
        test <@ Data.merge (Data.Text "x") (Data.Object [ "a", Data.Text "1" ]) = Data.Object [ "a", Data.Text "1" ] @>

    [<Fact>]
    let ``a non-object between two objects makes grouping matter, and mergeAll applies layers in order`` () =
        let a = Data.Object [ "x", Data.Text "1" ]
        let b = Data.Text "reset"
        let c = Data.Object [ "y", Data.Text "2" ]

        test <@ Data.merge (Data.merge a b) c = Data.Object [ "y", Data.Text "2" ] @>
        test <@ Data.merge a (Data.merge b c) = Data.Object [ "x", Data.Text "1"; "y", Data.Text "2" ] @>
        test <@ Data.mergeAll [ a; b; c ] = Data.Object [ "y", Data.Text "2" ] @>

    [<Fact>]
    let ``repeated names merge into their first occurrence`` () =
        let input =
            Data.Object
                [ "a", Data.Object [ "x", Data.Text "1" ]
                  "b", Data.Text "kept"
                  "a", Data.Object [ "y", Data.Text "2" ] ]

        test
            <@
                Data.merge (Data.Object []) input =
                    Data.Object [ "a", Data.Object [ "x", Data.Text "1"; "y", Data.Text "2" ]; "b", Data.Text "kept" ]
            @>

    [<Fact>]
    let ``merge rejects a null list`` () =
        raises<ArgumentNullException> <@ Data.mergeAll (Unchecked.defaultof<Data list>) @>
