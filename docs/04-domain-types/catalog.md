---
weight: 20
title: Built-in Refined Values
description: The invariant-carrying types the package supplies, and the operations that justify each one.
targetFramework: net8.0
---

# Built-in Refined Values

A refined type earns its place by what it lets you *stop writing*. Each type below makes
some partial operation total, guarantees a property later operations rely on, or removes a
branch from every consumer. A wrapper that only validates at construction is a constraint,
not a type — see [When not to make a type](#when-not-to-make-a-type).

```fsharp
open Reified
open Reified.Refinements
```

## What each type buys you

| Type | Closed under | Made total |
|---|---|---|
| `NonEmptyList<'T>`, `NonEmptyArray<'T>` | `map`, `append`, `rev`, `sort`, `distinct`, `scan`, `truncate` | `head`, `last`, `reduce`, `min`, `max`, `average`, `item` |
| `Interval<'T>` | `between`, `span`, `clamp`, `mapMonotonic` | `contains`, `clamp` |
| `Bounded<'T>` | `clamp`, `map` (re-clamps) | `clamp` |
| `NonBlankString` | `append`, `trim`, `toUpper`, `toLower` | `split` |
| `UnitInterval` | `*`, `complement`, `lerp`, `min`, `max` | `clamp` |
| `FiniteFloat`, `FiniteFloat32` | `negate`, `abs` | aggregates that cannot be silently poisoned |
| `DistinctList<'T>` | `add`, `remove`, `union`, `intersect`, `sort`, `filter`, `map` | `toSet` |

## Collections

`NonEmptyList` carries its non-emptiness in the representation, so the case is public and
you can pattern match on it:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let lines = NonEmpty(firstLine, remainingLines)   // total, no Result

let (NonEmpty(first, rest)) = lines               // total
let total = NonEmptyList.reduce (+) lines         // total, needs no seed
let largest = NonEmptyList.max lines              // total, no option
```

`NonEmptyList.create` admits an ordinary sequence and returns
`Result<NonEmptyList<'T>, Violation>`; `NonEmptyList.ofList` returns an option.

Filtering can remove every item, so `filter` returns an ordinary list and `tryFilter`
returns an option. `traverseResult` applies a fallible mapping across the list and
accumulates every failure rather than stopping at the first.

`NonEmptyArray` stays smart-constructed rather than structural. A head-and-tail
representation would forfeit contiguous storage and indexed access, which are the reasons
to choose an array; the total `head`/`last`/`reduce`/`max` still apply.

`DistinctList` exists for `toSet`: distinct items always produce a set of the same size,
where `Set.ofList` on an ordinary list silently collapses duplicates.

`toMap` and `toMapBy` return a `Result`, because distinctness holds over whole items
rather than over keys — `[ 1, "a"; 1, "b" ]` is a legitimate `DistinctList` whose entries
would collide. They report the collision instead of dropping an entry the way `Map.ofList`
does.

### Everyday operations

A refined collection is still a collection. Alongside the operations the invariant makes
total, each module carries the everyday list vocabulary, so a pipeline never has to drop
back to `List` and re-admit the result:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let total   = NonEmptyList.sumBy _.Total lines        // not List.sumBy on a converted list
let mean    = NonEmptyList.average prices             // total: the divisor is never zero
let biggest = NonEmptyList.maxBy _.Quantity lines     // total: no option
```

The two tiers are worth telling apart, because only the second is a reason to reach for
the type:

| Tier | Operations | What the invariant does |
|---|---|---|
| Parity | `sum`, `sumBy`, `choose`, `countBy`, `tryPick`, `tryFind`, `tryFindBack`, `tryFindIndex`, `pairwise`, `iter`, `iteri`, `fold`, `exists`, `forall` | nothing — these exist so the type is usable without a round trip |
| Earned | `average`, `averageBy`, `item`, `truncate`, `skip`, `map2`, `init`, `replicate`, `scan`, `chunkBySize` | removes the partiality: no raise, no option, no empty case |

`average` is the clearest of the earned ones: `List.average []` raises, while the divisor
here is the length, which is at least one. `item`, `truncate`, `init`, and `chunkBySize`
clamp their count into range instead of raising or emptying the list, so non-emptiness
survives; `skip` and `map2` are total where `List.skip` and `List.map2` raise. Operations
that can genuinely destroy the invariant stay honest: `filter`, `choose`, and `skip`
return an ordinary list, and `tryFilter`/`tryChoose` return an option.

`scan` is non-empty by construction rather than by inheritance — a scan always emits its
seed.

`NonEmptyArray` mirrors the whole set, and `DistinctList` carries the operations that
cannot introduce a duplicate (`sort`, `sortBy`, `rev`, `truncate`, `filter`, `fold`,
`sum`, `sumBy`); `map` and `choose` deduplicate, because neither a mapping nor a chooser
need be injective.

## Intervals and bounds

One generic `Interval<'T>` covers any ordered value. It is always inhabited, so emptiness
is reported as an option rather than by a second type:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let window  = Interval.between start finish     // total: orders its arguments
let overlap = Interval.intersect window other   // Interval option — honest about emptiness
let clamped = Interval.clamp candidate window   // total
```

The ends are `Lower` and `Upper`: they name the two bounds' roles, not a traversal. An
interval has no direction, so `between 5 1` equals `between 1 5`.

That is why there are two constructors. `between` accepts either order and repairs it;
`create` asserts the pair is already ordered and fails when it is not. Reach for `between`
in code, and `create` at a boundary, where an inverted pair is a caller error worth
reporting rather than silently swapping.

`union` returns `None` when the two intervals do not overlap, because joining them would
invent a gap; `span` closes the gap deliberately.

For instants, `DateRange` abbreviates `Interval<DateTimeOffset>` and
`RefinedSchemas.dateRange` uses `start`/`end` on the wire. That is a schema-level naming
choice, not a second type — every `Interval` operation applies unchanged.

`Bounded<'T>` pairs a value with the interval it must stay inside. Bounds are carried at
run time, so `Bounded.clamp` is total and `Bounded.map` re-clamps — a mapping cannot break
the invariant.

## Why there are no refined numbers

There is no `PositiveInt`, `NonNegativeDecimal`, or `NonZeroInt` here, and that is
deliberate.

F# cannot propagate an invariant through arithmetic. A language with refinement types
infers that `a + b` is positive when `a` and `b` are; F# cannot, so every step has to
re-establish the fact by hand. Since integer arithmetic is unchecked —
`Int32.MaxValue + 1` is negative — an addition returning `PositiveInt` would be unsound,
which leaves returning `Result`:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
// what a refined numeric type costs for ((2 + 3) * 4) + 1
PositiveInt.add a b
|> Result.bind (fun s -> PositiveInt.multiply s c)
|> Result.bind (fun m -> PositiveInt.add m d)
```

Nobody writes that. They unwrap, compute, and re-admit — so the type adds bulk at every
use site and buys nothing in return, which is more likely to hide an arithmetic mistake
than to catch one.

Numeric ranges are therefore constraints:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
field _.Quantity { constrain (Constraint.greaterThan 0) }
```

If you want a nominal type for a numeric identifier — where the point is identity rather
than arithmetic — define one over the same constraint. `Refinement` is public, and
[Customer Id](/domain-types/tutorials/customer-id.html) works it through.

## Floating point

`FiniteFloat` excludes `NaN` and the infinities. Its value is that **aggregation means
something**: one bad reading destroys a whole aggregate, silently.

```fsharp
List.sum     [ 12.5; 3.0; nan; 8.25 ]   // NaN
List.average [ 12.5; 3.0; nan; 8.25 ]   // NaN
```

No exception, no obviously wrong number — just a dashboard that reads `NaN` some time
later. Admitting through `FiniteFloat` localises that to the one bad reading at the
boundary. Infinity poisons `sum` and `average` identically, which is why the type excludes
both rather than only `NaN`.

`NaN` also makes `List.contains` and `List.distinct` wrong, since both use IEEE equality
under which `NaN` is not equal to itself.

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
FiniteFloat.negate value      // closed
FiniteFloat.average values    // one Result at the end, not one per step
```

**It is not needed for sorting or for `Map`, `Set` and `Dictionary` keys.** F# generic
comparison already orders `NaN` consistently — `compare nan nan` is `0`, and `NaN` sorts
first — so those work on plain `float`. What stays broken is a comparison hand-written
with `<` and `>`: it reports `NaN` equal to every value, which is intransitive and makes
`sortWith` return unsorted output without raising.

For the same reason there are no refined numbers, it offers no pairwise arithmetic:
unwrap with `value`, compute in plain `float`, and re-admit once.

`UnitInterval` holds a proportion in `[0, 1]`. It is the only type here closed under
multiplication, which is the reason to reach for it:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
UnitInterval.multiply a b         // total and closed
UnitInterval.complement a         // total
UnitInterval.lerp low high a      // total, always lands between the endpoints
UnitInterval.inverseLerp low high v // the inverse: where v sits, clamped
UnitInterval.saturatingAdd a b    // not closed under +, so this clamps
```

`complement` is an involution only up to floating-point rounding — exact for dyadic values,
approximate otherwise.

`FiniteFloat32` carries the same guarantee for single precision. It has no canonical wire
schema, because JSON has no single-precision number — widen with `toFiniteFloat` at a
boundary.

`Bounded<'T>` gets its schema from `RefinedSchemas.bounded bounds itemSchema`: the bounds
belong to the field rather than to each value, so they are supplied once.

## Text

`NonBlankString` preserves accepted text exactly, and its operations preserve inhabitation:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
NonBlankString.append first second   // total
NonBlankString.trim value            // total — trimming inhabited text leaves it inhabited
NonBlankString.split "," value       // NonEmptyList<NonBlankString>, never empty
```

## When not to make a type

Trimmed text, slugs, email addresses, and length bounds carry no invariant that any later
operation uses. Nothing about a string becomes total or loses a branch once you know its
ends are free of whitespace, so a wrapper would only be unwrapped at first use. That is the
test, not closure: trimmedness happens to survive concatenation, and it still earns no type.
Slug does not even get that far — joining two slugs can break the pattern.

Express them as constraints on a primitive instead — the metadata reaching interpreters is
identical:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
field _.DisplayName { constrain Constraint.trimmed }

field _.Slug {
    constrain Constraint.present
    constrain (Constraint.pattern slugPattern)
}
```

The field's schema is inferred from its type, so a constraint needs no `withSchema`, and
each constraint can sit on its own line.

If you do want a nominal type in your own domain, the machinery is still here — see
[Define Refined Types](/domain-types/domain-values.html).

## Schema resolution

Every type above has a canonical wire schema, so a bare field resolves it with no
`withSchema`. The 64-bit and floating-point types sit on the `Schema.int64` and
`Schema.float` primitives rather than being mapped onto `decimal`, which would change
their meaning. Note that JSON has no literal for `NaN` or the infinities: a schema that
must reject them should use `FiniteFloat`, whose `finite` constraint is inspectable
metadata like any other.

Continue with [Compose Parse and Refinement](/domain-types/composition.html) and
[Define Refined Types](/domain-types/domain-values.html).
