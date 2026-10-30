namespace Reified

open System.Collections.Generic

/// <summary>Internal right-biased deep merge of structured data layers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal DataMerging =
    let rec merge (left: Data) (right: Data) : Data =
        match left, right with
        | Data.Object leftFields, Data.Object rightFields -> mergeFields (leftFields @ rightFields)
        | _, Data.Object rightFields -> mergeFields rightFields
        | _, value -> value

    /// Folds fields into an accumulator with one entry per name, first-seen order, merging a repeated name into the
    /// entry it repeats. Merging two objects folds the left fields then the right ones.
    and private mergeFields (fields: (string * Data) list) : Data =
        let names = Dictionary<string, int>()
        let merged = ResizeArray<string * Data>()

        for name, value in fields do
            match names.TryGetValue name with
            | true, index ->
                let _, existing = merged[index]
                merged[index] <- (name, merge existing value)
            | false, _ ->
                names[name] <- merged.Count
                merged.Add(name, merge (Data.Object []) value)

        merged |> Seq.toList |> Data.Object

    let mergeAll (layers: Data list) : Data =
        if isNull (box layers) then
            nullArg (nameof layers)

        layers |> List.fold merge (Data.Object [])
