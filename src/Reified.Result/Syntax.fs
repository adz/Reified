namespace Reified.Result

/// <summary>
/// The concise result vocabulary: the <c>result { }</c> computation expression and, through it,
/// <c>result.list { }</c> and <c>result.array { }</c> for accumulating every error.
/// </summary>
/// <remarks>
/// Optional and opt-in, in the same shape as <c>Reified.Data.Syntax</c>, <c>Reified.Constraint.Syntax</c>, and
/// <c>Reified.Schema.Syntax</c>: <c>open Reified.Result</c> for <c>Result</c>, then
/// <c>open Reified.Result.Syntax</c> for this vocabulary.
/// </remarks>
module Syntax =
    /// <summary>The fail-fast <c>result { }</c> computation expression.</summary>
    let result = ResultBuilder()
