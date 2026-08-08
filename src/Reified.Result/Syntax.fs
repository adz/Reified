namespace Reified.Result

/// <summary>
/// The concise result vocabulary: the <c>result { }</c> computation expression and, through it,
/// <c>result.list { }</c> and <c>result.array { }</c> for accumulating every error.
/// </summary>
/// <remarks>
/// Optional and opt-in, in the same shape as <c>Reified.DataSyntax</c>, <c>Reified.ConstraintSyntax</c>, and
/// <c>Reified.SchemaSyntax</c>: <c>open Reified.Result</c> for <c>Result</c>, then
/// <c>open Reified.Result.Syntax</c> for this vocabulary.
/// </remarks>
module Syntax =
    /// <summary>The fail-fast <c>result { }</c> computation expression.</summary>
    let result = ResultBuilder()
