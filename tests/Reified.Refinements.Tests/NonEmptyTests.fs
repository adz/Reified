namespace Reified.Refinements.Tests

open Reified.Refinements
open Swensen.Unquote
open Xunit

/// Proves the operations the type exists to make total, and the closure properties
/// consumers are entitled to rely on.
module NonEmptyTests =

    let private sample () = NonEmpty(1, [ 2; 3; 4 ])

    [<Fact>]
    let ``the case is public, so callers can pattern match and construct without a Result`` () =
        let (NonEmpty(head, tail)) = sample ()
        test <@ head = 1 @>
        test <@ tail = [ 2; 3; 4 ] @>
        test <@ NonEmpty(1, [ 2 ]) |> NonEmptyList.toList = [ 1; 2 ] @>
        test <@ NonEmptyList.singleton 9 |> NonEmptyList.toList = [ 9 ] @>

    [<Fact>]
    let ``head, last, min, max, and reduce are total with no option or exception`` () =
        let input = sample ()
        test <@ NonEmptyList.head input = 1 @>
        test <@ NonEmptyList.last input = 4 @>
        test <@ NonEmptyList.min input = 1 @>
        test <@ NonEmptyList.max input = 4 @>
        test <@ NonEmptyList.reduce (+) input = 10 @>

    [<Fact>]
    let ``last and reduce hold on the single-item case`` () =
        let single = NonEmptyList.singleton 7
        test <@ NonEmptyList.last single = 7 @>
        test <@ NonEmptyList.reduce (+) single = 7 @>
        test <@ NonEmptyList.length single = 1 @>

    [<Fact>]
    let ``map, rev, sort, append, and distinct are closed over non-emptiness`` () =
        let input = sample ()
        test <@ NonEmptyList.map ((*) 2) input |> NonEmptyList.toList = [ 2; 4; 6; 8 ] @>
        test <@ NonEmptyList.rev input |> NonEmptyList.toList = [ 4; 3; 2; 1 ] @>
        test <@ NonEmptyList.sort (NonEmpty(3, [ 1; 2 ])) |> NonEmptyList.toList = [ 1; 2; 3 ] @>
        test <@ NonEmptyList.append input (NonEmptyList.singleton 5) |> NonEmptyList.toList = [ 1; 2; 3; 4; 5 ] @>
        test <@ NonEmptyList.distinct (NonEmpty(1, [ 1; 2 ])) |> NonEmptyList.toList = [ 1; 2 ] @>

    [<Fact>]
    let ``map preserves identity and composition`` () =
        let input = sample ()
        test <@ NonEmptyList.map id input = input @>

        let f value = value + 1
        let g value = value * 3
        test <@ NonEmptyList.map (f >> g) input = (input |> NonEmptyList.map f |> NonEmptyList.map g) @>

    [<Fact>]
    let ``rev is its own inverse and append is associative`` () =
        let input = sample ()
        test <@ input |> NonEmptyList.rev |> NonEmptyList.rev = input @>

        let a = NonEmpty(1, [])
        let b = NonEmpty(2, [ 3 ])
        let c = NonEmpty(4, [])
        test <@ NonEmptyList.append (NonEmptyList.append a b) c = NonEmptyList.append a (NonEmptyList.append b c) @>

    [<Fact>]
    let ``narrowing operations admit emptiness honestly`` () =
        let input = sample ()
        test <@ NonEmptyList.filter (fun value -> value > 10) input = [] @>
        test <@ NonEmptyList.tryFilter (fun value -> value > 10) input = None @>
        test <@ NonEmptyList.tryFilter (fun value -> value > 2) input |> Option.map NonEmptyList.toList = Some [ 3; 4 ] @>

    [<Fact>]
    let ``ofList returns None for an empty source and Some otherwise`` () =
        test <@ NonEmptyList.ofList ([]: int list) = None @>
        test <@ NonEmptyList.ofList [ 1 ] |> Option.map NonEmptyList.toList = Some [ 1 ] @>

    [<Fact>]
    let ``traverseResult accumulates every failure rather than stopping at the first`` () =
        let mapping value =
            if value % 2 = 0 then Ok value else Error [ $"odd:{value}" ]

        test <@ NonEmptyList.traverseResult mapping (NonEmpty(2, [ 4 ])) |> Result.map NonEmptyList.toList = Ok [ 2; 4 ] @>
        test <@ NonEmptyList.traverseResult mapping (NonEmpty(1, [ 2; 3 ])) = Error [ "odd:1"; "odd:3" ] @>

    [<Fact>]
    let ``traverseOption succeeds only when every item is present`` () =
        test <@ NonEmptyList.traverseOption Some (NonEmpty(1, [ 2 ])) |> Option.map NonEmptyList.toList = Some [ 1; 2 ] @>
        test <@ NonEmptyList.traverseOption (fun value -> if value > 1 then Some value else None) (NonEmpty(1, [ 2 ])) = None @>

    [<Fact>]
    let ``the refinement still reports the same failure as before the rewrite`` () =
        test <@ NonEmptyList.create ([]: int list) |> Result.isError @>
        test <@ NonEmptyList.create [ 1; 2 ] |> Result.map NonEmptyList.toList = Ok [ 1; 2 ] @>

    [<Fact>]
    let ``zip truncates to the shorter input rather than raising`` () =
        // List.zip raises on a length mismatch, which would make zip partial.
        raises<System.ArgumentException> <@ List.zip [ 1; 2 ] [ 9 ] @>

        let longer = NonEmpty(1, [ 2; 3 ])
        let shorter = NonEmpty(9, [ 8 ])
        test <@ NonEmptyList.zip longer shorter |> NonEmptyList.toList = [ (1, 9); (2, 8) ] @>
        test <@ NonEmptyList.zip shorter longer |> NonEmptyList.toList = [ (9, 1); (8, 2) ] @>
        test <@ NonEmptyList.zip longer longer |> NonEmptyList.toList = [ (1, 1); (2, 2); (3, 3) ] @>

    [<Fact>]
    let ``non-empty arrays keep contiguous storage while making head and reduce total`` () =
        let input = NonEmptyArray.create [ 3; 1; 2 ] |> Result.defaultWith (failwithf "%A")
        test <@ NonEmptyArray.head input = 3 @>
        test <@ NonEmptyArray.last input = 2 @>
        test <@ NonEmptyArray.max input = 3 @>
        test <@ NonEmptyArray.reduce (+) input = 6 @>
        test <@ NonEmptyArray.sort input |> NonEmptyArray.toArray = [| 1; 2; 3 |] @>
        test <@ NonEmptyArray.create ([]: int list) |> Result.isError @>

    [<Fact>]
    let ``non-empty arrays and lists convert without losing the invariant`` () =
        let list = NonEmpty(1, [ 2; 3 ])
        test <@ list |> NonEmptyArray.ofNonEmptyList |> NonEmptyArray.toNonEmptyList = list @>

    [<Fact>]
    let ``the returned array is a copy, so mutation cannot break the invariant`` () =
        let input = NonEmptyArray.singleton 1
        let copy = NonEmptyArray.toArray input
        copy[0] <- 99
        test <@ NonEmptyArray.head input = 1 @>

    [<Fact>]
    let ``non-empty arrays traverse and accumulate like non-empty lists`` () =
        let input = NonEmptyArray.create [ 2; 4 ] |> Result.defaultWith (failwithf "%A")
        let mapping value = if value % 2 = 0 then Ok value else Error [ $"odd:{value}" ]

        test <@ NonEmptyArray.traverseResult mapping input |> Result.map NonEmptyArray.toArray = Ok [| 2; 4 |] @>

        let mixed = NonEmptyArray.create [ 1; 2; 3 ] |> Result.defaultWith (failwithf "%A")
        test <@ NonEmptyArray.traverseResult mapping mixed = Error [ "odd:1"; "odd:3" ] @>
        test <@ NonEmptyArray.traverseOption Some input |> Option.map NonEmptyArray.toArray = Some [| 2; 4 |] @>

    [<Fact>]
    let ``non-empty array closed operations preserve non-emptiness`` () =
        let input = NonEmptyArray.create [ 1; 2; 3 ] |> Result.defaultWith (failwithf "%A")

        test <@ NonEmptyArray.consTo 0 input |> NonEmptyArray.toArray = [| 0; 1; 2; 3 |] @>
        test <@ NonEmptyArray.distinct (NonEmptyArray.create [ 1; 1; 2 ] |> Result.defaultWith (failwithf "%A"))
                    |> NonEmptyArray.toArray = [| 1; 2 |] @>
        test <@ NonEmptyArray.sortDescending input |> NonEmptyArray.toArray = [| 3; 2; 1 |] @>
        test <@ NonEmptyArray.concat (NonEmptyArray.create [ input; input ] |> Result.defaultWith (failwithf "%A"))
                    |> NonEmptyArray.length = 6 @>
        test <@ NonEmptyArray.reduceBack (-) input = 2 @>
        test <@ NonEmptyArray.contains 2 input @>

    [<Fact>]
    let ``non-empty array zip truncates rather than raising`` () =
        let longer = NonEmptyArray.create [ 1; 2; 3 ] |> Result.defaultWith (failwithf "%A")
        let shorter = NonEmptyArray.create [ 9 ] |> Result.defaultWith (failwithf "%A")

        raises<System.ArgumentException> <@ Array.zip [| 1; 2 |] [| 9 |] @>
        test <@ NonEmptyArray.zip longer shorter |> NonEmptyArray.toArray = [| (1, 9) |] @>



    [<Fact>]
    let ``groupBy keeps every group non-empty, because a group only exists if filled`` () =
        let grouped = NonEmpty(1, [ 2; 3; 4; 5 ]) |> NonEmptyList.groupBy (fun value -> value % 2)

        test <@ Map.count grouped = 2 @>
        test <@ grouped[1] |> NonEmptyList.toList = [ 1; 3; 5 ] @>
        test <@ grouped[0] |> NonEmptyList.toList = [ 2; 4 ] @>
        // The payoff: max on a group needs no option.
        test <@ grouped[1] |> NonEmptyList.max = 5 @>

    [<Fact>]
    let ``chunkBySize keeps both the outer list and every chunk non-empty`` () =
        let chunks = NonEmpty(1, [ 2; 3; 4; 5 ]) |> NonEmptyList.chunkBySize 2

        test <@ chunks |> NonEmptyList.toList |> List.map NonEmptyList.toList = [ [ 1; 2 ]; [ 3; 4 ]; [ 5 ] ] @>
        test <@ chunks |> NonEmptyList.head |> NonEmptyList.head = 1 @>

        // List.chunkBySize raises on zero; a size below one is treated as one instead.
        raises<System.ArgumentException> <@ List.chunkBySize 0 [ 1 ] @>
        test <@ NonEmptyList.chunkBySize 0 (NonEmpty(1, [ 2 ])) |> NonEmptyList.length = 2 @>

    [<Fact>]
    let ``an admitted array cannot be mutated through the caller's reference`` () =
        let source = [| 1; 2 |]
        let admitted = Refinement.create (NonEmptyArray.refinement ()) source |> Result.defaultWith (failwithf "%A")
        source[0] <- 99
        test <@ NonEmptyArray.head admitted = 1 @>

    [<Fact>]
    let ``sum, sumBy, average, and averageBy match the List versions without the empty branch`` () =
        let input = NonEmpty(1m, [ 2m; 3m; 4m ])
        test <@ NonEmptyList.sum input = 10m @>
        test <@ NonEmptyList.sumBy (fun value -> value * 2m) input = 20m @>
        test <@ NonEmptyList.average input = 2.5m @>
        test <@ NonEmptyList.averageBy (fun value -> value * 2m) input = 5m @>

        // List.average raises on the empty list; there is no such input here.
        raises<System.ArgumentException> <@ List.average ([]: decimal list) @>
        test <@ NonEmptyList.average (NonEmptyList.singleton 7m) = 7m @>

    [<Fact>]
    let ``choose narrows to a standard list, and tryChoose reports the empty case`` () =
        let input = sample ()
        let even value = if value % 2 = 0 then Some value else None
        test <@ NonEmptyList.choose even input = [ 2; 4 ] @>
        test <@ NonEmptyList.tryChoose even input |> Option.map NonEmptyList.toList = Some [ 2; 4 ] @>
        test <@ NonEmptyList.tryChoose (fun _ -> None: int option) input = None @>
        test <@ NonEmptyList.tryPick even input = Some 2 @>

    [<Fact>]
    let ``countBy gives a map whose every count is at least one`` () =
        let counts = NonEmpty(1, [ 2; 3; 4; 5 ]) |> NonEmptyList.countBy (fun value -> value % 2)
        test <@ counts[1] = 3 @>
        test <@ counts[0] = 2 @>
        test <@ counts |> Map.forall (fun _ count -> count >= 1) @>

    [<Fact>]
    let ``scan is non-empty by construction, because it always emits the seed`` () =
        test <@ sample () |> NonEmptyList.scan (+) 0 |> NonEmptyList.toList = [ 0; 1; 3; 6; 10 ] @>
        test <@ NonEmptyList.scanBack (+) (sample ()) 0 |> NonEmptyList.toList = [ 10; 9; 7; 4; 0 ] @>

    [<Fact>]
    let ``item, truncate, skip, init, and replicate are total where the List versions raise`` () =
        let input = sample ()
        test <@ NonEmptyList.item 2 input = 3 @>
        test <@ NonEmptyList.item 99 input = 4 @> // clamped, because there is always a last item
        test <@ NonEmptyList.tryItem 99 input = None @>

        raises<System.ArgumentException> <@ List.skip 99 [ 1 ] @>
        test <@ NonEmptyList.skip 99 input = [] @>

        // List.truncate 0 empties; the refined version keeps the head.
        test <@ List.truncate 0 [ 1 ] = [] @>
        test <@ NonEmptyList.truncate 0 input |> NonEmptyList.toList = [ 1 ] @>
        test <@ NonEmptyList.truncate 2 input |> NonEmptyList.toList = [ 1; 2 ] @>

        test <@ NonEmptyList.init 0 id |> NonEmptyList.toList = [ 0 ] @>
        test <@ NonEmptyList.replicate 3 'a' |> NonEmptyList.toList = [ 'a'; 'a'; 'a' ] @>

    [<Fact>]
    let ``map2 truncates rather than raising on a length mismatch`` () =
        raises<System.ArgumentException> <@ List.map2 (+) [ 1; 2 ] [ 1 ] @>
        test <@ NonEmptyList.map2 (+) (sample ()) (NonEmpty(10, [ 20 ])) |> NonEmptyList.toList = [ 11; 22 ] @>
        test <@ NonEmptyList.allPairs (NonEmpty(1, [ 2 ])) (NonEmpty('a', [])) |> NonEmptyList.toList = [ 1, 'a'; 2, 'a' ] @>

    [<Fact>]
    let ``distinctBy and sortByDescending stay closed over non-emptiness`` () =
        let input = NonEmpty(1, [ 2; 3; 4 ])
        test <@ NonEmptyList.distinctBy (fun value -> value % 2) input |> NonEmptyList.toList = [ 1; 2 ] @>
        test <@ NonEmptyList.sortByDescending id input |> NonEmptyList.toList = [ 4; 3; 2; 1 ] @>
        test <@ NonEmptyList.pairwise input = [ 1, 2; 2, 3; 3, 4 ] @>
        test <@ NonEmptyList.pairwise (NonEmptyList.singleton 1) = [] @>

    [<Fact>]
    let ``the array module carries the same operations`` () =
        let input = NonEmptyArray.ofList [ 1m; 2m; 3m; 4m ] |> Option.get
        test <@ NonEmptyArray.sum input = 10m @>
        test <@ NonEmptyArray.sumBy (fun value -> value * 2m) input = 20m @>
        test <@ NonEmptyArray.average input = 2.5m @>
        test <@ NonEmptyArray.averageBy (fun value -> value * 2m) input = 5m @>
        test <@ NonEmptyArray.item 99 input = 4m @>
        test <@ NonEmptyArray.truncate 0 input |> NonEmptyArray.toList = [ 1m ] @>
        test <@ NonEmptyArray.skip 99 input = [||] @>
        test <@ NonEmptyArray.scan (+) 0m input |> NonEmptyArray.toList = [ 0m; 1m; 3m; 6m; 10m ] @>
        test <@ NonEmptyArray.choose (fun value -> if value > 2m then Some value else None) input = [| 3m; 4m |] @>
        test <@ NonEmptyArray.countBy (fun value -> value > 2m) input |> Map.find true = 2 @>
        test <@ NonEmptyArray.replicate 3 'a' |> NonEmptyArray.toList = [ 'a'; 'a'; 'a' ] @>
        test <@ NonEmptyArray.map2 (+) input (NonEmptyArray.singleton 10m) |> NonEmptyArray.toList = [ 11m ] @>
        test <@ NonEmptyArray.sortByDescending id input |> NonEmptyArray.head = 4m @>
