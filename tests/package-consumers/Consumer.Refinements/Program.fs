// Reified.Refinements installed alone. It depends on Reified.Constraint and nothing else, so this also
// checks that Constraint arrives transitively and that Violation is reachable without naming it.

open Reified.Refinements

[<EntryPoint>]
let main _ =
    let populated = NonEmptyList.create [ 1; 2; 3 ]
    let empty = NonEmptyList.create ([]: int list)

    match populated, empty with
    | Ok list, Error _ when list.ToList() = [ 1; 2; 3 ] ->
        printfn "Consumer.Refinements OK"
        0
    | other ->
        eprintfn "Consumer.Refinements FAILED: %A" other
        1
