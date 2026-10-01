---
title: Model Legal Transitions
weight: 20
description: Keep trusted aggregates valid by exposing named updates instead of unrestricted record copies.
targetFramework: net8.0
---

# Model legal transitions

Construction is only the first guard. A valid value can become invalid when application code updates it.

A **transition** is a named operation that changes one valid state into another, such as completing an order or moving a
booking. It may return an error when the requested change is not allowed.

Keep updates in the aggregate module and name them after the business action. Callers use operations such
as `changeEnd`, `cancel`, or `complete` instead of modifying storage fields.

## Make uncertain changes fallible

Changing one end of a booking can break its date relationship, so reuse the constructor.

```fsharp isolated
open System

type BookingError = EndBeforeStart

type BookingDraft = { Start: DateOnly; End: DateOnly }

type Booking =
    private
        { Start: DateOnly
          End: DateOnly }

[<RequireQualifiedAccess>]
module Booking =
    let start booking = booking.Start
    let finish booking = booking.End

    let create (draft: BookingDraft) =
        if draft.Start <= draft.End then Ok { Start = draft.Start; End = draft.End }
        else Error BookingError.EndBeforeStart

    let changeEnd newEnd booking =
        create
            { Start = start booking
              End = newEnd }
```


The type tells the caller that the change can be refused:

```fsharp no-check reason="proposedEnd/booking/save/showDateError are the caller's own values and functions; not ones this page can construct standalone."
match Booking.changeEnd proposedEnd booking with
| Ok changed -> save changed
| Error BookingError.EndBeforeStart -> showDateError ()
```


## Keep preserving changes total

Some operations preserve the invariant by their construction. Shifting both dates by the same number of days cannot
reverse their order.

```fsharp isolated
open System

type Booking =
    private
        { Start: DateOnly
          End: DateOnly }

[<RequireQualifiedAccess>]
module Booking =
    let start booking = booking.Start
    let finish booking = booking.End

    let shift days booking =
        {
            Start = (start booking).AddDays days
            End = (finish booking).AddDays days
        }
```


This function can return `Booking` directly. Keep it inside the module that can see the private representation.

## Use drafts for several user edits

An edit screen often changes several fields before submission. Convert to a draft, edit it, then call the constructor
once.

```fsharp no-check reason="booking/proposedStart/proposedEnd/save/redisplay are the caller's own values and functions, and Booking.toDraft needs a toDraft function this fragment doesn't declare; not a standalone compilation unit."
let edited =
    booking
    |> Booking.toDraft
    |> fun draft ->
        { draft with
            Start = proposedStart
            End = proposedEnd }

match Booking.create edited with
| Ok booking -> save booking
| Error error -> redisplay edited error
```


Do not pass the draft into business functions that expect the invariant to hold.

## Use types for important lifecycle states

When operations differ sharply by state, use separate types so unavailable transitions are absent from the interface.

```fsharp no-check reason="val-only member declarations are a signature-file (.fsi) shape; this sketch of Order's public surface is not a standalone implementation-file compilation unit."
type DraftOrder
type SubmittedOrder
type CancelledOrder

[<RequireQualifiedAccess>]
module Order =
    val submit:
        DraftOrder ->
        Result<SubmittedOrder, SubmitError>

    val cancel:
        SubmittedOrder ->
        Result<CancelledOrder, CancelError>
```


`Order.submit cancelledOrder` cannot compile because the function requires `DraftOrder`.

Use this for lifecycle states that change what operations are legal. A single discriminated union field is simpler when
all states share the same operations and callers already handle them by pattern matching.

## Keep operation policies separate

A transition belongs to the aggregate when the rule is always true for those related values. Tenant permissions,
current time, feature flags, and operation-specific approval rules belong outside it.

Apply those requirements with ordinary result-returning functions after intrinsic construction has succeeded.
