---
weight: 40
title: Define Refined Types
description: Couple an inspectable constraint to total construction and projection.
targetFramework: net8.0
---

# Define Refined Types

A refined type is a private wrapper, a checked constructor, and one canonical way to recover the underlying value.
This page is the reference for that machinery.

Before reaching for it, decide whether the concept deserves a type at all. Checked construction is how a value is
admitted, not a reason on its own: if nothing downstream becomes total or loses a branch, the rule belongs in a
[constraint](/constraints/constraint.html) on the primitive instead. Numeric ranges are the clearest
example, F# cannot carry "greater than zero" through arithmetic, so a refined number costs more at every use site
than it saves. [When not to make a type](/refined/catalog.html#when-not-to-make-a-type) draws the line, and
[Customer Id](/refined/tutorials/customer-id.html) works a full example through.

## Define the wrapper and Value projection

```fsharp
open Reified
open Reified.Refinements

type ContactEmail =
    private
    | ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value
```


## Guard construction with a refinement

Use `Refinement.define` when one constraint from the built-in catalogue describes admission. Those constraints are
[interpreted](/constraints/constraints.html): Reified can read the rule as data, so the refinement
carries metadata other tools can use, not only a check.

```fsharp isolated
open Reified
open Reified.Refinements

type ContactEmail =
    private
    | ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.define
            Constraint.email
            ContactEmail
            value

    let create raw =
        Refinement.create refinement raw
```


`ContactEmail.create` is now the only way in. Construction returns check failures directly:

```fsharp no-check reason="Continues the isolated block above; shown separately for readability, not as an independently-compiling unit (rawEmail is illustrative caller input)."
let email : Result<ContactEmail, Violation> =
    ContactEmail.create rawEmail
```


## Combine constraints

`Constraint.all` checks every listed constraint against the same original value and combines their failures; give
the combined constraint to the same `Refinement.define` used above:

```fsharp isolated
open Reified
open Reified.Refinements

type ContactEmail =
    private
    | ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.define
            (Constraint.all
                [ Constraint.present
                  Constraint.email
                  Constraint.maxLength 254 ])
            ContactEmail
            value

    let create raw = Refinement.create refinement raw
```


The same constraint values provide executable checks and `ConstraintDescription` metadata.

## Use a metadata-free check

Use `Constraint.customWith` for an invariant no built-in constraint describes, then hand that opaque constraint to
`Refinement.define` like any other. The check runs, but nothing downstream can read the rule, the trade-off is
[interpreted versus opaque](/constraints/constraints.html). `customWith`'s own string argument ("even" below) only
feeds that opaque `ConstraintDescription`; a caller sees the callback's own `Described` text on failure, not that
string:

```fsharp isolated
open Reified
open Reified.Refinements

type EvenInt = private EvenInt of int

module EvenInt =
    let value (EvenInt value) = value

    let private even value =
        if value % 2 = 0 then Ok ()
        else Error(Violation.Atomic(AtomicViolation.Described("must be an even number", None)))

    let refinement =
        Refinement.define (Constraint.customWith "even" even) EvenInt value
```


## Projection law

For every successful construction, the reverse projection returns the supplied underlying value:

```fsharp isolated
open Reified
open Reified.Refinements

type ContactEmail =
    private
    | ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.define
            (Constraint.all [ Constraint.present; Constraint.email; Constraint.maxLength 254 ])
            ContactEmail
            value

    let create raw = Refinement.create refinement raw

let result =
    ContactEmail.create "ada@example.com"
    |> Result.map (Refinement.underlying ContactEmail.refinement)

// Ok "ada@example.com"
```


Choose one concrete underlying representation. For collection refinements, prefer `'a list` or `'a array` rather than
an arbitrary `seq<'a>`.

## Give the type its operations

A wrapper that only checks on the way in leaves callers unwrapping it at first use. What makes the type worth having
is the family of operations that preserve its invariant, so the fact stays true without being rechecked:

```fsharp isolated
open Reified
open Reified.Refinements

type ContactEmail =
    private
    | ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.define
            (Constraint.all [ Constraint.present; Constraint.email; Constraint.maxLength 254 ])
            ContactEmail
            value

    let create raw = Refinement.create refinement raw

    // ... as in "Combine constraints" above

    /// Total: lower-casing inhabited, well-formed text leaves it inhabited and well-formed.
    let normalise (input: ContactEmail) = ContactEmail(value input |> fun text -> text.ToLowerInvariant())
```


If you cannot write an operation like that, the concept is probably a constraint rather than a type.

Continue with [Schema Integration](/schema/index.html).
