# `Contract`

A schema describes the current model. A `Contract` records previously shipped wire versions and a typed migration from
each version into that model.

`Contract.parse` selects the declared version, parses its frozen shape, and runs each adjacent migration in order. It
returns `Result<'model, ContractError>`: the current model, `VersionMissing`, `VersionUnrecognized`, `VersionTooNew`,
`ParseFailed` (path-aware `SchemaErrors` for the selected version), or `Migration`. A migrated value is re-checked
against the current schema, so `MigrationError.RevalidationFailed` carries path-aware `SchemaErrors`, while a
`MigrationError.MigrationFailed` written by your own migration carries only its message string and preserves no paths.

See [Versioned Contracts](/schema/versioned-contracts.html) for contract authoring.
