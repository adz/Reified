// The code behind docs/getting-started.md, compiled and run so the page cannot drift from the API.
//
// One complete boundary transaction, in the order the page presents it:
//   1. an ordinary F# record
//   2. one schema declaration over it
//   3. realistic untrusted input parsed into the typed value
//   4. bad input turned into path-aware accumulated failures
//   5. a second artefact — a JSON codec — derived from the same declaration

open Reified.Constraint.Syntax
open Reified.Data
open Reified.Schema
open Reified.Schema.Syntax
open Reified.Schema.Json

// ---------------------------------------------------------------------------
// 1. The model: a plain record. Nothing here is Reified-specific.
// ---------------------------------------------------------------------------

type Signup =
    { Email: string
      Age: int
      Newsletter: bool }

// ---------------------------------------------------------------------------
// 2. The declaration: how untrusted input becomes a Signup.
// ---------------------------------------------------------------------------

let signupSchema =
    schema<Signup> {
        field _.Email { constraints [ present; email ] }
        field _.Age { constrain (atLeast 13) }
        field _.Newsletter
        construct (fun email age newsletter ->
            { Email = email; Age = age; Newsletter = newsletter })
    }

// ---------------------------------------------------------------------------
// 3 and 4. Parsing: the typed value, or every failure with its path.
// ---------------------------------------------------------------------------

let goodInput =
    Data.ofNameValues
        [ "email", "ada@example.org"
          "age", "36"
          "newsletter", "true" ]

let badInput =
    Data.ofNameValues
        [ "email", "ada"
          "age", "11" ]

let private renderFailures (errors: SchemaErrors) =
    SchemaErrors.toList errors
    |> List.map (fun issue -> sprintf "%s: %s" (Path.format issue.Path) (SchemaError.render issue.Error))

[<EntryPoint>]
let main _ =
    match Schema.parse signupSchema goodInput with
    | Ok signup -> printfn "parsed: %A" signup
    | Error errors -> printfn "unexpected failure: %A" (renderFailures errors)

    match Schema.parse signupSchema badInput with
    | Ok signup -> printfn "unexpected success: %A" signup
    | Error errors -> renderFailures errors |> List.iter (printfn "  %s")

    // 5. The same declaration, read by a different interpreter.
    let codec = Json.compile signupSchema

    printfn "encoded: %s" (Json.serialize codec { Email = "ada@example.org"; Age = 36; Newsletter = true })
    printfn "json schema: %s" (JsonSchema.generate signupSchema)

    0
