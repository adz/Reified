namespace Reified

/// <summary>Primitive parse failures returned by <c>Parse</c> helpers.</summary>
type ParseError =
    /// <summary>The input was missing for the target primitive type.</summary>
    | MissingValue of target: string
    /// <summary>The input did not match the expected format for the target primitive type.</summary>
    | InvalidFormat of target: string * input: string
    /// <summary>The input was outside the supported range for the target primitive type.</summary>
    | OutOfRange of target: string * input: string

    override this.ToString() =
        match this with
        | MissingValue target -> "MissingValue (target '" + target + "')"
        | InvalidFormat(target, input) -> "InvalidFormat (target '" + target + "', input '" + input + "')"
        | OutOfRange(target, input) -> "OutOfRange (target '" + target + "', input '" + input + "')"
