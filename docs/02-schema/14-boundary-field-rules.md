---
weight: 14
title: Boundary-specific field rules
description: Attach constraints to a field block when a rule belongs to one boundary rather than to the domain type.
targetFramework: net8.0
---

# Boundary-specific field rules

A refinement holds for every value of its type. A constraint holds at one boundary. Use a constraint on a field block
when the rule belongs to this form rather than to the domain type. [SchemaDSL](/schema/dsl.html) lists the full field
block vocabulary; [Refined Schemas](/schema/refined-values.html) shows how to lift a rule that should hold everywhere
into a refinement.

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Profile =
    { DisplayName: string
      Age: int }

let profileSchema =
    schema<Profile> {
        field _.DisplayName {
            constraints [ present; maxLength 40 ]
        }

        field _.Age {
            constrain (Constraint.between 13 120)
        }

        construct (fun displayName age -> { DisplayName = displayName; Age = age })
    }
```


A field block is the expanded form of `field _.DisplayName`. `constrain` adds one; `constraints` adds a list.

Constraints reach the interpreters just as refinements do:

```fsharp
JsonSchema.generate profileSchema
// "displayName": {"type":"string","maxLength":40}
// "age":         {"type":"integer","minimum":13,"maximum":120}
```


```text
displayName: This value must be present.
age: Must be between 13 and 120; got 9.
```


A constraint preserves the value's type, `maxLength 40` on a `string` field leaves a `string` field. That is the
difference from `refine`, which changes the type and is what makes the guarantee durable.

Which to reach for:

- The rule is true of every value of the type, put it in the refinement, and every construction path enforces it.
- The rule is true only at this boundary, put it in the field block, where a reader can see it applies here.

Schema will not take metadata without an executable check behind it, so what an inspector reports is always what
parsing enforces. Constraint names come from
[Constraints](/constraints/constraints.html); see
[Refined Schemas](/schema/refined-values.html) for application-defined constraints.
