#load "references.fsx"

open Reified
open Reified.SchemaDSL

type Person = { Name: string; Age: int }

schema<Person> {
    field _.Name
    field _.Age
    construct (fun age name -> { Name = name; Age = age })
}
|> ignore
