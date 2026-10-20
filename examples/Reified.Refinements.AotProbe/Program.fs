open Reified.Refinements

[<EntryPoint>]
let main _ =
    Refine.nonEmptyList [ 42 ]
    |> Result.map NonEmptyList.head
    |> function
        | Ok 42 -> 0
        | _ -> failwith "Unexpected Refined probe result."
