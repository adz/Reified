# Documentation review — fresh-context reading notes

Scope: documentation only; no implementation inspected. Line numbers refer to Markdown source. Items below are questions to verify, not findings about implementation.

## Assumptions

- `docs/01-getting-started/02-what-a-schema-gives-you.md:150` — “`SchemaGen` produces values that satisfy the declaration”; confirm how generation reports unsupported or opaque constraints instead of producing unchecked fixtures.

- `docs/index.md:27` — “No runtime reflection: the same declaration compiles under NativeAOT, trimming, and Fable.” Confirm the supported target/compiler combinations and exceptions.
- `docs/index.md:67` — “decoder ran about 14% faster on .NET 10 ... and allocated less.” Confirm workload, versions, and measurement variance.
- `docs/01-getting-started/_index.md:112` — “Every independent field is checked”; confirm behavior for dependent fields, constructor failures, nested collections, and error ordering.
- `docs/02-schema/01-quickstart.md:46` — “the wire name is the camelCased property name”; confirm acronym and Unicode naming behavior.
- `docs/02-schema/01-quickstart.md:111` — “Compile the codec once and reuse it”; confirm concurrent reuse and whether compilation itself is thread-safe.
- `docs/02-schema/20-dsl.md:72` — “Supplying more than one accepted name for the same field is an error”; confirm which interpreter reports it and whether construction can throw.
- `docs/02-schema/20-dsl.md:231` — “evaluated at most once”; confirm concurrency and whether failed evaluation is cached.
- `docs/02-schema/25-input-sources.md:108` — “Later pairs override earlier ones at the same path”; confirm collision rules for scalar/section and sparse numeric indexes.
- `docs/02-schema/35-trusted-construction.md:217` — “It returns the original value on success”; confirm whether `constructResult` normalization is discarded by `Schema.check`.
- `docs/02-schema/45-union-schemas.md:58` — “Reified rejects duplicate tags ... inspectors that match either no case or more than one case”; confirm when and how these fail.
- `docs/02-schema/50-json-codecs.md:83` — “reordered object, alias, omitted field, or unknown field transparently restarts”; confirm unknown-field behavior and performance cost.
- `docs/02-schema/50-json-codecs.md:183` — “Checked constructors ... still run”; confirm exactly which validation is skipped by trusted decoding, including refinements.
- `docs/02-schema/60-derivation/_index.md:40` — generator matches bare attribute name rather than resolved type; confirm false-positive diagnostics and mitigation.
- `docs/02-schema/65-versioned-contracts.md:68` — migrations are followed by checking the current schema; confirm intermediate versions and path preservation on migration failures.
- `docs/02-schema/70-patterns/20-legal-transitions.md:64` — shifting dates “cannot reverse their order”; confirm overflow behavior at date limits.

## Unclear or misleading

- `docs/02-schema/47-schema-or-generated-dto.md:64-77` — `toDomain` promises `Result<Signup, string>` but builds `Signup` with a `ContactEmail` where `Email` was declared `string`; `bytes` is undeclared, and `Json.deserialize` is used as though it returned `Result`. Show a complete type-correct conversion using `Json.tryDeserialize` and explicit error mapping.
- `docs/02-schema/25-input-sources.md:48-51` — suggests moving `Contacts`' collection-level `minLength 1` constraint onto `Contact` when removing `withSchema`; an individual `Contact` cannot enforce the length of its containing list. Keep the field block with `constrain` and omit only `withSchema`.
- `docs/02-schema/90-reference-apps.md:48-55` — says generated wire schemas carry constraints and are written via a compiled codec, but `docs/02-schema/47-schema-or-generated-dto.md:39-55` warns that trusted codecs skip wire constraints. Explain whether this app validates with `Schema.parse` before writing and why this differs from the recommended permissive DTO strategy.

- `docs/01-getting-started/_index.md:94` — “No `Signup` exists unless every field and the constructor succeeded” sounds like a guarantee about the public record type; callers can construct `Signup` directly. Say that `Schema.parse` does not return a `Signup` on failure.
- `docs/01-getting-started/_index.md:79-89` — says the same schema reads JSON and shows `Data.ofNameValues` for text pairs; explain how JSON becomes `Data` before `Schema.parse`, or link the conversion recipe at this point.
- `docs/01-getting-started/02-what-a-schema-gives-you.md:65-69` — printed sample output includes `age:` and `email:`, but the `printfn` expression renders only `issue.Error`, never `issue.Path`; show path formatting or remove the prefixes.
- `docs/01-getting-started/02-what-a-schema-gives-you.md:143-144` — says migration failures and malformed payloads carry the same `SchemaErrors` and paths, but the example matches only `VersionUnrecognized` and `ParseFailed`; show the migration failure case or qualify the claim.

- `README.md:112` calls attribute-based derivation “preferred approach for DTOs,” while `docs/02-schema/47-schema-or-generated-dto.md:39-44` says derived wire DTOs should have **no** constraint attributes, but README’s derived example has `[<Present; Email>]` and `[<AtLeast 13>]`. Distinguish derived validated boundary model from permissive wire DTO explicitly.
- `docs/01-getting-started/02-what-a-schema-gives-you.md:78-84` says codec decoding failures return “the same path-aware `SchemaErrors`”; `docs/02-schema/50-json-codecs.md:113-121` says `JsonCodecException` or `Result<_, string>` with first structural failure. These cannot both describe the public decoder result.
- `docs/02-schema/01-quickstart.md:174-189` shows `Seats` without constraint in the schema, yet JSON Schema has `exclusiveMinimum:0` and diagnostics say “Must be greater than 0”; no visible definition establishes this rule.
- `docs/02-schema/70-patterns/30-wire-and-domain-models.md:74` uses `(BookingWire.parse raw).Result` although `docs/02-schema/60-derivation/_index.md:29-31` documents `parse : Data -> Result<_, SchemaErrors>`; `.Result` suggests a different API.
- `docs/02-schema/70-patterns/30-wire-and-domain-models.md:113` points to a “`.contract` grammar alternative,” but no such grammar is introduced in the linked Versioned Contracts page.
- `docs/02-schema/90-reference-apps.md:38` claims generated typed `Fields` references, while `docs/02-schema/60-derivation/40-generated-code.md:104` explicitly says no `Fields` modules are generated.
- `docs/02-schema/85-examples.md:156-220` labels an example “refined” but publicly exposes `Email.create`/`Quantity.create` that directly wrap unchecked inputs and then demonstrates invalid values. It undercuts the guarantee taught elsewhere.
- `docs/02-schema/70-patterns/40-project-structure.md:67` mentions `Service.get`, without introducing its package or relation to Reified; looks like an unrelated API.
- `docs/02-schema/05-tutorials/25-inspecting-metadata.md:70` says lowering `Any` by dropping a disjunct “strengthens” it; useful caveat, but the preceding `keywords` example does precisely a lossy flattening without showing the safe full-expression alternative.
- `docs/02-schema/50-json-codecs.md:23` calls storage and queues trusted producers; persistence and queues can contain stale or externally produced bytes. Define trust as a property of the producer and admission history, not transport.

## Too long

- `docs/01-getting-started/_index.md:137-232` — after the complete Signup walkthrough, the page restarts with a problem statement, a standalone constraint, a refinement, and another schema. Consider retaining one short conceptual contrast here and linking to focused guides for the second set of examples.

- `docs/02-schema/01-quickstart.md` spans plain parsing, refined types, constraints, private aggregates, drafts, updates, and JSON Schema. Keep “First schema and parse” and “Read failures” here; move refined fields to “Refined fields in Schema,” field constraints to “Boundary-specific field rules,” and private Booking/draft/update flow to “Construction guarantees.”
- `docs/02-schema/65-versioned-contracts.md` mixes the manual contract workflow and a long generator evolution tutorial. Split into “Handwritten versioned contracts” and “Generated version series: freezing and migrating.”
- `docs/02-schema/85-examples.md` embeds entire example program files. Replace with a short explanation, link to runnable source, and focused relevant excerpts; keep the complete program at the linked location.

## Reference material in guides

- `docs/01-getting-started/02-what-a-schema-gives-you.md:37-45` — the seven-API inventory is useful orientation, not an exhaustive reference; keep it here, but link the `SchemaGen` entry directly to its own usage/reference page rather than only the generic Data section at line 152.

- `docs/02-schema/20-dsl.md`, “Constraint equivalents”: move the operation/attribute mapping to `docs/api/Reified.Schema.md` front matter (and retain a short link to Derivation Attributes).
- `docs/02-schema/46-advanced-union-handling.md`, “Ready-made compatibility formats” and “Build a representation directly”: move exhaustive `UnionRepresentations`/`UnionPayloadStyle` options to their API entity reference enrichment; keep one compatibility recipe in the guide.
- `docs/02-schema/60-derivation/10-msbuild.md`, “MSBuild properties”: put the exhaustive build-property defaults in the build tooling reference; keep installation and a link in the guide.
- `docs/02-schema/60-derivation/20-attributes.md`, attribute tables: this is already a reference-shaped page; consider reference enrichment for the derivation attribute entities rather than repeating the tables in tutorial pages.
- `docs/02-schema/65-versioned-contracts.md`, “Version sources”: move the exhaustive `VersionSource` case list to `docs/api/Reified.Contract.md`; keep one example per use case.
- `docs/02-schema/30-redisplay-and-field-errors.md`, “The Schema catalogue”: move exhaustive message-key/argument/default table to the appropriate Schema error API reference front matter.

## Missing

- `docs/02-schema/47-schema-or-generated-dto.md:51-77` — strategy 2 lacks a runnable boundary example with a defined byte payload, actual wire-to-domain types, and structural decode error handling; the current fragment cannot serve as a copyable starting point.

- `docs/01-getting-started/02-what-a-schema-gives-you.md:150-152` — the seventh promised job has no runnable generation example, unlike the preceding jobs. Add a minimal `SchemaGen` call or label this section as a pointer to a separate recipe.

- A single decision matrix explicitly contrasting `Schema.parse`, `Schema.check`, `Json.deserialize`, `Json.tryDeserialize`, and `Contract.parse`, including validation run, constructor behavior, input type, error type, and when to trust a payload.
- A complete HTTP JSON boundary recipe from request bytes through `Json.parseData`, `Schema.parseRetainingInput`, localized `SchemaErrors`, and an application-owned response; current examples demonstrate pieces separately.
- A compact error taxonomy and mapping recipe: `Violation`, `ParseError`, `SchemaError`, `SchemaErrors`, `JsonCodecException`, `MigrationError`, and `ContractError` with conversions and paths.
- Documentation for concurrency/caching of compiled codecs and deferred recursive schemas; the guidance to compile once invites shared use but does not say whether it is safe.
- A single glossary for “trusted,” “admission,” “interpreted,” “opaque,” “canonical,” “wire,” and “domain.”

## Suggestions for humans and LLMs

- `docs/02-schema/85-examples.md:147-148` — the mirrored example invokes `DateTimeOffset.UtcNow` twice for a publish interval. Use fixed endpoints in teaching examples so copied examples are deterministic and the interval is unambiguous.

- `docs/01-getting-started/03-where-to-go-next.md:10` — the page has both a front-matter title and an explicit H1, unlike its sibling pages; use the same heading convention throughout Getting Started to avoid duplicate titles in rendered navigation.

- Keep `llms.txt` as a task router, but reconcile its asserted navigation order with the visible section ordering; add a short API-selection decision table and explicit trust-boundary warning.
- Put actual F# return types and failure types immediately beside every first-use API example; avoid pseudo-output that resembles executable code without showing its setup.
- Link every performance claim to a specific workload and runtime/version, and mark relative results as benchmark-specific rather than general guarantees.
- Make examples consistently self-contained or label them explicitly as fragments; use the same Signup/Booking model across adjacent pages only when rules and expected output really agree.
- Add “verified by docs build” versus “illustrative pseudocode” metadata to examples so coding assistants do not copy placeholders as runnable F#.
