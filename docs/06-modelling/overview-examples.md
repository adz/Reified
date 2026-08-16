---
weight: 3
title: Schema Overview Examples
description: Short examples of fields, checked construction, refinement, recursion, and interpretation.
targetFramework: net8.0
---

# Schema Overview Examples

```fsharp
open Reified
open Reified.SchemaDSL
```

## Canonical fields

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
type Address =
    { City: string }

    static member Schema(_: Address) : Schema<Address> =
        schema<Address> {
            field _.City {
                constrain (minLength 1)
            }
            construct (fun city -> { City = city })
        }
```

Canonical schemas resolve through nested records, options, lists, and string-keyed maps:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let customerSchema =
    schema<Customer> {
        field _.Id
        field _.Address
        field _.Labels
        field _.Note
        construct Customer.create
    }
```

## Checked construction

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let rangeSchema =
    schema<DateRange> {
        field _.Start
        field _.End
        constructResult DateRange.create
    }
```

The constructor runs only after both fields succeed.

## Refined fields

Built-in refined values have canonical schemas, so the field remains plain:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let accountSchema =
    schema<Account> {
        field _.Name
        construct (fun name -> { Name = name })
    }
```

A local raw schema can instead transition to the getter type:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
field _.Email {
    withSchema Schema.text
    constrain present
    refine
}
```

## Recursion

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let rec schema : Lazy<Schema<Category>> =
    lazy (
        SchemaDSL.schema<Category> {
            field _.Name
            field _.Children {
                withSchema (Schema.listWith (Schema.defer schema))
            }
            construct Category.create
        })
```

The qualification is required only because the recursive binding is also named `schema`.

## Interpreters

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
let parsed = Schema.parse customerSchema dataInput
let checked = Schema.check customerSchema customer
let description = Inspect.model customerSchema
let jsonSchema = JsonSchema.generate customerSchema
let codec = Json.compile customerSchema
```
