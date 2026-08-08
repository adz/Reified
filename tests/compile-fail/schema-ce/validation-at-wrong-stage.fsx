#load "references.fsx"

open Reified
open Reified.Refinements
open Reified.SchemaSyntax

type Email =
    private
    | Email of string

module Email =
    let value (Email value) = value
    let refinement = Refinement.define (Reified.Constraint.Constraint.pattern ".+@.+") Email value

type Email with
    static member Refinement(_: string, _: Email) = Email.refinement

type Signup = { Email: Email }

let validateText (value: string) =
    if value.Length > 3 then
        Ok()
    else
        Error(SchemaError.Custom("short", Some "Too short."))

schema<Signup> {
    field _.Email {
        withSchema Schema.text
        refine
        validate validateText
    }

    construct (fun email -> { Email = email })
}
|> ignore
