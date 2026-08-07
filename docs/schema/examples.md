---
weight: 85
title: Runnable Examples
description: Executable schema, refined, diagnostics, and policy examples mirrored back into the docs.
---

# Runnable Examples

This page shows the examples that are executed during the docs build, so the public docs stay tied to real code and observed output.

The examples below are built from the repository projects, run with the current source, and then written back into this page.

The code blocks keep the important API calls on the same lines as the values they bind, with trailing comments where that makes the signature easier to read.
The examples prefer the normal direct-bind style inside computation expressions, so the docs reflect the recommended day-to-day usage.

## Refined Catalog Example

This example shows a request boundary that parses strings, builds refined numeric/text/collection values, chooses a domain union case, and rejects invalid input before the domain record is created.

Run it:

```bash
REIFIED_EXAMPLE=refined-catalog dotnet run --project examples/Reified.Schema.Examples/Reified.Schema.Examples.fsproj --nologo
```

Source:

- [RefinedCatalogExample.fs](https://github.com/adz/Reified/blob/main/examples/Reified.Schema.Examples/RefinedCatalogExample.fs)

Source code:

```fsharp
module RefinedCatalogExample

open Reified.Parse

open System
open Reified.Result
open Reified.Result.Syntax
open Reified.Constraint
open Reified.Constraint.Syntax
open Reified.Refinements

// Slug is no longer a catalogue entry: it carries no invariant past the boundary, so it
// is defined here from the same constraints the built-in type used, exactly like Sku below.
type Slug = private Slug of string

type ProductId =
    private
    | ProductId of int

    member this.Value =
        let (ProductId value) = this
        value

module ProductId =
    let refinement = Refinement.define (Constraint.notEqualTo 0) ProductId _.Value
    let create value = Refinement.create refinement value
type ProductSlug = ProductSlug of Slug
type DisplayName = DisplayName of NonBlankString
type ProductTags = ProductTags of DistinctList<Slug>
type Quantity =
    private
    | Quantity of int

    member this.Value =
        let (Quantity value) = this
        value

module Quantity =
    let refinement = Refinement.define (Constraint.greaterThan 0) Quantity _.Value
    let create value = Refinement.create refinement value
type ContactEmail = private ContactEmail of string
type Sku = private Sku of string
type Rating = private Rating of int
type UnitPrice = private UnitPrice of decimal

module Slug =
    let value (Slug value) = value

    let create value : Result<Slug, Violation> =
        value
        |> Constraint.check (Constraint.all [ present; pattern "^[a-z0-9]+(-[a-z0-9]+)*$" ])
        |> Result.map (fun () -> Slug value)

module ContactEmail =
    let value (ContactEmail value) = value

    let create value : Result<ContactEmail, Violation> =
        value
        |> Constraint.check (Constraint.all [ present; email; maxLength 254 ])
        |> Result.map (fun () -> ContactEmail value)

module Sku =
    let value (Sku value) = value

    let create value : Result<Sku, Violation> =
        value
        |> Constraint.check (Constraint.all [ present; lengthBetween 3 12; pattern "^[A-Z0-9-]+$" ])
        |> Result.map (fun () -> Sku value)

module Rating =
    let value (Rating value) = value

    let create value : Result<Rating, Violation> =
        value |> Constraint.check (Constraint.between 1 5) |> Result.map (fun () -> Rating value)

module UnitPrice =
    let value (UnitPrice value) = value

    let create value : Result<UnitPrice, Violation> =
        value |> Constraint.check (greaterThan 0m) |> Result.map (fun () -> UnitPrice value)

type Discount =
    | Percent of Quantity
    | Code of Slug

type PublishWindow =
    { Range: Interval<DateTimeOffset> }

type ProductRequest =
    { Id: ProductId
      Slug: ProductSlug
      DisplayName: DisplayName
      Tags: ProductTags
      Quantity: Quantity
      ContactEmail: ContactEmail
      Sku: Sku
      Rating: Rating
      UnitPrice: UnitPrice
      Discount: Discount
      PublishWindow: PublishWindow }

let sequenceResults values =
    let folder next state =
        match next, state with
        | Ok value, Ok values -> Ok(value :: values)
        | Error error, _ -> Error error
        | _, Error error -> Error error

    values
    |> List.foldBack folder
    <| Ok []

let private parseError error = Error(sprintf "%A" error)
let private checkError failures = Error(Violation.render failures)

let parseDiscount (raw: string) : Result<Discount, string> =
    let parsePercent value =
        result {
            let! parsed = Parse.int value |> Result.mapError (sprintf "%A")
            let! percent = Quantity.create parsed |> Result.mapError Violation.render
            return Percent percent
        }
    match parsePercent raw with
    | Ok value -> Ok value
    | Error _ -> Slug.create raw |> Result.map Code |> Result.mapError Violation.render

let createProductRequest rawId rawSlug rawDisplayName rawTags rawQuantity rawContactEmail rawSku rawRating rawUnitPrice rawDiscount publishStart publishEnd : Result<ProductRequest, string> =
    result {
        let! parsedId = Parse.int rawId |> Result.mapError (sprintf "%A")
        let! id = ProductId.create parsedId |> Result.mapError Violation.render
        let! slug = Slug.create rawSlug |> Result.mapError Violation.render
        let! displayName = Refine.nonBlankString rawDisplayName |> Result.mapError Violation.render
        let! tags = rawTags |> List.map Slug.create |> sequenceResults |> Result.mapError Violation.render
        let! distinctTags = Refine.distinctList tags |> Result.mapError Violation.render
        let! parsedQuantity = Parse.int rawQuantity |> Result.mapError (sprintf "%A")
        let! quantity = Quantity.create parsedQuantity |> Result.mapError Violation.render
        let! contactEmail = ContactEmail.create rawContactEmail |> Result.mapError Violation.render
        let! sku = Sku.create rawSku |> Result.mapError Violation.render
        let! parsedRating = Parse.int rawRating |> Result.mapError (sprintf "%A")
        let! rating = Rating.create parsedRating |> Result.mapError Violation.render
        let! parsedUnitPrice = Parse.decimal rawUnitPrice |> Result.mapError (sprintf "%A")
        let! unitPrice = UnitPrice.create parsedUnitPrice |> Result.mapError Violation.render
        let! discount = parseDiscount rawDiscount
        let! range = Interval.create publishStart publishEnd |> Result.mapError Violation.render
        return { Id = id; Slug = ProductSlug slug; DisplayName = DisplayName displayName; Tags = ProductTags distinctTags; Quantity = quantity; ContactEmail = contactEmail; Sku = sku; Rating = rating; UnitPrice = unitPrice; Discount = discount; PublishWindow = { Range = range } }
    }


let run () =
    createProductRequest "1" "product" "Product" [ "featured" ] "2" "ada@example.com" "SKU-1" "5" "12.50" "10" DateTimeOffset.UtcNow DateTimeOffset.UtcNow
    |> printfn "Refined catalog: %A"

```

## Refined Value Schema Example

This example shows total domain conversions built with Schema.convert, composed into a record schema, and lowered to executable checks.

Run it:

```bash
REIFIED_EXAMPLE=refined-value-schema dotnet run --project examples/Reified.Schema.Examples/Reified.Schema.Examples.fsproj --nologo
```

Source:

- [RefinedValueSchemaExample.fs](https://github.com/adz/Reified/blob/main/examples/Reified.Schema.Examples/RefinedValueSchemaExample.fs)

Source code:

```fsharp
module RefinedValueSchemaExample

open Reified.Constraint
open Reified.Schema
open Reified.Schema.Syntax

/// <summary>An email address refined over Reified's text primitive, carrying the well-known email format.</summary>
type Email =
    private
    | Email of string

    static member Schema(_: Email) : Schema<Email> =
        Schema.text
        |> Schema.constrainAll [ Constraint.present; Constraint.email ]
        |> Schema.convert Email (fun (Email value) -> value)
        |> Schema.withFormat SchemaFormat.email

module Email =
    let create (value: string) = Email value
    let value (Email value) = value

    let schema : Schema<Email> = SchemaDefaults.Resolve()

/// <summary>A bounded-text domain value whose length constraints live on the raw text schema.</summary>
type ContactName =
    private
    | ContactName of string

    static member Schema(_: ContactName) : Schema<ContactName> =
        Schema.text
        |> Schema.constrainAll [ Constraint.minLength 2; Constraint.maxLength 40 ]
        |> Schema.convert ContactName (fun (ContactName value) -> value)

module ContactName =
    let create (value: string) = ContactName value
    let value (ContactName value) = value

    let schema : Schema<ContactName> = SchemaDefaults.Resolve()

/// <summary>A quantity that must always be positive (strictly greater than zero).</summary>
type Quantity =
    private
    | Quantity of int

    static member Schema(_: Quantity) : Schema<Quantity> =
        Schema.int
        |> Schema.constrain (Constraint.greaterThan 0)
        |> Schema.convert Quantity (fun (Quantity value) -> value)

module Quantity =
    let create (value: int) = Quantity value
    let value (Quantity value) = value

    let schema : Schema<Quantity> = SchemaDefaults.Resolve()

/// <summary>A running total that must never go negative, but zero is allowed.</summary>
type Balance =
    private
    | Balance of decimal

    static member Schema(_: Balance) : Schema<Balance> =
        Schema.decimal
        |> Schema.constrain (Constraint.atLeast 0m)
        |> Schema.convert Balance (fun (Balance value) -> value)

module Balance =
    let create (value: decimal) = Balance value
    let value (Balance value) = value

    let schema : Schema<Balance> = SchemaDefaults.Resolve()

type Contact =
    { Email: Email
      Name: ContactName
      Quantity: Quantity
      Balance: Balance }

let contactSchema =
    schema<Contact> {
        field _.Email
        field _.Name
        field _.Quantity
        field _.Balance
        construct (fun email name quantity balance ->
            { Email = email
              Name = name
              Quantity = quantity
              Balance = balance })
    }

let run () =
    let contact =
        { Email = Email.create "ada@example.com"
          Name = ContactName.create "Ada"
          Quantity = Quantity.create 3
          Balance = Balance.create 0m }

    let emailCheck = Email.schema |> SchemaCheck.text
    let nameCheck = ContactName.schema |> SchemaCheck.text
    let quantityCheck = Quantity.schema |> SchemaCheck.ordered<int, _>
    let balanceCheck = Balance.schema |> SchemaCheck.ordered<decimal, _>

    printfn "Email check: %A" (emailCheck contact.Email)
    printfn "Name check: %A" (nameCheck contact.Name)
    printfn "Quantity check: %A" (quantityCheck contact.Quantity)
    printfn "Balance check: %A" (balanceCheck contact.Balance)

    printfn "Invalid email check: %A" (emailCheck (Email.create ""))
    printfn "Invalid name check: %A" (nameCheck (ContactName.create "A"))
    printfn "Invalid quantity check: %A" (quantityCheck (Quantity.create 0))
    printfn "Invalid balance check: %A" (balanceCheck (Balance.create -1m))

```

