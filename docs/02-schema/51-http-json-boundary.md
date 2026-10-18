---
weight: 51
title: HTTP JSON boundary recipe
linkTitle: HTTP JSON boundary recipe
type: docs
description: From request bytes to a model or localized, path-aware failures, with an application-owned response.
targetFramework: net8.0
---

# HTTP JSON boundary recipe

This page shows one complete, untrusted JSON boundary: request text in, a model or localized failures out. It uses
only the published runtime packages (`Reified.Schema` for parsing and codecs, `Reified.Constraint` for rendering).
The HTTP host itself — reading the request, writing the response, choosing a status code — is application-owned.

## The pipeline

1. Turn the request text into `Data` with `Json.tryParseData`. This applies no schema; it only checks that the text
   is one JSON value.
2. Parse that `Data` with `Schema.parseRetainingInput`, which runs every field rule and keeps the original input for
   redisplay.
3. On failure, map each `SchemaIssue` to a localized, path-attached message for the client.

```fsharp
open Reified
open Reified.SchemaDSL
open Reified.ConstraintDSL

type Signup = { Email: string; Age: int; Newsletter: bool }

let signupSchema =
    schema<Signup> {
        field _.Email { constraints [ present; email ] }
        field _.Age { constrain (atLeast 13) }
        field _.Newsletter
        construct (fun email age newsletter ->
            { Email = email; Age = age; Newsletter = newsletter })
    }

type SignupFailure =
    | MalformedJson of message: string
    | Invalid of path: string * message: string

let translations =
    Map.ofList
        [ "constraint.presence.present", "doit être renseigné"
          "constraint.format.email", "doit être une adresse email"
          "constraint.relation.atLeast", "doit être au moins {expected}" ]

let renderer = Renderer.ofLookup translations.TryFind

let renderIssue (issue: SchemaIssue) =
    let message =
        match issue.Error with
        | SchemaError.Violation violation -> Violation.message renderer violation
        | other -> SchemaError.render other

    SchemaPath.format issue.Path, message

let handle (requestText: string) : Result<Signup, SignupFailure list> =
    match Json.tryParseData requestText with
    | Error message -> Error [ MalformedJson message ]
    | Ok data ->
        let parsed = Schema.parseRetainingInput signupSchema data

        match parsed.Result with
        | Ok signup -> Ok signup
        | Error errors ->
            errors
            |> SchemaErrors.toList
            |> List.map (fun issue -> Invalid(renderIssue issue))
            |> Error
```

`handle` is the whole boundary. The application then owns the response: map `Ok signup` to a `201` with the model
encoded through a compiled codec, and `Error failures` to a `400` whose body is whatever error contract the client
expects. Reified never opens a socket or writes a response; it produces the value and the failures.

For a stricter contract, `Reified.Schema.Http` assembles RFC 9457 problem-details values from these same parse
diagnostics; the host adapters that serve them live in the Axial repository.

## Notes

- `Json.tryParseData` returns `Result<Data, string>`; `Json.parseData` throws `JsonCodecException` instead. Use the
  `try` form at a boundary so a malformed body is a value, not an exception.
- `Schema.parseRetainingInput` keeps `parsed.Input`, so a form can redisplay exactly what the user sent next to each
  field error.
- The trusted codec (`Json.compile`) is the way out, not the way in for this pipeline: `Schema.parse` runs the rules
  the codec deliberately skips. See [Choosing an API](/guides/choosing-an-api.html).
