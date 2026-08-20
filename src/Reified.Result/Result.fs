namespace Reified.Result

/// <summary>Fail-fast helpers over the standard F# <c>Result</c> type.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Result =
    /// <summary>Creates an <c>Ok</c> result.</summary>
    let inline ok value =
        Ok value

    /// <summary>Creates an <c>Error</c> result.</summary>
    let inline error failure =
        Error failure

    /// <summary>Maps the success value of a result.</summary>
    let map mapper result =
        match result with
        | Ok value -> Ok(mapper value)
        | Error failure -> Error failure

    /// <summary>Maps the error value of a result.</summary>
    let mapError mapper result =
        match result with
        | Ok value -> Ok value
        | Error failure -> Error(mapper failure)

    /// <summary>Binds a result to the next fail-fast operation.</summary>
    let bind binder result =
        match result with
        | Ok value -> binder value
        | Error failure -> Error failure

    /// <summary>Computes a fallback result from the source error when the result fails.</summary>
    /// <remarks>The lazy counterpart to <c>orElse</c>, matching the <c>Flow.orElseWith</c> naming and shape:
    /// the fallback runs only on failure and can inspect the error that caused it.</remarks>
    /// <example>
    /// <code>
    /// let result = Error "boom"
    /// result |> Result.orElseWith (fun error -> Ok (String.length error)) // Ok 4
    /// </code>
    /// </example>
    let orElseWith (fallback: 'error -> Result<'value, 'error>) (result: Result<'value, 'error>) : Result<'value, 'error> =
        match result with
        | Ok value -> Ok value
        | Error failure -> fallback failure

    /// <summary>Falls back to another result when the source result fails.</summary>
    /// <example>
    /// <code>
    /// let result = Error "boom"
    /// result |> Result.orElse (Ok 5) // Ok 5
    /// </code>
    /// </example>
    let orElse (fallback: Result<'value, 'error>) (result: Result<'value, 'error>) : Result<'value, 'error> =
        orElseWith (fun _ -> fallback) result

    /// <summary>Keeps the input value when the predicate holds, or returns the supplied error.</summary>
    /// <remarks>Mirrors <c>Option.filter</c>: predicate first, subject piped last. The error is attached
    /// separately with <c>orError</c> so this stays a pure filter, same shape as its <c>Option</c> counterpart.</remarks>
    let inline okIf (predicate: 'input -> bool) (input: 'input) : Result<'input, unit> =
        if predicate input then Ok input else Error ()

    /// <summary>Keeps the input value when the predicate does not hold, or returns the supplied error.</summary>
    /// <remarks>The inverse of <c>okIf</c>: fails when the predicate is true, succeeds otherwise.</remarks>
    let inline failIf (predicate: 'input -> bool) (input: 'input) : Result<'input, unit> =
        if predicate input then Error () else Ok input

    /// <summary>Requires an already-computed condition where there is no subject value to preserve.</summary>
    /// <remarks>The condition is already computed and stands alone, so success produces <c>Ok ()</c>. Use
    /// <c>okIf</c>/<c>failIf</c> instead when the value under test should flow through.</remarks>
    /// <example><code>request.AcceptedTerms |> Result.require |> Result.orError TermsNotAccepted</code></example>
    let inline require (condition: bool) : Result<unit, unit> =
        if condition then Ok () else Error ()

    /// <summary>Replaces whatever error a result carries with the supplied typed error. <c>Ok</c> passes through unchanged.</summary>
    /// <remarks>The natural follow-up to <c>okIf</c>/<c>failIf</c>, which fail with <c>unit</c> precisely so the
    /// reason is chosen here: <c>value |> Result.okIf isValid |> Result.orError MyError</c>. Use
    /// <c>Result.mapError</c> instead when the existing error carries something worth keeping, as a
    /// <c>Violation</c> does.</remarks>
    let inline orError (failure: 'error) (result: Result<'value, 'discardedError>) : Result<'value, 'error> =
        result |> mapError (fun _ -> failure)

    /// <summary>Converts a .NET <c>Try*</c> tuple into a unit-error result.</summary>
    let fromTry (tryResult: bool * 'value) : Result<'value, unit> =
        match tryResult with
        | true, value -> Ok value
        | false, _ -> Error ()

    /// <summary>Converts an F# <c>Choice</c> into a result.</summary>
    let fromChoice (choice: Choice<'value, 'error>) : Result<'value, 'error> =
        match choice with
        | Choice1Of2 value -> Ok value
        | Choice2Of2 failure -> Error failure

    /// <summary>Drops the error channel and returns <c>Some</c> for success.</summary>
    let toOption (result: Result<'value, 'error>) : 'value option =
        match result with
        | Ok value -> Some value
        | Error _ -> None

    /// <summary>Drops the error channel and returns <c>ValueSome</c> for success.</summary>
    let toValueOption (result: Result<'value, 'error>) : 'value voption =
        match result with
        | Ok value -> ValueSome value
        | Error _ -> ValueNone

    /// <summary>Returns the success value or the supplied fallback value.</summary>
    let defaultValue (fallback: 'value) (result: Result<'value, 'error>) : 'value =
        match result with
        | Ok value -> value
        | Error _ -> fallback

    /// <summary>Runs a side effect on the successful value and returns the result unchanged.</summary>
    /// <remarks>For logging and diagnostics at a boundary. The effect cannot change the result.</remarks>
    /// <example>
    /// <code>
    /// Ok 10 |> Result.tap (printfn "loaded %d") // prints, then returns Ok 10
    /// </code>
    /// </example>
    let tap (effect: 'value -> unit) (result: Result<'value, 'error>) : Result<'value, 'error> =
        match result with
        | Ok value -> effect value
        | Error _ -> ()

        result

    /// <summary>Runs a side effect on the error value and returns the result unchanged.</summary>
    /// <example>
    /// <code>
    /// Error "boom" |> Result.tapError (printfn "failed: %s") // prints, then returns Error "boom"
    /// </code>
    /// </example>
    let tapError (effect: 'error -> unit) (result: Result<'value, 'error>) : Result<'value, 'error> =
        match result with
        | Ok _ -> ()
        | Error failure -> effect failure

        result

    /// <summary>Maps each value with a result-returning function, stopping at the first error.</summary>
    /// <remarks>Takes any sequence and always produces a list. Traversal stops at the first error, so later
    /// mappings do not run. Use one of the accumulating builders when every error should be reported.</remarks>
    /// <example>
    /// <code>
    /// [ "1"; "2" ] |> Result.traverse parseInt // Ok [ 1; 2 ]
    /// </code>
    /// </example>
    let traverse (mapping: 'input -> Result<'output, 'error>) (values: seq<'input>) : Result<'output list, 'error> =
        let rec loop accumulated remaining =
            match remaining with
            | [] -> Ok(List.rev accumulated)
            | head :: tail ->
                match mapping head with
                | Ok value -> loop (value :: accumulated) tail
                | Error failure -> Error failure

        loop [] (Seq.toList values)

    /// <summary>Turns a sequence of results into one fail-fast result containing all successes.</summary>
    /// <remarks>Takes any sequence and always produces a list. Stops at the first error.</remarks>
    /// <example>
    /// <code>
    /// [ Ok 1; Error "missing"; Ok 3 ] |> Result.sequence // Error "missing"
    /// </code>
    /// </example>
    let sequence (values: seq<Result<'value, 'error>>) : Result<'value list, 'error> =
        traverse id values

    /// <summary>Maps each value with a result-returning function, running every mapping and collecting every error.</summary>
    /// <remarks>Takes any sequence and always produces a list. The sequence is enumerated once, in order, and every
    /// mapping runs even after one fails, so a mapping with side effects runs for every item. Errors appear in input
    /// order. Each mapping contributes one error; nothing is flattened. Use <c>traverse</c> when the first failure
    /// should stop the work.</remarks>
    /// <example>
    /// <code>
    /// [ "1"; "x"; "y" ] |> Result.traverseAll parseInt // Error [ NotANumber "x"; NotANumber "y" ]
    /// </code>
    /// </example>
    let traverseAll (mapping: 'input -> Result<'output, 'error>) (values: seq<'input>) : Result<'output list, 'error list> =
        let mapped = values |> Seq.toList |> List.map mapping

        let failures =
            mapped
            |> List.choose (function
                | Error failure -> Some failure
                | Ok _ -> None)

        match failures with
        | [] ->
            mapped
            |> List.choose (function
                | Ok value -> Some value
                | Error _ -> None)
            |> Ok
        | failures -> Error failures

    /// <summary>Turns a sequence of results into one result containing all successes, or every error.</summary>
    /// <remarks>Takes any sequence and always produces a list. Errors appear in input order.</remarks>
    /// <example>
    /// <code>
    /// [ Ok 1; Error "missing"; Error "invalid" ] |> Result.sequenceAll // Error [ "missing"; "invalid" ]
    /// </code>
    /// </example>
    let sequenceAll (values: seq<Result<'value, 'error>>) : Result<'value list, 'error list> =
        traverseAll id values
