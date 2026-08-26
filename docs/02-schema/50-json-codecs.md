---
weight: 55
title: JSON Codecs
description: Compile a schema into a runtime-reflection-free JSON codec for trusted hot-path serialization.
targetFramework: net8.0
---

# JSON Codecs

This page shows how `Reified.Schema.Json` turns the schema you already declared into a compiled JSON codec, so trusted
serialization and boundary parsing come from one declaration.

Reified has two paths for JSON, and they exist because they optimize for different things:

- **Boundary parsing** — `Data` + `Schema.parse`: for untrusted input. It runs constraint metadata, accumulates
  path-aware diagnostics, and keeps the structured data for redisplay.
- **Trusted path** — `Json.compile` + `Json.serialize`/`Json.deserialize`: for payloads whose producer you trust, such
  as internal services, storage, caches, and queues. It enforces the wire shape and required fields, skips constraint
  checking, and runs about 6x faster with a fraction of the allocations (see the
  [benchmarks](/notes/benchmarks.html#schema-json-codec)).

## Compile Once, Reuse Everywhere

```fsharp
open Reified
open Reified.Schema.Json
open Reified.SchemaDSL

type Address =
    { Street: string; City: string }

    static member Schema(_: Address) : Schema<Address> =
        schema<Address> {
            field _.Street
            field _.City
            construct (fun street city -> { Street = street; City = city })
        }

type Customer =
    { Name: string
      Age: int
      Address: Address }

let customerSchema =
    schema<Customer> {
        field _.Name
        field _.Age
        field _.Address
        construct (fun name age address -> { Name = name; Age = age; Address = address })
    }

let codec = Json.compile customerSchema   // compile once, typically at startup

let json = Json.serialize codec { Name = "Ada"; Age = 36; Address = { Street = "12 Analytical Way"; City = "London" } }
// {"name":"Ada","age":36,"address":{"street":"12 Analytical Way","city":"London"}}

let customer = Json.deserialize codec json
```


`Json.compile` walks the typed record plan retained when the object shape closes and emits a direct plan: ordered field
descriptors, cached UTF-8 wire-name bytes, typed field decoders, and the original curried constructor applied without
boxing. Everything is compiler-directed: there is no runtime reflection at codec-compile time or per value, so the codec is AOT- and trimming-safe by construction.

## Every Schema Shape Is Supported

Refined values encode as their raw representation and are reconstructed on decode; nested models, collections, and
tagged unions follow the same wire shapes the input parser reads:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
// A union field {"type":"card","value":{...}} round-trips through the same discriminator convention.
let orderCodec = Json.compile orderSchema
```


## String-Like Map Keys

JSON object property names are strings, but the model key does not have to be. Use `Schema.mapWithKey` when a key type
has a total conversion to and from a property name:

```fsharp
type LocaleTag = LocaleTag of string

let localizedText =
    Schema.mapWithKey LocaleTag (fun (LocaleTag value) -> value) Schema.text
```

Boundary parsing and compiled JSON codecs use the same conversion, including under Fable. Build derivation infers this
schema for a map keyed by a transparent single-case string union.

## Decode Failures Carry Paths

Decoding trusted input can still meet malformed payloads. Failures raise `JsonCodecException` with a schema-relative
path, or use `tryDeserialize` for a `Result`:

```fsharp
match Json.tryDeserialize codec """{"name":"Ada","age":"not-a-number"}""" with
| Ok customer -> customer
| Error message -> failwith message   // JSON decode failed at $.age: expected digit
```


The codec reports the first structural failure and stops. When you need every problem reported with redisplayable
input — a form, a public API — that is boundary parsing's job:

```fsharp no-check reason="Not yet re-verified against the FsLiveDocs pipeline after the docs migration from the old docgen tool; port the correct fsharp/run/isolated mode by hand."
// Boundary parsing: complete diagnostics for untrusted input.
let parsed = Schema.parse customerSchema (Data.ofJsonDocument document)
```


## Bytes In, Bytes Out

`Json.serializeBytes` and `Json.deserializeBytes` avoid the string conversion when the payload already lives as UTF-8
bytes, which is the faster path for network and storage boundaries:

```fsharp
let bytes = Json.serializeBytes codec customer
let roundTripped = Json.deserializeBytes codec bytes
```


## What The Codec Does Not Do

- It does not run constraint metadata such as `maxLength` or `between` — those belong to boundary parsing and
  validation. A value that only ever passes through trusted systems does not pay for checks it already passed.
- Checked constructors from `constructResult` still run, so intrinsic cross-field invariants hold on the trusted path;
  their errors surface as `JsonCodecException`.

## From C#

Consume-don't-author: F# declares the schema, C# compiles the codec, parses, and reads diagnostics. Every `Json.*`
function takes plain positional arguments, so it calls as an ordinary static method with no `FSharpFunc` conversion:

```csharp
using Reified.Schema;
using Reified.Schema.Json;

JsonCodec<Customer> codec = Json.compile(customerSchema);

string json = Json.serialize(codec, customer);
Customer roundTripped = Json.deserialize(codec, json);

// Failures raise JsonCodecException instead of a Result, or use tryDeserialize:
var attempt = Json.tryDeserialize(codec, json); // FSharpResult<Customer, string>
```


`serializeToStream` and `deserializeStreamAsync` (both `async`/`Task`-based) are also plain static calls, so they work
directly against `HttpContext.Response.Body` / `Request.Body` in an ASP.NET Core handler.

## Next

- Serve the same declaration as a contract with [`JsonSchema.generate`](/api.html).
- See the two paths together in the runnable
  [minimal API sample](/schema/examples.html#minimal-api-boundary-example).
