#load "references.fsx"

open Reified
open Reified.SchemaDSL

type Email =
    private
    | Email of string

type Signup = { Email: Email }

schema<Signup> {
    field _.Email {
        withSchema Schema.text
    }

    construct (fun email -> { Email = email })
}
|> ignore
