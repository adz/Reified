namespace Reified

open Reified.Result

/// <summary>
/// The concise result vocabulary: the <c>result { }</c> computation expression and, through it,
/// <c>result.list { }</c> and <c>result.array { }</c> for accumulating every error.
/// </summary>
/// <remarks>
/// Optional and opt-in, in the same shape as <c>Reified.DataDSL</c>, <c>Reified.ConstraintDSL</c>, and
/// <c>Reified.SchemaDSL</c>: <c>open Reified.Result</c> for <c>Result</c>, then
/// <c>open Reified.ResultDSL</c> for this vocabulary. It carries only computation expressions rather
/// than a vocabulary of constructors, and sits under the shared name for consistency.
/// </remarks>
module ResultDSL =
    /// <summary>The fail-fast <c>result { }</c> computation expression.</summary>
    let result = ResultBuilder()
