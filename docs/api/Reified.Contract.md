# `Contract`

A schema describes the current model. A `Contract` records previously shipped wire versions and a typed migration from
each version into that model.

`Contract.parse` selects the declared version, parses its frozen shape, and runs the migration. It returns the current
model, an unrecognized-version error, or the path-aware parse failures for the selected version.

See [Versioned Contracts](/schema/versioned-contracts.html) for the complete authoring workflow.
