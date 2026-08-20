namespace Reified

open Reified.Result

/// <summary>
/// The concise result vocabulary: the <c>result { }</c> computation expression, its accumulating
/// <c>result.list { }</c> / <c>result.array { }</c> variants, and the lightweight admission functions
/// (<c>okIf</c>, <c>failIf</c>, <c>require</c>, <c>orError</c>, <c>mapError</c>).
/// </summary>
/// <remarks>
/// Optional and opt-in, in the same shape as <c>Reified.DataDSL</c>, <c>Reified.ConstraintDSL</c>, and
/// <c>Reified.SchemaDSL</c>: <c>open Reified.Result</c> for <c>Result</c>, then
/// <c>open Reified.ResultDSL</c> for this vocabulary. Deliberately small: generic combinators such as
/// <c>map</c>, <c>bind</c>, <c>orElse</c>, <c>tap</c>, and the traversal helpers stay qualified as
/// <c>Result.map</c>, <c>Result.bind</c>, and so on.
/// </remarks>
module ResultDSL =
    /// <summary>The fail-fast <c>result { }</c> computation expression.</summary>
    let result = ResultBuilder()

    /// <summary>Alias for <see cref="M:Reified.Result.Result.okIf" />.</summary>
    let inline okIf predicate value =
        Result.okIf predicate value

    /// <summary>Alias for <see cref="M:Reified.Result.Result.failIf" />.</summary>
    let inline failIf predicate value =
        Result.failIf predicate value

    /// <summary>Alias for <see cref="M:Reified.Result.Result.require" />.</summary>
    let inline require condition =
        Result.require condition

    /// <summary>Alias for <see cref="M:Reified.Result.Result.orError" />.</summary>
    let inline orError failure result =
        Result.orError failure result

    /// <summary>Alias for <see cref="M:Reified.Result.Result.mapError" />.</summary>
    let inline mapError mapper result =
        Result.mapError mapper result
