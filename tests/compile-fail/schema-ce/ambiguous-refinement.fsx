#load "references.fsx"

open Reified.Refinements

type Email =
    private
    | Email of string

type Email with
    static member Refinement(_: string, _: Email) =
        Refinement.define (Reified.Constraint.pattern ".+@.+") Email (fun (Email value) -> value)

    static member Refinement(_: string, _: Email) =
        Refinement.define (Reified.Constraint.pattern ".+@.+") Email (fun (Email value) -> value)
