#load "references.fsx"

open Reified.Data
open Reified.Schema
open Reified.Schema.Syntax

// A field can only be optional when its type can hold an absent input. `Name` is `string`, so there is
// nowhere to put an absent value and the constructor could never be applied — `mayOmit` must not compile.
type Contact = { Name: string }

schema<Contact> {
    field _.Name {
        withSchema Schema.text
        mayOmit
    }

    construct (fun name -> { Name = name })
}
|> ignore
