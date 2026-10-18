# Documentation review — fresh-context reading notes

Scope: documentation only; no implementation inspected. Line numbers refer to Markdown source. Items below are questions to verify, not findings about implementation.

## Assumptions

- `docs/95-notes/82-aot-trimming-fable.md:29-36` — verify whether every push runs the NativeAOT probe and whether its trimmer warnings are treated as errors; “if a change introduced reflection ... CI fails” is stronger than merely publishing and running the probe.
- `docs/95-notes/95-packages-and-platforms.md:38-40` — verify the claim that each listed package is exercised in a browser, not only on Node; the coverage claim affects platform choice.

- `docs/90-comparisons/05-dataannotations-fluentvalidation-comparison.md:31-36` — verify the exact default `Violation.render` sentence; the comment presents it as executable output.
- `docs/90-comparisons/20-fluentvalidation-comparison.md:78-79` — confirm whether `Schema.check` runs the constructor and preserves the original model or constructs a new one; this comparison describes both as one operation.

- `docs/07-result-handling/80-collecting-errors.md:112-127` — confirm how `result.list` distinguishes an already-collected `Result<_, SignupError list>` from a single-error binding, especially when inference has not fixed the error type; the flattening claim is important to copyable composition.

- `docs/05-parsing/_index.md:61-73` — confirm whether invariant-culture thousands separators and exponent/overflow handling match the stated accepted forms across numeric parsers; the table reads as an exact grammar.

- `docs/04-refined/20-catalog.md:88-90` — confirm the exact return types and out-of-range behavior of `skip`, `item`, `truncate`, and `map2`; the paragraph describes clamping and totality but then says `skip` returns an ordinary list.

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

- `docs/06-data/50-testing-schema-guarantees.md:26-27,116` — confirm whether `SchemaGen.model` checks generated results with `Schema.check` after parsing, and how unsupported constraints or constructor failures are surfaced; the prose promises accepted models but the example unwraps generation only.

## Unclear or misleading

- `docs/95-notes/_index.md:23` — promises benchmarks “on .NET and Fable,” but the benchmarks page contains only .NET runtime tables; remove Fable or link Fable measurements.
- `docs/95-notes/95-packages-and-platforms.md:64-67` — “Everything else ... is available on every target” extends beyond the listed tested JavaScript surface and conflicts with the AOT/Fable page's .NET-only `DateOnly` and `Data.ofJsonDocument` caveat; scope the statement to supported field types and verified targets.
- `docs/95-notes/82-aot-trimming-fable.md:17-26` — “no runtime reflection in hot paths” is qualified by optional `IServiceProvider` resolution, but new users are not told which package supplies `Service<'service>` or whether it is included in the Fable set; identify the API owner and platform boundary.
- `docs/95-notes/80-benchmarks.md:54-58` — 14.19 us compile versus 2.42–3.10 us decode is roughly 4.6–5.9 decodes, not “two to three times one decode”; correct the comparison or specify another denominator.
- `docs/95-notes/80-benchmarks.md:138-143` — table labels .NET 10.0.300 “this package's target,” although that is an SDK version and the notes page front matter says `net8.0`; distinguish SDK, runtime, and target framework.

- `docs/90-comparisons/20-fluentvalidation-comparison.md:10-11,58,85` — the public `Customer` record can be created without parsing, so “cannot exist in an invalid state,” “every Customer in the program passed the boundary,” and “Invalid object exists? No” are false as written. Limit the claim to values returned by `Schema.parse`, and note that the shown `Name` rule permits empty text.
- `docs/90-comparisons/20-fluentvalidation-comparison.md:68-71` — says the same declaration produces UI metadata and JSON codecs as consequences of inspectable constraints; distinguish schema structure from constraints, and note that trusted codec decoding does not enforce value rules.
- `docs/90-comparisons/30-zod-comparison.md:11,26-28` — claims a record schema's constructor/getter alignment is checked field by field, but the comparison does not show a getter or explain the mechanism; readers cannot tell which guarantees are compile-time versus runtime.
- `docs/90-comparisons/30-zod-comparison.md:31-33` — “on par with System.Text.Json” reads as a broad performance claim without a workload or benchmark; specify the measured operation or remove it.
- `docs/90-comparisons/05-dataannotations-fluentvalidation-comparison.md:83-86,93-98` — categorical claims that DataAnnotations rules cannot be reused and FluentValidation rules cannot be shared disregard reusable custom attributes, `Include`, and reusable validators; specify that the *declaration attached to a property/type* is not the same free-standing inspectable value.

- `docs/07-result-handling/50-collections.md:123-124` — says an infinite sequence will not terminate through *any* traversal, but fail-fast `traverse`/`sequence` can terminate on an early error. Qualify this for successful or all-errors traversal.
- `docs/07-result-handling/80-collecting-errors.md:150-160` — the form supplies string `"12"` for an `int` field but the example does not show a text-to-int parser; clarify whether `Data.ofNameValues` plus `Schema.parse` converts it and show the actual diagnostic shape rather than a schematic comment.
- `docs/07-result-handling/70-result-ce.md:132-146` — a `result` block illustrates filesystem I/O despite the section's description-only context; label it explicitly as host-owned illustrative code so readers do not infer this package opens readers.

- `docs/05-parsing/_index.md:95-99` — `OutOfRange` also covers numbers too negative for the destination type; the sample message “too large” mislabels that case.
- `docs/05-parsing/_index.md:186-206` — “a refinement admits it into a domain type” but `quantity` returns the guarded `int`, not a refined type. Either construct a refinement or describe this as parsing followed by a value check.

- `docs/04-refined/60-tutorials/10-order-totals.md:40-58` — public `OrderLine` and `Order` records can be constructed with unchecked quantities, prices, or even default/null refined fields; “none of those facts ... can be false” and “rejected here and nowhere else” overstate the guarantee. Make the aggregate private or scope the claim to the shown constructor.
- `docs/04-refined/60-tutorials/10-order-totals.md:112-114` — payable cannot exceed subtotal only if subtotal is nonnegative; the public records permit negative prices, and decimal arithmetic may overflow. Qualify the claim or enforce the preconditions in the type.
- `docs/04-refined/40-domain-values.md:179-187` — `ContactEmail.normalise` wraps lower-cased text without rechecking `Constraint.email` or `maxLength`; explain why this transformation preserves the precise configured rule, especially if the rule is later changed.
- `docs/04-refined/_index.md:163-165` — says a custom `CustomerId` resolves from `field _.Id` immediately after defining `Refinement.define`, while the tutorial at `docs/04-refined/60-tutorials/20-customer-id.md:149` says registration is needed. Show registration here or say the field needs an explicit schema until registered.

- `docs/03-constraints/_index.md:30-35` — quick example prints `Error [ Blank; InvalidLength ... ]` as if `check` returned a list, but this page says it returns `Result<unit, Violation>`; the structured `Atomic`/group representation shown in the interpreted-rules guide differs. Show the actual `Violation` or label this as schematic output.
- `docs/03-constraints/40-localization/_index.md:65-73` — “Using the violation and renderer from above” mixes an `atLeast 13` violation with a French renderer containing only presence/cardinality/email/name translations; the quoted English outputs for `message` and capitalized `Name` are not what that lookup demonstrates. Use `Renderer.english` for those outputs or show the actual fallback/translated noun.
- `docs/03-constraints/30-constraints.md:55-69` — `customWith` is described as returning its own violation, yet the example wraps `Constraint.check (Constraint.oneOf ...)` and says the enclosing rule is opaque. Explain whether a failed custom rule exposes an interpreted atom and how that differs from inspectability of the rule itself.
- `docs/03-constraints/40-localization/40-catalogue.md:11` — “Every key Reified can produce” omits keys supplied through `Constraint.customLocalized`; qualify this as every built-in key.
- `docs/03-constraints/45-adding-a-language.md:261-267` — “What the catalogue handles” lists imperative actions without a lead-in or negation; “reproduce the key list by hand” contradicts the page’s generation advice. Introduce the list as things readers need *not* do.


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

- `docs/06-data/10-tutorial.md:126-133` — `variant "present"` is declared, but the displayed case list starts with `"valid"`; update the expected output.
- `docs/06-data/10-tutorial.md:203-225` — the narrative says “If the actual plan is `"free"`” while the shown `expected` plan is `"free"` and the actual response has `"pro"`; the following example reverses both values. Separate the illustrative comparison from the preceding one.
- `docs/06-data/31-how-to-build-test-cases.md:122-133` — `cases |> List.map _.Name` refers to the four independent cases, not the eight `matrixCases` whose names are printed. Use `matrixCases`.
- `docs/06-data/21-how-to-test-produced-json.md:62-63` — calls `customer.id` a generated identifier, though the fixture fixes it to `"c-123"`; say “present identifier” or introduce generation.
- `docs/06-data/50-testing-schema-guarantees.md:46-52,74-89` — `Booking.shift` uses `DateTimeOffset.AddDays`, which can throw near date limits; describing it as a total transition is misleading. Bound the input or make failure explicit.

## Too long

- `docs/95-notes/95-packages-and-platforms.md:54-67` and `docs/95-notes/82-aot-trimming-fable.md:92-116` — repeat the same Fable quotation target/version table and `field`/`fieldAs` example; keep detailed compatibility in one page and link it from the package inventory.

- `docs/90-comparisons/05-dataannotations-fluentvalidation-comparison.md:64-76,108-120` — repeats the same `Constraint.guard Constraint.email` example twice with only error mapping changed. Show both mappings together once.
- `docs/90-comparisons/05-dataannotations-fluentvalidation-comparison.md:77-120` and `docs/90-comparisons/20-fluentvalidation-comparison.md:8-89` — two FluentValidation comparisons repeat the rules-as-data argument; link the focused Schema comparison from the Constraint comparison and keep their different scopes explicit.

- `docs/07-result-handling/70-result-ce.md:72-128` — repeats the binding example with explicit types and then a second failure demonstration; one typed annotation and one failure example would teach the same behavior with less repetition.

- `docs/05-parsing/_index.md:130-184` — generic optional/defaulting helpers and named wrappers repeat the same absence-versus-malformed rule and examples; condense the wrappers to a short list or table.

- `docs/03-constraints/21-constraint.md:53-85,140-191` — the same DSL/module/type-directed-binding introduction appears twice on the core type page and again in `dsl.md`. Keep one small example here and direct readers to the DSL guide for details.


- `docs/01-getting-started/_index.md:137-232` — after the complete Signup walkthrough, the page restarts with a problem statement, a standalone constraint, a refinement, and another schema. Consider retaining one short conceptual contrast here and linking to focused guides for the second set of examples.

- `docs/02-schema/01-quickstart.md` spans plain parsing, refined types, constraints, private aggregates, drafts, updates, and JSON Schema. Keep “First schema and parse” and “Read failures” here; move refined fields to “Refined fields in Schema,” field constraints to “Boundary-specific field rules,” and private Booking/draft/update flow to “Construction guarantees.”
- `docs/02-schema/65-versioned-contracts.md` mixes the manual contract workflow and a long generator evolution tutorial. Split into “Handwritten versioned contracts” and “Generated version series: freezing and migrating.”
- `docs/02-schema/85-examples.md` embeds entire example program files. Replace with a short explanation, link to runnable source, and focused relevant excerpts; keep the complete program at the linked location.

- `docs/06-data/_index.md:106-140` and `docs/06-data/20-declaring-and-editing.md:73-114` repeat the same control-flow fixture and output. Keep the full example in the focused page and shorten the landing-page example.

## Reference material in guides

- `docs/95-notes/95-packages-and-platforms.md:42-49,74-78` — package/install and tooling availability tables are useful reference material; keep them in Notes as the central install inventory and link them from guides rather than repeating setup lists.

- `docs/90-comparisons/90-fstoolkit-comparison.md:24-32` — the API-equivalence table is useful as a comparison quick reference; keep it here rather than duplicating it across Result guides, and link the Result reference for precise signatures.

- `docs/07-result-handling/50-collections.md:103-112` — the four complete traversal signatures are reference-shaped; keep the list-output warning here and link the signatures from the API reference.

- `docs/05-parsing/_index.md:56-69` — the exhaustive parser/accepted-form table is useful reference material; consider keeping a smaller selection in the walkthrough and linking the complete list from the Parse API reference.

- `docs/03-constraints/40-localization/40-catalogue.md:14-95` — the exhaustive key/argument/default-English tables are reference data (also exposed by `Catalogue.keys`/maps); keep the runtime maps discoverable and consider placing the tables beside the catalogue API reference rather than in the localization walkthrough.


- `docs/01-getting-started/02-what-a-schema-gives-you.md:37-45` — the seven-API inventory is useful orientation, not an exhaustive reference; keep it here, but link the `SchemaGen` entry directly to its own usage/reference page rather than only the generic Data section at line 152.

- `docs/02-schema/20-dsl.md`, “Constraint equivalents”: move the operation/attribute mapping to `docs/api/Reified.Schema.md` front matter (and retain a short link to Derivation Attributes).
- `docs/02-schema/46-advanced-union-handling.md`, “Ready-made compatibility formats” and “Build a representation directly”: move exhaustive `UnionRepresentations`/`UnionPayloadStyle` options to their API entity reference enrichment; keep one compatibility recipe in the guide.
- `docs/02-schema/60-derivation/10-msbuild.md`, “MSBuild properties”: put the exhaustive build-property defaults in the build tooling reference; keep installation and a link in the guide.
- `docs/02-schema/60-derivation/20-attributes.md`, attribute tables: this is already a reference-shaped page; consider reference enrichment for the derivation attribute entities rather than repeating the tables in tutorial pages.
- `docs/02-schema/65-versioned-contracts.md`, “Version sources”: move the exhaustive `VersionSource` case list to `docs/api/Reified.Contract.md`; keep one example per use case.
- `docs/02-schema/30-redisplay-and-field-errors.md`, “The Schema catalogue”: move exhaustive message-key/argument/default table to the appropriate Schema error API reference front matter.

- `docs/06-data/18-dsl.md:81-118` — the complete DSL name and edit mapping tables are reference-shaped; keep a short comparison example here and link the exhaustive vocabulary from the API reference.
- `docs/06-data/21-how-to-test-produced-json.md:169-199` — the complete pattern inventory and return-type summary belong beside the matching API reference; retain a few representative patterns in the walkthrough.

## Missing

- `docs/95-notes/95-packages-and-platforms.md:11-18,69-84` — give a minimal Fable project install/compile recipe or point to one; `dotnet add package` alone does not show how a JavaScript client consumes these packages or generated output.
- `docs/95-notes/82-aot-trimming-fable.md:78-90` — bundle-size estimates omit esbuild/Fable versions, build settings, and a reproducible command or linked recipe; readers cannot compare their own bundles to these numbers.

- `docs/90-comparisons/_index.md:4-10` — the landing description mentions only Schema, despite the Constraint and Result comparisons; add links and a sentence distinguishing the three comparison scopes.
- `docs/90-comparisons/20-fluentvalidation-comparison.md:38-60` — the example uses `Constraint` and `Data` without showing imports or package setup, and uses `printfn` in a boundary example; show a complete application-host example or explicitly label it a fragment.
- `docs/90-comparisons/30-zod-comparison.md:14-22` — table entries such as `schema<...>`, `construct ctor`, and `Schema.refine` are shorthand rather than copyable F#; link one complete schema example and identify the shorthand.

- `docs/07-result-handling/80-collecting-errors.md:74-75` — `NonEmptyList.ofList` is named without identifying its package or defining it; show a local conversion or link its owner so the suggestion is copyable.

- `docs/05-parsing/_index.md:130-159` — clarify how `Some ""` and `Some "   "` behave with `optional`/`optionalOr`: these are present inputs that produce `MissingValue`, not absent values receiving a fallback.

- `docs/04-refined/60-tutorials/10-order-totals.md:44-55` — the tutorial promises building an order from untrusted input but never constructs `Order` from all four admitted fields; add a complete boundary function, including error mapping for heterogeneous failures and checking the line collection.
- `docs/04-refined/50-schema.md:99-106` — `Schema.convert decimal int` narrows a decimal on encoding; explain what happens with fractional or out-of-range values, or choose a genuinely reversible conversion for the first example.

- `docs/03-constraints/40-localization/20-custom-rules.md:14-30` — the page’s first F# example omits `open Reified`, unlike adjacent pages, and does not show a check and rendered translation. Add a complete minimal invocation so a new reader can verify the custom key and argument path.
- `docs/03-constraints/45-adding-a-language.md:219-259` — the coverage test checks only bare `Catalogue.keys`, while the skeleton emits `.one`/`.other` instead of the bare key for pluralized entries; a fully filled skeleton fails this proposed test. Supply a coverage predicate that accepts bare OR both required plural forms, and distinguish contextual overrides from base coverage.


- `docs/02-schema/47-schema-or-generated-dto.md:51-77` — strategy 2 lacks a runnable boundary example with a defined byte payload, actual wire-to-domain types, and structural decode error handling; the current fragment cannot serve as a copyable starting point.

- `docs/01-getting-started/02-what-a-schema-gives-you.md:150-152` — the seventh promised job has no runnable generation example, unlike the preceding jobs. Add a minimal `SchemaGen` call or label this section as a pointer to a separate recipe.

- A single decision matrix explicitly contrasting `Schema.parse`, `Schema.check`, `Json.deserialize`, `Json.tryDeserialize`, and `Contract.parse`, including validation run, constructor behavior, input type, error type, and when to trust a payload.
- A complete HTTP JSON boundary recipe from request bytes through `Json.parseData`, `Schema.parseRetainingInput`, localized `SchemaErrors`, and an application-owned response; current examples demonstrate pieces separately.
- A compact error taxonomy and mapping recipe: `Violation`, `ParseError`, `SchemaError`, `SchemaErrors`, `JsonCodecException`, `MigrationError`, and `ContractError` with conversions and paths.
- Documentation for concurrency/caching of compiled codecs and deferred recursive schemas; the guidance to compile once invites shared use but does not say whether it is safe.
- A single glossary for “trusted,” “admission,” “interpreted,” “opaque,” “canonical,” “wire,” and “domain.”

- `docs/06-data/10-tutorial.md:136-149` — the tutorial calls `Reified.Json.parseData`, but the Data-only install at `docs/06-data/_index.md:20-26` does not supply it; state that this step also requires `Reified.Schema` and show the install command before the example.
- `docs/06-data/15-what-it-does.md:67-69` — direct `Data.Number` construction permits invalid JSON number tokens; explain what `Data.Json.render` does with such values, especially when input did not come through `num` or the JSON parser.
- `docs/06-data/15-what-it-does.md:112-113` — duplicate names use last-field lookup, but partial matching consumes duplicates. Add a small example showing which occurrence an `at` path inspects versus `containing`.
- `docs/06-data/30-converting-data.md:120-129` — show how `Data.ofCliArgs` treats negative numeric option values and a missing option value; the “starts with `-`” rule turns both into flags or positionals in a surprising way.
- `docs/06-data/31-how-to-build-test-cases.md:112-114` — explain the error type/exception when `matrix` exceeds 256 combinations or a dimension has no variations, so test authors can diagnose case-generation failure.

## Suggestions for humans and LLMs

- `docs/95-notes/80-benchmarks.md:31-46,126-152` — put a short “which path to benchmark” reminder beside the multi-runtime comparison: its trusted codec figures do not measure untrusted boundary admission, whose costs are reported separately at lines 60-79.
- `docs/95-notes/82-aot-trimming-fable.md:40-55` — separate published-package advice from the repo-only `ProjectReference` props path; an assistant copying the latter into an installed application will point at a nonexistent local file.

- `docs/90-comparisons/05-dataannotations-fluentvalidation-comparison.md:67-75,114-118` — both email snippets are marked `no-check` and omit the caller's error union; provide one complete example of `InvalidEmail` with and without a `Violation` so assistants do not invent its shape.
- `docs/90-comparisons/30-zod-comparison.md:39-45` — link supported Fable targets and a complete `RetainedParseResult` example; the one-line API names do not show how to obtain a retained parse result.

- `docs/07-result-handling/60-observing.md:70-79` — the logging example includes raw `AgeNotANumber of string`; advise redacting sensitive raw input before logging, even when the callback itself is total.
- `docs/07-result-handling/10-quickstart.md:63-69` — the first accumulation example is a no-check fragment with undefined functions; use the complete `signupAll` example from the collecting-errors page or mark the displayed output explicitly as illustrative for copy/paste readers.

- `docs/05-parsing/_index.md:50-51` — warn that `ParseError` retains raw input, which may be sensitive; avoid logging or returning it verbatim without considering redaction.

- `docs/04-refined/60-tutorials/20-customer-id.md:108-115` — `index` and `range` work for any comparable key, including zero; identify which operation specifically relies on positivity, or present `CustomerId` as nominal identity rather than proof that positive numeric wrappers earn their keep.

- `docs/03-constraints/60-reference-app.md:39-45` — “must print on one line” is not enforced by the length-only checks shown; remove that claim or add a newline rule, so copied validation does what its comment promises.
- `docs/03-constraints/40-localization/30-advanced-rendering.md:169-180` — the `MessageRequest` type sketch and host-owned `icu` in a `no-check` block look like an API declaration plus runnable resolver. Label the type as illustrative and show a concrete `Map`-based resolver separately for readers copying code.


- `docs/02-schema/85-examples.md:147-148` — the mirrored example invokes `DateTimeOffset.UtcNow` twice for a publish interval. Use fixed endpoints in teaching examples so copied examples are deterministic and the interval is unambiguous.

- `docs/01-getting-started/03-where-to-go-next.md:10` — the page has both a front-matter title and an explicit H1, unlike its sibling pages; use the same heading convention throughout Getting Started to avoid duplicate titles in rendered navigation.

- `docs/06-data/21-how-to-test-produced-json.md:140-158` — `decimal token` in a `satisfying` predicate can throw for a syntactically valid JSON number outside decimal range; use a safe parse so a mismatch is reported instead of an exception.
- `docs/06-data/50-testing-schema-guarantees.md:10-11,26-34` — the copyable `SchemaGen` sample imports an unpublished repository-only namespace. Provide an actual application-local adapter recipe or label this as an internal-only illustration rather than an available public API.
- `docs/06-data/30-converting-data.md:204-215` — `lookupPath` maps missing paths to `Data.Null`, collapsing absence and explicit null; prefer `tryFindPath` when a boundary must distinguish them.

- Keep `llms.txt` as a task router, but reconcile its asserted navigation order with the visible section ordering; add a short API-selection decision table and explicit trust-boundary warning.
- Put actual F# return types and failure types immediately beside every first-use API example; avoid pseudo-output that resembles executable code without showing its setup.
- Link every performance claim to a specific workload and runtime/version, and mark relative results as benchmark-specific rather than general guarantees.
- Make examples consistently self-contained or label them explicitly as fragments; use the same Signup/Booking model across adjacent pages only when rules and expected output really agree.
- Add “verified by docs build” versus “illustrative pseudocode” metadata to examples so coding assistants do not copy placeholders as runnable F#.
