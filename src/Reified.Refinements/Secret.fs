namespace Reified.Refinements

/// <summary>A reference-equality value with redacted display and explicit Secret.reveal access.</summary>
/// <remarks>The payload is captured in a private closure rather than an enumerable raw field. This is redaction,
/// not encryption or access control. Caller-owned plaintext and explicitly revealed values remain the caller's responsibility.</remarks>
#if FABLE_COMPILER
[<Fable.Core.AttachMembers>]
#endif
[<Sealed>]
type Secret<'value> internal (read: unit -> 'value) =
    member internal _.Read() = read ()
    override _.ToString() = "<redacted>"
    /// <summary>Returns the redacted JSON representation, including for JavaScript JSON.stringify.</summary>
    /// <example><code>let shown = (Secret.create "credential").ToJson() // &lt;redacted&gt;</code></example>
    [<CompiledName("toJSON")>]
    member _.ToJson() = "<redacted>"

/// <summary>Explicit creation and access for secret values.</summary>
[<RequireQualifiedAccess>]
module Secret =
    /// <summary>Seals a value without inspecting it.</summary>
    /// <example><code>let password = Secret.create "credential"</code></example>
    let create value = Secret(fun () -> value)

    /// <summary>Explicitly accesses the sealed value.</summary>
    /// <example><code>let plain = Secret.reveal password</code></example>
    let reveal (secret: Secret<'value>) =
        if isNull (box secret) then nullArg (nameof secret)
        secret.Read()
