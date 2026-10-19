# `Contract`

A schema describes the current model. A `Contract` records previously shipped wire versions and a typed migration from
each version into that model.

`Contract.parse` selects the declared version, parses its frozen shape, and runs each adjacent migration in order. It
returns `Result<'model, ContractError>`: the current model, `VersionMissing`, `VersionUnrecognized`, `VersionTooNew`,
`ParseFailed` (path-aware `SchemaErrors` for the selected version), or `Migration`. A migrated value is re-checked
against the current schema, so `MigrationError.RevalidationFailed` carries path-aware `SchemaErrors`, while a
`MigrationError.MigrationFailed` written by your own migration carries only its message string and preserves no paths.

The error union is five cases, not the three prose outcomes above:

```fsharp
open Reified

// The union a Contract.parse caller matches: the current model on success, one of these on failure.
let describe (failure: ContractError) =
    match failure with
    | ContractError.VersionMissing -> "no readable schema version"
    | ContractError.VersionUnrecognized version -> $"version {version} is not registered"
    | ContractError.VersionTooNew(found, supported) ->
        $"payload is v{found}; this build supports v{supported}"
    | ContractError.ParseFailed(version, diagnostics) ->
        $"v{version} payload is malformed: {SchemaErrors.count diagnostics} issue(s)"
    | ContractError.Migration failure -> $"migration failed: {failure}"
```

See [Versioned Contracts](/schema/versioned-contracts.html) for contract authoring.
