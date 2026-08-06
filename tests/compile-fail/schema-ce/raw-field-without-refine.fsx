#load "references.fsx"

open Reified.Data
open Reified.Schema
open Reified.Schema.Syntax

type Email =
    private
    | Email of string

type Signup = { Email: Email }

schema<Signup> {
    field "email" _.Email {
        withSchema Schema.text
    }

    construct (fun email -> { Email = email })
}
|> ignore
