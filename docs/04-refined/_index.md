---
weight: 4
title: Refined
type: docs
description: Domain types whose invariant removes work from the code that uses them.
targetFramework: net8.0
---

# Refined

`Reified.Refinements` supplies types that carry an invariant, together with the operations that
invariant makes possible. Guarded construction is how a value is admitted, but it is not
the reason to reach for a refined type — a wrapper that only checks on the way in leaves
callers unwrapping it at first use, and the invariant buys nothing after the boundary.

The types here are chosen so that later code can be simpler:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let total = NonEmptyList.reduce (+) lines   // no seed, no empty case, no option
let largest = NonEmptyList.max lines        // total
let ratio = UnitInterval.multiply a b       // closed: still in [0, 1]
```


`NonEmptyList.max` returns a value rather than an option because the type makes the empty
case unrepresentable. That is the test a type has to pass to belong here: it should make a
partial operation total, guarantee a property later operations rely on, encode a
relationship between values, preserve an invariant across a useful family of operations,
or remove a branch from consumers rather than only from construction.

Validation that fails that test belongs in a constraint instead. Trimmed text and slugs make
nothing later total or simpler: no operation on a string needs the ends to be free of
whitespace, and none needs a particular pattern. So they are `Constraint.trimmed` and
`Constraint.pattern` on an ordinary `string`, not types. See
[When not to make a type](/refined/catalog.html#when-not-to-make-a-type).

`Reified.Refinements` depends only on `Reified.Constraint`. It does not
[parse text](/parsing/index.html) and does not normalize input.

```sh
dotnet add package Reified.Refinements
```


```fsharp
open Reified
open Reified.Refinements
```


## Admission

Every built-in type has a constructor returning `Result<'refined, Violation>`:

```fsharp
let name : Result<NonBlankString, Violation> = Refine.nonBlankString "Ada"
let lines : Result<NonEmptyList<string>, Violation> = Refine.nonEmptyList [ "a"; "b" ]

Refine.nonBlankString "  "  // Error [ Blank ]
Refine.nonEmptyList []      // Error [ InvalidLength (MinimumLength 1, Some 0) ]
```


Some types also offer a **total** constructor, which is the one to prefer when the input
has an obvious correct reading:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let lines = NonEmpty(first, rest)              // cannot fail
let window = Interval.between finish start     // cannot fail: orders the pair
let ratio = UnitInterval.clamp 1.5             // cannot fail: clamps to 1.0
```


Read the underlying value through the `Value` member or the module's `value` function.

## Where the invariant pays

Start with the smallest version of the contrast — one partial operation becoming total:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
List.max lines            // throws on an empty list
NonEmptyList.max lines    // total: returns the value
```


Avoiding the exception means writing the option version by hand, and then every caller
unwraps it:

```fsharp
let tryMax lines =
    if List.isEmpty lines then None else Some (List.max lines)
```


That option is the empty case travelling downstream. `NonEmptyList` settles it once, at
construction, and every later caller reads a value.

The same saving shows up in aggregates:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
// plain: every caller re-establishes what is already true
let averageLine (lines: OrderLine list) =
    if List.isEmpty lines then None
    else Some (List.sumBy _.Total lines / decimal lines.Length)

// refined: the empty case cannot arise, so there is nothing to return an option for
let averageLine (lines: NonEmptyList<OrderLine>) =
    NonEmptyList.averageBy _.Total lines
```


The refined version is shorter, not just safer. That is deliberate: the collection types
carry the ordinary list vocabulary — `sum`, `sumBy`, `average`, `choose`, `countBy`,
`item` — as well as the operations the invariant makes total. A refined type that only
offered the clever operations would push you back through `toList` for everyday work, and
the invariant would be lost halfway down the pipeline. See
[the catalogue](/refined/catalog.html#everyday-operations).

[Order Totals](/refined/tutorials/order-totals.html) works this through on a realistic domain.

## There are no refined numbers

F# cannot propagate an invariant through arithmetic the way a refinement-typed language
can, so a `PositiveInt` would have to re-establish "greater than zero" at every step. With
unchecked integer arithmetic — `Int32.MaxValue + 1` is negative — that means returning
`Result` from addition, and a `Result` per arithmetic step is bulk that hides mistakes
rather than catching them.

Numeric ranges are constraints instead:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
field _.Quantity { constrain (Constraint.greaterThan 0) }
```


`FiniteFloat` is the exception that proves the rule: it is worth having because `NaN` and
infinity silently destroy an **aggregate** — `List.average [ 12.5; 3.0; nan; 8.25 ]` is
`NaN` — not because of arithmetic or ordering. See
[the catalogue](/refined/catalog.html#floating-point).

## Define your own

The machinery is public, so an application type gets the same treatment:

```fsharp
type CustomerId =
    private
    | CustomerId of int

    member this.Value =
        let (CustomerId value) = this
        value

module CustomerId =
    let refinement = Refinement.define (Constraint.greaterThan 0) CustomerId _.Value
    let create value = Refinement.create refinement value
```


`Refinement.constraints` exposes the same rules to Schema and other interpreters, so the
type describes itself at a boundary without restating the rule.
[Customer Id](/refined/tutorials/customer-id.html) builds one end to end, including its schema.

Once the type exists, a schema field of that type needs nothing but the field: `field _.Id`
resolves the refinement, and a failure at that field is reported with its path alongside every
other field's. See [Schema integration](/schema/index.html).

## Read next

1. [Order Totals](/refined/tutorials/order-totals.html) uses the built-in types in anger.
2. [Built-in Refined Values](/refined/catalog.html) lists what each type buys you.
3. [Customer Id](/refined/tutorials/customer-id.html) defines a refined type of your own.
4. [Define Refined Types](/refined/domain-values.html) is the reference for `Refinement`.
5. [Schema Integration](/schema/index.html) applies refinements at structured boundaries.
6. [Compose Parse and Refinement](/refined/composition.html) maps failures into application errors.
