---
title: Introductory Reference App
description: Checks, result {}, parsing, and refinement in one small program.
---

# Introductory Reference App

The introductory app uses `Reified.Result`, `Reified.Constraint`, `Reified.Refinements`, and `Reified.Parse` without Schema.
Every snippet below is taken from `examples/Reified.ReferenceApp.Intro/Program.fs`, which compiles and runs:

```bash
dotnet run --project examples/Reified.ReferenceApp.Intro/Reified.ReferenceApp.Intro.fsproj --nologo
```

The examples open the syntax modules, so the constraint and result vocabulary is unqualified:

```fsharp
open Reified
open Reified.Result
open Reified.Result.Syntax
open Reified.ConstraintSyntax
open Reified.Refinements
```

## Reusable checks

`Constraint.guard` runs a constraint and returns the input on success, so the value survives the check. `orError`
replaces the `Violation` with the application's own error case, which keeps the signature in the application's
vocabulary:

```fsharp
type BadgeError =
    | NameTooShort
    | NameTooLong

/// A badge name must print on one line: 3 to 40 characters.
let validateBadgeName (name: string) : Result<string, BadgeError> =
    name
    |> guard (minLength 3)
    |> orError NameTooShort
    |> Result.bind (guard (maxLength 40) >> orError NameTooLong)
```

## Dependent work

`result { }` sequences steps that depend on each other, so the first failure stops the pipeline:

```fsharp
let parseTicketRequest (rawTier: string) (rawQuantity: string) : Result<Tier * int, TicketError> =
    result {
        let! tier = parseTier rawTier
        let! quantity = Parse.int rawQuantity |> orError (QuantityNotANumber rawQuantity)
        do! (quantity >= 1 && quantity <= 6) |> Result.requireTrue (QuantityOutOfRange quantity)
        return tier, quantity
    }
```

## Construct domain values

Parsing and refinement stay separate steps: `Parse.int` turns text into an `int`, and the refinement decides whether
that `int` is an `AttendeeId`.

```fsharp
type AttendeeId =
    private
    | AttendeeId of int

    member this.Value =
        let (AttendeeId value) = this
        value

module AttendeeId =
    let refinement = Refinement.define (Constraint.greaterThan 0) AttendeeId _.Value
    let create value = Refinement.create refinement value

let createContact (rawId: string) (rawEmail: string) : Result<Contact, ContactError> =
    result {
        let! parsedId = Parse.int rawId |> Result.mapError (fun _ -> InvalidId)
        let! id = AttendeeId.create parsedId |> Result.mapError (fun _ -> InvalidId)
        let! email = Refine.nonBlankString rawEmail |> Result.mapError (fun _ -> InvalidEmail)
        return { Id = id; Email = ContactEmail email }
    }
```

`AttendeeId.create` already returns an `AttendeeId`, and `Refine.nonBlankString` already returns a `NonBlankString`, so
the record takes them as they are. Nothing is checked or wrapped twice.

Positive integers are a constraint rather than a shipped type: F# cannot carry "greater than zero" through arithmetic,
so a built-in `PositiveInt` would cost more at every use site than it saves. Define one over the constraint, as above,
when your domain wants the name.

The full reference app adds Schema for structured input, path-aware diagnostics, codecs, and contracts. Effectful
application work sits outside Reified, in the [Axial repository](https://github.com/adz/Axial).
