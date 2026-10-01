# Doc examples that are poor or don't communicate intent well

Compiled retrospectively while clearing the no-check verification backlog (see TASKS.md /
git log on `json-codec-improvements`). These are readability/communication issues the
per-batch review subagents and I found, not simple no-check-flag removals. Most were
already improved in-session; a few are left as open follow-ups. Grouped by file.

## Fixed in-session, noted here as a record of the pattern

- **`docs/02-schema/40-refined-values.md`** — "Canonical refinement inference inside a
  field" section silently redeclared `ContactEmail` with a weaker constraint
  (`Constraint.email` alone) right after the page had called a stronger
  `Constraint.all [...]` version "the complete invariant" two sections earlier. Reusing a
  type name across sections with a different, undocumented shape is the single most common
  readability defect found across this backlog.
- **`docs/04-refined/40-domain-values.md`** — a "running example" that implied it was
  building up (ContactEmail definition shown three times, each time adding a constraint)
  actually *reverted* to the single-constraint form after the "Combine constraints"
  section, with a `// ... as above` comment pointing at the wrong section.
- **`docs/02-schema/70-patterns/20-legal-transitions.md`** — the `shift` example's minimal
  `Booking` redeclaration (only `start`/`finish`/`shift`, no `create`/`BookingError`) read
  as a different, unconstructable type with no signal it was "the same Booking, abbreviated."
- **`docs/02-schema/35-trusted-construction.md`** — the page's own no-draft `Booking`
  differs in shape (public record, string error, no `RequireQualifiedAccess`) from the
  draft+typed-error `Booking` the patterns pages it recommends as "complete project-sized
  examples" actually use, with no note reconciling the two.
- **`docs/06-data/15-what-it-does.md`** — the `Data.Object` render example showed literal
  `{{ }}` (an F#-interpolation escape sequence that leaked into the "expected output"
  comment) instead of the real single-brace output; and the page's "in JSON it renders as"
  framing conflated the human-readable `Data.render` with the actual JSON renderer,
  breaking down exactly at the one case (`Data.Object`) where the two differ.
- **`docs/03-constraints/40-localization/30-advanced-rendering.md`** — the operand-
  formatting example's `format` helper had both match arms call the same function
  regardless of the branch, so the "the suffix, when you need it" example never
  demonstrated using the suffix at all — a reader copying it would get nothing useful.
- **`docs/06-data/50-testing-schema-guarantees.md`** — `Booking.shift 10` (int literal into
  a function requiring `float`) is a genuine type error hidden behind a no-check flag that
  had to stay (FsCheck isn't resolvable in this audit pipeline), so the compiler could never
  have caught it; found only by a review pass reading the code by hand.
- **`docs/04-refined/60-tutorials/20-customer-id.md`** — `CustomerId.create 0`'s expected-
  output comment claimed a fabricated `Error [ OutOfRange (GreaterThan "0", Some "0") ]`
  shape; the block had no no-check flag (so the *code* was being compiled), but a wrong
  comment is invisible to a compiler. Real shape is
  `Error (Atomic (Expected (RelationAtom (Compared (GreaterThan, Integer 0L)), Some (Integer 0L))))`.
  Same page also cited `Refinement.defineAll`/`Refinement.defineWithCheck`, neither of
  which exists.

## Open follow-ups (not yet applied as of this writing)

- **`docs/03-constraints/45-adding-a-language.md`** — the `Violation.toMessageTree` recipe's
  `render` example buries `yourConjunction`/`yourDisjunction` (the actual point) under
  several lines of leaf-case boilerplate; a reviewer suggested trimming/commenting it so the
  two custom-joining calls are the visual focus. Low priority, still open.
- General pattern worth a maintainer pass: several tutorial/pattern pages reuse the same
  domain name (`Booking`, `ContactEmail`, `Email`, `SignupError`) for deliberately different
  shapes across sections or across pages, relying on `isolated` compilation to make each
  one compile but giving the reader no visible signal that the "same-looking" type changed
  shape. Every instance found so far has been patched with an explicit sentence
  ("continues the illustration above," "a fresh, unrelated example," cross-page notes
  reconciling two shapes), but the pattern itself keeps recurring, suggesting the doc site
  could use a house convention (e.g. always renaming a reused-but-reshaped type, or always
  adding a one-line reset note) rather than deciding case by case.

## Why this file exists

`AGENTS.md`'s "Writing" section and the per-batch review-subagent instructions already ask
for concrete, non-generic prose and for catching stale/misleading examples; this file is a
durable summary of what that process actually turned up, in case a future pass wants to
check for recurrence of the same defect classes rather than re-discovering them per file.
