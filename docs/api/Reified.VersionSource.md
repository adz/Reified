# `VersionSource`

`VersionSource` is the explicit source of a contract version, chosen with `Contract.build` when a versioned contract is
declared.

| Source | Use |
| --- | --- |
| `VersionSource.Field "schemaVersion"` | Read a positive integer version from an input field. |
| `VersionSource.External` | The caller knows the version out of band and calls `Contract.parseVersion`. |
| `VersionSource.UnversionedMeans 1` | Treat input without a marker as one registered version. |

`Contract.parse` reads the declared source to select the frozen schema for the input version before parsing and
migrating. See [Versioned Contracts](/schema/versioned-contracts.html) for the full workflow.
