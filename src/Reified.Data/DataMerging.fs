namespace Reified

open System
open System.Collections.Generic

/// <summary>Internal right-biased deep merge of structured data layers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal DataMerging =
    let ordinalIgnoreCase: IEqualityComparer<string> =
#if FABLE_COMPILER
        // JavaScript full casing expands characters and lowercase conflates Kelvin/K while separating final sigma.
        // OrdinalIgnoreCase uses simple uppercase, never expanding one scalar or folding non-ASCII into ASCII.
        let fold (text: string) =
            if isNull text then null
            else
                let parts = ResizeArray<string>()
                let mutable index = 0
                while index < text.Length do
                    let code = int text.[index]
                    let width =
                        if code >= 0xD800 && code <= 0xDBFF && index + 1 < text.Length &&
                           int text.[index + 1] >= 0xDC00 && int text.[index + 1] <= 0xDFFF then 2 else 1
                    let scalar = text.Substring(index, width)
                    let upper =
                        // Unicode simple-uppercase mappings whose JS full-uppercase form expands a Greek iota.
                        if (code >= 0x1F80 && code <= 0x1F87) || (code >= 0x1F90 && code <= 0x1F97) ||
                           (code >= 0x1FA0 && code <= 0x1FA7) then string (char (code + 8))
                        elif code = 0x1FB3 then "\u1FBC"
                        elif code = 0x1FC3 then "\u1FCC"
                        elif code = 0x1FF3 then "\u1FFC"
                        else scalar.ToUpperInvariant()
                    let folded =
                        if upper.Length <> width || (code >= 128 && upper.Length = 1 && int upper.[0] < 128) then scalar
                        else upper
                    parts.Add folded
                    index <- index + width
                String.concat "" parts
        { new IEqualityComparer<string> with
            member _.Equals(left, right) = fold left = fold right
            member _.GetHashCode(value) = if isNull value then 0 else hash (fold value) }
#else
        StringComparer.OrdinalIgnoreCase
#endif

    let mergeWith (comparer: IEqualityComparer<string>) (left: Data) (right: Data) : Data =
        if isNull (box comparer) then nullArg (nameof comparer)
        let rec mergeValue left right =
            match left, right with
            | Data.Object leftFields, Data.Object rightFields -> mergeFields (leftFields @ rightFields)
            | _, Data.Object rightFields -> mergeFields rightFields
            | _, value -> value
        and mergeFields fields =
            let names = Dictionary<string, int>(comparer)
            let merged = ResizeArray<string * Data>()
            for name, value in fields do
                match names.TryGetValue name with
                | true, index ->
                    let originalName, existing = merged[index]
                    merged[index] <- (originalName, mergeValue existing value)
                | false, _ ->
                    names[name] <- merged.Count
                    merged.Add(name, mergeValue (Data.Object []) value)
            merged |> Seq.toList |> Data.Object
        mergeValue left right

    let merge left right = mergeWith EqualityComparer<string>.Default left right

    let mergeAllWith comparer (layers: Data list) : Data =
        if isNull (box comparer) then nullArg (nameof comparer)
        if isNull (box layers) then nullArg (nameof layers)
        layers |> List.fold (mergeWith comparer) (Data.Object [])

    let mergeAll layers = mergeAllWith EqualityComparer<string>.Default layers
