---
weight: 20
title: vs FluentValidation
description: Validators check existing objects; Reified schemas never construct the invalid object.
targetFramework: net8.0
---

# Reified vs FluentValidation

FluentValidation validates objects that already exist. Reified's `Schema.parse` either constructs a value whose
declared boundary rules passed or reports path-aware issues; it never hands back a partially checked object.

## The Model Difference

A FluentValidation validator receives a constructed object and reports rule failures:

```csharp
public class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(80);
        RuleFor(c => c.Age).InclusiveBetween(13, 120);
    }
}
```


The `Customer` had to be constructed first, usually by a model binder filling public setters with whatever arrived.
Between construction and validation (and anywhere a code path forgets to call the validator) an invalid `Customer`
exists and can leak.

A Reified schema owns construction. Parsing either produces a trusted model or path-aware issues; there is no
intermediate invalid object:

```fsharp
open Reified.SchemaDSL

type Customer = { Name: string; Age: int }

let customerSchema =
    schema<Customer> {
        field _.Name {
            constrain (Constraint.maxLength 80)
        }
        field _.Age {
            constrain (Constraint.between 13 120)
        }
        construct (fun name age -> { Name = name; Age = age })
    }

let raw =
    Data.ofNameValues
        [ "name", "Ada"
          "age", "36" ]

match Schema.parse customerSchema raw with
| Ok customer -> printfn "accepted %s" customer.Name   // a Customer returned by Schema.parse passed the declared rules
| Error errors -> printfn "%A" errors
```


The `Name` rule shown is only `Constraint.maxLength 80`, which admits an empty string; add `Constraint.present` when
a non-blank name is required. And because `Customer` is an ordinary public record, code outside the boundary can still
construct `{ Name = ""; Age = 0 }` directly — the "cannot exist invalid" guarantee applies to values `Schema.parse`
returns, not to every value of the type.

With refined field types (such as an `Email` with a private constructor), downstream code need not check whether the fields were validated.

## Rules Are Data, Not Just Code

FluentValidation rules are lambdas inside a class: they can run, but nothing else can read them. A Reified schema is
inspectable data: its structure — the declared fields, getters, and constructor — is what `Inspect.model` reads for UI
metadata and `Json.compile` compiles into a JSON codec, while the constraints each field carries are what
`JsonSchema.generate` exports and `Schema.parse` enforces. So the same declaration produces the JSON Schema/OpenAPI
contract, UI metadata, a compiled JSON codec, and redisplayable form errors. With FluentValidation, each of those is a
separate artifact to keep in sync by hand.

The compiled codec enforces JSON structure and required fields, not the field constraints: use it for trusted producers,
and `Schema.parse` when the value rules must hold for untrusted input.

## Where FluentValidation Fits Better

- Large existing C# codebases already organized around DTOs, model binding, and validator classes.
- Validation of objects you genuinely do not construct (third-party types, EF entities mid-flight).
- Teams that want C#-first fluent syntax rather than F# declarations.

`Schema.check schema model` re-checks an existing value against the same field schemas, then re-invokes the record's
constructor with the checked field values. It returns the original value on success, or `SchemaErrors` with the same
paths as parsing.

## Side By Side

| Concern | FluentValidation | Reified |
| --- | --- | --- |
| Invalid object exists? | Yes, until validated | Not for values `Schema.parse` returns |
| Rules readable as data | No (lambdas in classes) | Yes (`Constraint` metadata) |
| OpenAPI/JSON Schema | Separate annotations | Generated from the same declaration |
| Error paths | Property names via expressions | Structural paths (`contacts[1].value`) |
| Reflection | Expression trees + reflection | None; AOT/trimming/Fable-safe |
