---
weight: 35
title: Construction Guarantees
type: docs
description: Which correctness claims Schema can enforce, and which require an invariant-preserving F# type.
targetFramework: net8.0
---

# Construction guarantees

This page shows which correctness claims Schema can enforce and which claims require an invariant-preserving F# type.

An **invariant** is a rule that must hold for every valid value, such as a booking start date not following its end date.
An **aggregate** is a group of related values created and updated as one thing.

[Getting Started](/getting-started/index.html) introduces refined fields, field constraints, and the private-model-plus-draft
shape in order. This page is the reference behind those choices: what each level promises, where the promise stops, and
how updates work at each one.

## Choose the guarantee you need

There are four useful levels:

1. A raw, wire, or draft value is editable and proves nothing.
2. A successful `Schema.parse` or `Schema.check` result passed that operation's gates.
3. A private refined field preserves one field-local invariant wherever the value is used.
4. A private aggregate plus controlled transitions preserves relationships between fields.

Choose the simplest level that prevents a real application problem. Stronger representations add more
construction and update functions, so use them where callers benefit from relying on the guarantee.

## The limit

A library can guarantee the result of its own functions. It cannot prevent a caller from using another public
constructor:

```fsharp
open Reified
open Reified.SchemaDSL
open System

type Booking =
    { Start: DateOnly
      End: DateOnly }

let bookingSchema =
    schema<Booking> {
        field _.Start
        field _.End
        constructResult (fun start finish ->
            if start <= finish then Ok { Start = start; End = finish }
            else Error "Start must not be after end.")
    }

let invalidAnyway =
    { Start = DateOnly(2026, 7, 20)
      End = DateOnly(2026, 7, 12) }
```


`Schema.parse bookingSchema raw` and `Schema.check bookingSchema value` enforce the constructor rule. The public record
literal does not. There is no honest library-level type guarantee for every `Booking` while that literal remains
available.

## Durable field invariants

Private refined fields make illegal field values unrepresentable:

```fsharp
open Reified
open Reified.Refinements

type WorkspaceName = private WorkspaceName of string

module WorkspaceName =
    let value (WorkspaceName name) = name

    let refinement =
        Refinement.define
            (Constraint.all [ Constraint.present; Constraint.maxLength 80 ])
            WorkspaceName
            value

    let create raw = Refinement.create refinement raw

    let schema : Schema<WorkspaceName> =
        Schema.text |> Schema.refine refinement
```


`Refinement.define` keeps an executable check, readable metadata, total construction, and projection in one value.
`Schema.refine` applies that definition, so direct construction and Schema interpretation share the invariant.

Any `WorkspaceName` is valid because its representation is private and every exposed constructor returns `Result`.
The schema participates in that construction; it is not the sole guardian.

## Durable aggregate invariants

Use a private aggregate representation when every value must satisfy a relationship between fields:

```fsharp isolated
open Reified
open Reified.SchemaDSL
open System

type Booking =
    private
        { Start: DateOnly
          End: DateOnly }

module Booking =
    let create start finish =
        if start <= finish then Ok { Start = start; End = finish }
        else Error "Start must not be after end."

    let start booking = booking.Start
    let finish booking = booking.End

    let schema =
        schema<Booking> {
            fieldAs "start" start
            fieldAs "end" finish
            constructResult create
        }
```


The record schema invokes `Booking.create`, and ordinary callers cannot use a record literal. Updates must also go
through functions that preserve the rule. A private constructor with a public unchecked `with`-style escape hatch is
not complete encapsulation.

A private representation costs record syntax. Callers cannot write `{ Start = s; End = e }` or `{ booking with End = e }`
outside the defining module, and a positional `create start finish` loses the field names that make call sites readable.
The next section restores both without reopening the constructor.

## Drafts

A draft is a public record whose only job is to be assembled and edited freely before admission. Give the private
aggregate a draft type, and make the schema's constructor the one path from draft fields to the domain type. Named
`Reservation` here, to keep this design distinct from the no-draft `Booking` above:

```fsharp
type ReservationDraft =
    { Start: DateOnly
      End: DateOnly }

type Reservation =
    private
        { Start: DateOnly
          End: DateOnly }

module Reservation =
    let create (draft: ReservationDraft) =
        if draft.Start <= draft.End then Ok { Start = draft.Start; End = draft.End }
        else Error "Start must not be after end."

    let toDraft (reservation: Reservation) : ReservationDraft =
        { Start = reservation.Start; End = reservation.End }

    let schema =
        schema<Reservation> {
            fieldAs "start" (fun r -> r.Start)
            fieldAs "end" (fun r -> r.End)
            constructResult (fun start finish -> create { Start = start; End = finish })
        }
```


Construction keeps field names without exposing the representation:

```fsharp
let arrival = DateOnly(2026, 6, 1)
let departure = DateOnly(2026, 6, 5)
let reservation = Reservation.create { Start = arrival; End = departure }
```


This is the same division the versioned contract chain uses. A wire record such as `BookingV1` is also freely
constructible, but it serves transport and versioning; a draft serves local assembly and editing. The two roles can
share a type in small applications. Name the value `draft`, `wire`, or `contract` so the trust boundary stays visible,
and do not pass either shape through business logic as though its schema had changed the record's constructors.

The draft is not a hole in the guarantee. A `ReservationDraft` proves nothing and can hold any field values; only
`Reservation.create` and `Schema.parse` produce a `Reservation`, and both run the same rule. Code that skips the rule must
change the module that owns the representation, which is a visible, reviewable act rather than a quiet record literal
somewhere else in the codebase.

## Updates

`{ value with Field = x }` is the shape F# programmers reach for, and each level of guarantee keeps a version of it.

**Refined fields, public record.** When every invariant is field-local, the record can stay public and `with` needs no
gate. The replacement value already went through its own constructor:

```fsharp
type Workspace = { Name: WorkspaceName }

match WorkspaceName.create "Ada's Workspace", WorkspaceName.create "New Name" with
| Ok initialName, Ok newName ->
    let workspace = { Name = initialName }
    let renamed = { workspace with Name = newName }   // newName: WorkspaceName, proven at creation
    ignore renamed
| _ -> ()
```


This is why refined field types should be the first resort: they leave record syntax, `with`, and structural equality
untouched while making the invalid states unrepresentable.

**Schema-described public record.** Update the record with ordinary `with`, then re-check it to run the schema's
constraints and record constructor against the changed value. With the public `Booking` from the first section:

```fsharp
let booking : Booking = { Start = DateOnly(2026, 6, 1); End = DateOnly(2026, 6, 5) }
let newEnd = DateOnly(2026, 6, 8)
let extended = { booking with End = newEnd }

match Schema.check bookingSchema extended with
| Ok checked -> printfn "%A" checked
| Error diagnostics -> printfn "%A" diagnostics
```


This suits models that remain publicly constructible, where `Schema.check` is the admission decision rather than the type.

**Several fields, private aggregate.** When fields must move together, shifting a booking changes both dates, lower
to the draft, edit with ordinary record syntax, and re-admit:

```fsharp
let shift days reservation =
    let draft = Reservation.toDraft reservation
    Reservation.create { draft with Start = draft.Start.AddDays days; End = draft.End.AddDays days }
```


The `with` expression works on the draft, so callers keep familiar syntax, and the result passes back through the one
authoritative constructor.

Every gated update returns `Result`. That is the honest cost of a cross-field invariant: an edit can break the
relationship, so an infallible `with` on the validated type would be the bypass this page exists to close. When a
specific transition provably preserves the invariant, shifting both dates by the same amount cannot reorder them,
the owning module can expose it as a total function and keep the proof next to the representation.

## Existing typed values

`Schema.check` is for values whose construction history is uncertain:

```fsharp
let importedBooking : Booking = { Start = DateOnly(2026, 6, 1); End = DateOnly(2026, 6, 5) }

match Schema.check bookingSchema importedBooking with
| Ok checked -> printfn "%A" checked
| Error diagnostics -> printfn "%A" diagnostics
```


It recursively checks fields and collections and re-invokes a record schema's constructor. It returns the original
value on success; it does not create a proof wrapper. The guarantee belongs to that successful result flow. A private
domain representation carries a stronger, durable guarantee.

## Recommendation

The reference app uses all four levels deliberately:

- `WorkspaceV1` and `WorkspaceV2` are public wire records.
- `WorkspaceName`, `PersonName`, and `WorkItemTitle` have private representations and checked refinements.
- `Workspace` business transitions accept only refined fields and return `Result` for relational business rules.
- persisted contracts are parsed again when read; contextual production rules run only at the relevant admission
  boundary.

This division keeps schema metadata useful without claiming that metadata overrides F# construction semantics.

For complete project-sized examples, see [Build A Private Aggregate](/schema/patterns/private-aggregates.html),
[Model Legal Transitions](/schema/patterns/legal-transitions.html), and
[Separate Wire And Domain Models](/schema/patterns/wire-and-domain-models.html). Those pages' `Booking` carries a draft type
and a typed `BookingError`, a fuller shape than this page's own no-draft `Booking` example above; both are the
same pattern at different levels of completeness, not two different designs.
