---
title: Schema or Generated DTO
linkTitle: Schema or Generated DTO
weight: 47
type: docs
description: Two ways to get from untrusted input to a domain type, and how to tell which one a boundary needs.
targetFramework: net8.0
---

# Schema or generated DTO

Choose one boundary strategy per model. An attribute-validated generated record decoded with the trusted JSON path
looks validated but skips its constraints. A hand-written schema needs no migration chain unless old wire shapes must
remain readable.

## Strategy 1: the schema is the contract

Write `Schema<'model>` by hand against the type your application actually uses. There is no separate wire record.
Field-level rules live on the schema (`constrain`, refined field types); model-level invariants live in refined
values or a private constructor. `Schema.parse` is the one boundary operation: it is where untrusted input becomes
a value your domain trusts.

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup =
    { Email: string
      Age: int }

let signupSchema =
    schema<Signup> {
        field _.Email { constraints [ present; email ] }
        field _.Age { constrain (atLeast 13) }
        construct (fun email age -> { Email = email; Age = age })
    }
```

Use this when you own both ends of the boundary, an HTTP request body, a config file, a form post, and the model
does not need to keep reading a shape it no longer produces. This is the default. Reach for strategy 2 only when
one of its two triggers applies.

→ [Getting Started](/getting-started/index.html) walks this path end to end.

## Strategy 2: generate a permissive wire DTO, validate on the way into the domain

Use this when either is true:

- **You do not own the wire shape**, an external API, a message from another service, a format you cannot change.
- **The wire shape has history**, old versions must still decode, and you need a [`Contract<'model>`](/schema/versioned-contracts.html)
  to migrate them.

`[<DeriveSchema>]` generates a schema from an ordinary record. For this strategy, generate it with no constraint
attributes at all. The DTO's only job is to name the fields and their wire types correctly; it makes no claim
about validity.

```fsharp no-check reason="A complete file with its own namespace, not a fragment."
namespace MyApp.Wire

open Reified.DerivedSchema

[<DeriveSchema>]
type SignupWire = { Email: string; Age: int }
```

Decode it with the trusted JSON path, `Json.compile` plus `Json.deserialize`/`Json.tryDeserialize`, not
`Schema.parse`. The two decoders
differ in exactly this: `Schema.parse` runs every constraint on the schema and accumulates diagnostics for a caller
who has not vetted the payload; the trusted path skips constraint checking entirely and enforces only the wire
shape, required fields and field types, because there is nothing on this DTO to check. Since the DTO carries no
attributes, the two would behave identically here; the reason to reach for the trusted path is that it says so in
the code, and it stays true if a teammate later adds an attribute to the DTO by mistake.

The generated module exposes `SignupWire.schema`; the hand-written equivalent below is exactly what the generator
emits for the record above. It is a complete, runnable boundary:

```fsharp
open Reified
open Reified.SchemaDSL

// Signup is the type from Strategy 1 above. The permissive wire schema below adds no
// constraints, so it claims nothing about validity.
let signupWireSchema : Schema<Signup> =
    schema<Signup> {
        field _.Email
        field _.Age
        construct (fun email age -> { Email = email; Age = age })
    }

let wireCodec = Json.compile signupWireSchema

let toDomain (signup: Signup) : Result<Signup, string> =
    match Constraint.check (Constraint.all [ Constraint.present; Constraint.email ]) signup.Email with
    | Error violation -> Error $"invalid email: {Violation.render violation}"
    | Ok () ->
        if signup.Age >= 13 then Ok signup
        else Error "age must be at least 13"

let parseWire (json: string) : Result<Signup, string> =
    Json.tryDeserialize wireCodec json
    |> Result.bind toDomain

parseWire """{"email":"ada@example.org","age":36}"""
// Ok { Email = "ada@example.org"; Age = 36 }

parseWire """{"email":"ada","age":11}"""
// Error "invalid email: expected an email address, but was ada"

parseWire """{"age":36}"""
// Error "JSON decode failed at $.email: missing required field"
```

`parseWire` shows both failure kinds: `Json.tryDeserialize` returns the first structural wire-shape failure as a
string, and `toDomain` returns the first validation failure it finds. Neither accumulates path-aware diagnostics; use
`Schema.parse` over `Data` when you need every field error at once.

The real validation runs once, in `toDomain`, through refined values or a hand-written domain schema, exactly as it
would in strategy 1. The DTO is structural, not a second place the same rule could drift.

→ [Derived Schemas](/schema/derivation/index.html) covers every attribute the generator reads, including the ones
this strategy deliberately does not use. → [Separate Wire and Domain Models](/schema/patterns/wire-and-domain-models.html)
extends this into a full aggregate with controlled updates. → [Versioned Contracts](/schema/versioned-contracts.html)
covers the migration chain when old wire shapes must keep decoding.

## What not to do

Do not put `[<Email; Present; AtLeast 13>]` on a generated wire record and then decode it with `Json.deserialize`.
The trusted path does not read attributes, so the record looks validated in the editor and is not. Pick one:
decode it with `Schema.parse` instead, which makes it strategy 1's schema rather than a wire DTO, or drop the
attributes and validate in `toDomain`, which is strategy 2. Attributes and the trusted decoder do not combine.
