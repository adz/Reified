---
weight: 60
title: HTTP Servers
type: docs
description: Host-neutral schema-driven endpoints — boundary input, problem-details errors, and generated OpenAPI.
---

# HTTP Servers

An HTTP endpoint is a trust boundary: the body, form, or query string arrives untrusted, and the handler wants a
typed model. `Reified.Schema.Http` turns one schema declaration into everything that boundary needs — parsing, an
error contract, and the published API document — and stays out of routing entirely.

The package split matters. `Reified.Schema.Http` is host-neutral: it depends only on `Reified.Schema` and defines
the boundary contract as values — how name/value input becomes `Data`, how parse diagnostics render as an error
response, and how endpoint declarations assemble into an OpenAPI document. It never opens a socket, reads a
request, or takes a dependency on a web framework.

Because the contract lives in the package rather than in an adapter, a service on Kestrel and a service embedding
GenHTTP return the same error bodies and publish the same OpenAPI fragments from the same schema declarations.

Run `dotnet add package Reified.Schema.Http`, or `dotnet add package Reified` for every runtime package at once.

Host adapters that lower these contracts into a native ASP.NET Core or GenHTTP handler, together with the twin
reference APIs that prove the boundary is host-neutral, live in the
[Axial repository](https://github.com/adz/Axial). Routing, middleware, and app wiring stay in the host's own idiom
either way.

## The error contract

A failed parse becomes an RFC 9457 problem-details body served as `application/problem+json`. Each diagnostic keeps
its path as an RFC 6901 JSON pointer, so clients can attach errors to fields mechanically:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc9457",
  "title": "The request input could not be parsed.",
  "status": 400,
  "errors": [
    { "pointer": "/address/city", "message": "This value must be present." },
    { "pointer": "/tags", "message": "Length must be at most 5; got 6." }
  ]
}
```

`ProblemDetails.ofParsed` builds that value from any failed `RetainedParseResult`; `ProblemDetails.ofErrors` accepts
`SchemaErrors` directly. `ProblemDetails.malformedJson` is the stable 400 value used
when a JSON body is not syntactically valid. Schema diagnostics and malformed JSON therefore share one media type and
response shape.

## Declaring endpoints for OpenAPI

`EndpointSpec` describes the boundary contract of one endpoint — method, path, request schema, responses — and
`OpenApi.document` assembles the specs into an OpenAPI 3.1 document. Request and response schemas are embedded from
`JsonSchema.generate` output, so the published contract cannot drift from what the parser accepts:

```fsharp
open Reified.Schema.Http

let openApiDocument =
    OpenApi.document
        (OpenApi.info "Signup API" "1.0.0")
        [ Endpoint.post "/signups"
          |> Endpoint.summary "Create a signup"
          |> Endpoint.accepts Signup.schema
          |> Endpoint.returnsJson 201 "The trusted signup that was parsed." Signup.schema
          |> Endpoint.returnsProblemDetails ]
```

`Endpoint.returnsProblemDetails` adds the standard 400 response with the problem-details JSON Schema, so the error
contract is part of the published document too.

## Complete API reference

- [Host-neutral schema HTTP boundary]({{< relref "/schema/reference/schema/http/" >}})

The host adapters document their own `Request`, `Response`, and lowering surfaces in the
[Axial documentation](https://github.com/adz/Axial).

## Form and query input

The core package also owns the host-neutral input rules, so every adapter produces identical `Data` for
identical wire data:

- `BoundaryInput.ofQuery` builds flat input from query pairs; repeated names become collections.
- `BoundaryInput.ofForm` nests dotted names (`address.street`), turns repeated names into collections, and turns
  sibling numeric segments (`tags.0`, `tags.1`) into ordered collections.

Form input is hostile, so `ofForm` drops contradictory pairs instead of raising. One consequence to know: a name
posted exactly once stays a scalar, because only the schema knows which fields are collections. A list field
submitted with a single selection should be posted as a repeated or indexed name.
