// The umbrella installed alone. One PackageReference on Reified must put every runtime package on the
// compile line, so this fixture uses a value from each of them: the file does not compile if the
// umbrella's dependency list is missing one.

open Reified.Constraint
open Reified.Data
open Reified.Parse
open Reified.Refinements
open Reified.Result
open Reified.Schema
open Reified.Schema.Http
open Reified.Schema.Json

type Signup = { Email: string; Age: int }

module private Signup =
    let create email age = { Email = email; Age = age }

let private signupSchema =
    schema<Signup> {
        field "email" _.Email {
            withSchema Schema.text
            constrain (Constraint.minLength 3)
        }

        field "age" _.Age {
            withSchema Schema.int
            constrain (Constraint.atLeast 13)
        }

        construct Signup.create
    }

[<EntryPoint>]
let main _ =
    let codec = Json.compile signupSchema
    let roundTripped = Json.deserialize codec (Json.serialize codec { Email = "ada@example.com"; Age = 42 })

    let refined = NonEmptyList.create [ "ada" ]
    let parsed = Parse.int "42"
    let guarded = Result.requireTrue "unreachable" true
    let rendered = Data.render (Data.Text "ada")

    match roundTripped, refined, parsed, guarded with
    | signup, Ok _, Ok 42, Ok () when
        signup.Email = "ada@example.com"
        && signup.Age = 42
        && rendered.Contains "ada"
        && ProblemDetails.ContentType = "application/problem+json"
        ->
        printfn "Consumer.Umbrella OK"
        0
    | other ->
        eprintfn "Consumer.Umbrella FAILED: %A" other
        1
