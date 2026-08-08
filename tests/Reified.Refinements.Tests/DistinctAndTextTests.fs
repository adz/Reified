namespace Reified.Refinements.Tests

open Reified
open Reified.Refinements
open Swensen.Unquote
open Xunit

/// Proves the losslessness DistinctList buys, and the invariant-preserving text family.
module DistinctAndTextTests =

    let private distinct values =
        DistinctList.create values |> Result.defaultWith (failwithf "%A")

    let private nonBlank value =
        NonBlankString.create value |> Result.defaultWith (failwithf "%A")

    [<Fact>]
    let ``create rejects duplicates while ofSeq removes them`` () =
        test <@ DistinctList.create [ 1; 2; 3 ] |> Result.map DistinctList.toList = Ok [ 1; 2; 3 ] @>
        test <@ DistinctList.create [ 1; 2; 1 ] |> Result.isError @>
        test <@ DistinctList.ofSeq [ 1; 2; 1 ] |> DistinctList.toList = [ 1; 2 ] @>

    [<Fact>]
    let ``toSet is unconditionally lossless, which Set.ofList on a plain list is not`` () =
        // Distinct elements always produce a set of the same size.
        test <@ Set.ofList [ 1; 1; 2 ] |> Set.count = 2 @>

        let values = distinct [ 1; 2; 3 ]
        test <@ DistinctList.toSet values |> Set.count = DistinctList.length values @>

    [<Fact>]
    let ``toMap rejects a key collision rather than silently dropping an entry`` () =
        // Map.ofList keeps only the last entry and reports nothing.
        test <@ Map.ofList [ 1, "a"; 1, "b" ] |> Map.count = 1 @>

        // Distinctness holds over pairs, not keys, so these two are legitimately distinct
        // yet would collide. The conversion says so instead of losing one.
        let colliding = distinct [ 1, "a"; 1, "b" ]
        test <@ DistinctList.length colliding = 2 @>
        test <@ DistinctList.toMap colliding = Error(Atomic(Expected(UniquenessAtom, None))) @>

        let pairs = distinct [ 1, "a"; 2, "b" ]
        test <@ DistinctList.toMap pairs |> Result.map Map.count = Ok 2 @>
        test <@ DistinctList.toMap pairs |> Result.map (Map.tryFind 1) = Ok(Some "a") @>

    [<Fact>]
    let ``toMapBy rejects a non-injective projection`` () =
        let values = distinct [ 1; 2; 3 ]
        test <@ DistinctList.toMapBy string values |> Result.map Map.count = Ok 3 @>
        test <@ DistinctList.toMapBy (fun value -> value % 2) values = Error(Atomic(Expected(UniquenessAtom, None))) @>

    [<Fact>]
    let ``add, remove, and filter keep the list distinct`` () =
        let values = distinct [ 1; 2 ]
        test <@ DistinctList.add 3 values |> DistinctList.toList = [ 1; 2; 3 ] @>
        test <@ DistinctList.add 2 values |> DistinctList.toList = [ 1; 2 ] @>
        test <@ DistinctList.remove 1 values |> DistinctList.toList = [ 2 ] @>
        test <@ DistinctList.filter (fun value -> value > 1) values |> DistinctList.toList = [ 2 ] @>

    [<Fact>]
    let ``set operations are closed over distinctness`` () =
        let first = distinct [ 1; 2; 3 ]
        let second = distinct [ 3; 4 ]
        test <@ DistinctList.union first second |> DistinctList.toList = [ 1; 2; 3; 4 ] @>
        test <@ DistinctList.intersect first second |> DistinctList.toList = [ 3 ] @>
        test <@ DistinctList.difference first second |> DistinctList.toList = [ 1; 2 ] @>

    [<Fact>]
    let ``union is idempotent and its result stays admissible`` () =
        let values = distinct [ 1; 2 ]
        let united = DistinctList.union values values
        test <@ united |> DistinctList.toList = [ 1; 2 ] @>
        test <@ DistinctList.create (DistinctList.toList united) |> Result.isOk @>

    [<Fact>]
    let ``map removes duplicates a non-injective mapping introduces`` () =
        let values = distinct [ 1; 2; 3 ]
        test <@ DistinctList.map (fun value -> value % 2) values |> DistinctList.toList = [ 1; 0 ] @>

    [<Fact>]
    let ``non-blank text rejects whitespace-only input`` () =
        test <@ NonBlankString.create "hi" |> Result.map NonBlankString.value = Ok "hi" @>
        test <@ NonBlankString.create "   " |> Result.isError @>
        test <@ NonBlankString.create "" |> Result.isError @>

    [<Fact>]
    let ``append, trim, and case changes cannot produce blank text`` () =
        let value = nonBlank "  Hello  "
        test <@ NonBlankString.trim value |> NonBlankString.value = "Hello" @>
        test <@ NonBlankString.toUpper (nonBlank "ab") |> NonBlankString.value = "AB" @>
        test <@ NonBlankString.toLower (nonBlank "AB") |> NonBlankString.value = "ab" @>
        test <@ NonBlankString.append (nonBlank "a") (nonBlank "b") |> NonBlankString.value = "ab" @>

    [<Fact>]
    let ``every closed text operation yields text that is still admissible`` () =
        let value = nonBlank " Mixed Case "
        for candidate in
            [ NonBlankString.trim value
              NonBlankString.toUpper value
              NonBlankString.toLower value
              NonBlankString.append value value ] do
            test <@ NonBlankString.create (NonBlankString.value candidate) |> Result.isOk @>

    [<Fact>]
    let ``length is always at least one`` () =
        test <@ NonBlankString.length (nonBlank "a") = 1 @>
        test <@ NonBlankString.length (nonBlank "  x  ") > 0 @>

    [<Fact>]
    let ``split returns a non-empty list, so callers never handle an empty result`` () =
        let parts = NonBlankString.split "," (nonBlank "a,b,c")
        test <@ parts |> NonEmptyList.toList |> List.map NonBlankString.value = [ "a"; "b"; "c" ] @>
        test <@ NonEmptyList.length parts = 3 @>

    [<Fact>]
    let ``split discards blank segments but still yields at least one item`` () =
        let parts = NonBlankString.split "," (nonBlank "a,,b")
        test <@ parts |> NonEmptyList.toList |> List.map NonBlankString.value = [ "a"; "b" ] @>

        let degenerate = NonBlankString.split "," (nonBlank " x ")
        test <@ NonEmptyList.length degenerate = 1 @>

    [<Fact>]
    let ``join is the inverse of split for separator-free segments`` () =
        let value = nonBlank "a,b,c"
        test <@ NonBlankString.split "," value |> NonBlankString.join "," |> NonBlankString.value = "a,b,c" @>

    [<Fact>]
    let ``the everyday list operations stay closed over distinctness`` () =
        let input = DistinctList.ofSeq [ 3; 1; 2 ]

        test <@ DistinctList.sum input = 6 @>
        test <@ DistinctList.sumBy (fun value -> value * 2) input = 12 @>
        test <@ DistinctList.fold (+) 0 input = 6 @>
        test <@ DistinctList.exists (fun value -> value = 2) input @>
        test <@ DistinctList.forall (fun value -> value > 0) input @>
        test <@ DistinctList.tryFind (fun value -> value > 2) input = Some 3 @>
        test <@ DistinctList.tryPick (fun value -> if value > 2 then Some(value * 10) else None) input = Some 30 @>
        test <@ DistinctList.tryHead input = Some 3 @>
        test <@ DistinctList.isEmpty DistinctList.empty @>

        test <@ DistinctList.sort input |> DistinctList.toList = [ 1; 2; 3 ] @>
        test <@ DistinctList.sortBy (fun value -> -value) input |> DistinctList.toList = [ 3; 2; 1 ] @>
        test <@ DistinctList.rev input |> DistinctList.toList = [ 2; 1; 3 ] @>
        test <@ DistinctList.truncate 2 input |> DistinctList.toList = [ 3; 1 ] @>

        // A chooser need not be injective, so choose removes any collision it introduces.
        let collapsed = input |> DistinctList.choose (fun value -> Some(value % 2))
        test <@ DistinctList.toList collapsed = [ 1; 0 ] @>
        test <@ DistinctList.toSet collapsed |> Set.count = DistinctList.length collapsed @>
