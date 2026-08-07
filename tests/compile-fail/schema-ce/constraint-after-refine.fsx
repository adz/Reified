#load "references.fsx"

open Reified.Data
open Reified.Refinements
open Reified.Constraint.Syntax
open Reified.Schema
open Reified.Schema.Syntax

type Email =
    private
    | Email of string

module Email =
    let value (Email value) = value
    let refinement = Refinement.define (Reified.Constraint.Constraint.pattern ".+@.+") Email value

type Email with
    static member Refinement(_: string, _: Email) = Email.refinement

type Signup = { Email: Email }

schema<Signup> {
    field _.Email {
        withSchema Schema.text
        refine
        constrain (minLength 3)
    }

    construct (fun email -> { Email = email })
}
|> ignore
