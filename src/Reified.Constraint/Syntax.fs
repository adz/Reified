namespace Reified

/// <summary>
/// Constraint constructors usable without the <c>Constraint.</c> prefix inside a module that declares value rules.
/// </summary>
/// <remarks>
/// <para>
/// Optional vocabulary, not another abstraction. Opening it makes a declaration read
/// <c>minLength 3</c> instead of <c>Constraint.minLength 3</c>; everything here is the same value the qualified
/// name returns.
/// </para>
/// <para>
/// Some constructors are deliberately left out because they shadow names the same validation code is likely to
/// need in scope: <c>contains</c>, <c>distinct</c>, <c>all</c>, <c>any</c>, <c>length</c>, and <c>between</c> shadow
/// core F# operations, and <c>check</c> shadows <c>Schema.check</c>. Reach for those as <c>Constraint.contains</c>,
/// <c>Constraint.all</c>, <c>Constraint.check</c>, and so on, even inside a module that has opened this DSL.
/// <c>test</c> has no such collision and is exported.
/// </para>
/// <para>
/// <c>guard</c>, <c>orError</c>, and <c>mapError</c> are structural adapters matching the corresponding
/// <c>Result</c> operations. They let a constraint pipeline retain its input and finish with the application's
/// error type without adding an Reified.Result dependency.
/// </para>
/// <code>
/// module SignupRules =
///     open Reified.ConstraintDSL
///
///     let age : Constraint&lt;int&gt; = atLeast 13
///     let contact : Constraint&lt;string&gt; = Constraint.all [ present; email ]
///     let requireContact value = value |> guard contact |> orError EmailRequired
/// </code>
/// </remarks>
module ConstraintDSL =
    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.present" />.</summary>
    let inline present< ^value when (^value or Constraint.PresentDispatcher): (static member Create:
        ^value -> Constraint< ^value >)> : Constraint< ^value > = Constraint.present

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.blank" />.</summary>
    let inline blank< ^value when (^value or Constraint.BlankDispatcher): (static member Create:
        ^value -> Constraint< ^value >)> : Constraint< ^value > = Constraint.blank

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.optional" />.</summary>
    let inline optional inner = Constraint.optional inner

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.minLength" />.</summary>
    let inline minLength< ^value when (^value or Constraint.CardinalityDispatcher): (static member Create:
        ^value * Cardinality -> Constraint< ^value >)> minimum : Constraint< ^value > =
        Constraint.minLength minimum

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.maxLength" />.</summary>
    let inline maxLength< ^value when (^value or Constraint.CardinalityDispatcher): (static member Create:
        ^value * Cardinality -> Constraint< ^value >)> maximum : Constraint< ^value > =
        Constraint.maxLength maximum

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.lengthBetween" />.</summary>
    let inline lengthBetween< ^value when (^value or Constraint.CardinalityDispatcher): (static member Create:
        ^value * Cardinality -> Constraint< ^value >)> minimum maximum : Constraint< ^value > =
        Constraint.lengthBetween minimum maximum

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.email" />.</summary>
    let email = Constraint.email

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.trimmed" />.</summary>
    let trimmed = Constraint.trimmed

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.numeric" />.</summary>
    let numeric = Constraint.numeric

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.alphanumeric" />.</summary>
    let alphanumeric = Constraint.alphanumeric

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.pattern" />.</summary>
    let pattern = Constraint.pattern

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.oneOf" />.</summary>
    let oneOf = Constraint.oneOf

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.noneOf" />.</summary>
    let noneOf = Constraint.noneOf

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.equalTo" />.</summary>
    let equalTo = Constraint.equalTo

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.notEqualTo" />.</summary>
    let notEqualTo = Constraint.notEqualTo

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.greaterThan" />.</summary>
    let greaterThan = Constraint.greaterThan

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.lessThan" />.</summary>
    let lessThan = Constraint.lessThan

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.atLeast" />.</summary>
    let atLeast = Constraint.atLeast

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.atMost" />.</summary>
    let atMost = Constraint.atMost

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.positive" />.</summary>
    let inline positive< ^value when ^value: comparison and ^value: (static member Zero: ^value)> : Constraint< ^value > =
        Constraint.positive

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.nonNegative" />.</summary>
    let inline nonNegative< ^value when ^value: comparison and ^value: (static member Zero: ^value)> : Constraint< ^value > =
        Constraint.nonNegative

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.negative" />.</summary>
    let inline negative< ^value when ^value: comparison and ^value: (static member Zero: ^value)> : Constraint< ^value > =
        Constraint.negative

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.nonPositive" />.</summary>
    let inline nonPositive< ^value when ^value: comparison and ^value: (static member Zero: ^value)> : Constraint< ^value > =
        Constraint.nonPositive

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.single" />.</summary>
    let inline single< ^value when (^value or Constraint.CardinalityDispatcher): (static member Create:
        ^value * Cardinality -> Constraint< ^value >)> : Constraint< ^value > = Constraint.single

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.atLeastOne" />.</summary>
    let inline atLeastOne< ^value when (^value or Constraint.CardinalityDispatcher): (static member Create:
        ^value * Cardinality -> Constraint< ^value >)> : Constraint< ^value > = Constraint.atLeastOne

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.atMostOne" />.</summary>
    let inline atMostOne< ^value when (^value or Constraint.CardinalityDispatcher): (static member Create:
        ^value * Cardinality -> Constraint< ^value >)> : Constraint< ^value > = Constraint.atMostOne

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.moreThanOne" />.</summary>
    let inline moreThanOne< ^value when (^value or Constraint.CardinalityDispatcher): (static member Create:
        ^value * Cardinality -> Constraint< ^value >)> : Constraint< ^value > = Constraint.moreThanOne

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.notContains" />.</summary>
    /// <remarks>
    /// Exported although <c>contains</c> is not: the omissions are driven by collision, and no core F# operation
    /// is named <c>notContains</c>.
    /// </remarks>
    let inline notContains< ^container, 'value when (^container or Constraint.NotContainsDispatcher): (static member Create:
        ^container * 'value -> Constraint< ^container >)>
        (expected: 'value)
        : Constraint< ^container > =
        Constraint.notContains expected

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.multipleOf" />.</summary>
    let inline multipleOf divisor = Constraint.multipleOf divisor

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.finite" />.</summary>
    let finite = Constraint.finite

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.finite32" />.</summary>
    let finite32 = Constraint.finite32

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.notWith" />.</summary>
    let notWith = Constraint.notWith

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.custom" />.</summary>
    let custom = Constraint.custom

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.customLocalized" />.</summary>
    let customLocalized = Constraint.customLocalized

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.customWith" />.</summary>
    let customWith = Constraint.customWith

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.contramap" />.</summary>
    let contramap = Constraint.contramap

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.describe" />.</summary>
    let describe = Constraint.describe

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.test" />.</summary>
    let test = Constraint.test

    /// <summary>Alias for <see cref="M:Reified.Constraint.Constraint.guard" />.</summary>
    /// <example><code>value |> guard present |> orError NameRequired</code></example>
    let guard = Constraint.guard

    /// <summary>Replaces a failed constraint's violation with the supplied error.</summary>
    /// <remarks>Defined here because Reified.Constraint does not depend on Reified.Result.</remarks>
    /// <example><code>value |> guard present |> orError NameRequired</code></example>
    let inline orError failure result =
        match result with
        | Ok value -> Ok value
        | Error _ -> Error failure

    /// <summary>Maps a failed constraint's violation with the supplied function.</summary>
    /// <remarks>Defined here because Reified.Constraint does not depend on Reified.Result.</remarks>
    /// <example><code>value |> guard (greaterThan 0) |> mapError InvalidQuantity</code></example>
    let inline mapError mapper result =
        match result with
        | Ok value -> Ok value
        | Error violation -> Error(mapper violation)
