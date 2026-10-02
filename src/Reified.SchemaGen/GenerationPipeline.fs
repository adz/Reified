namespace Reified.SchemaGen

/// Shared parse, resolve, and emit pipeline used by the CLI and the MSBuild task.
module GenerationPipeline =

    /// <summary>One emitted source file: the input it was generated from and its resulting content.</summary>
    type GeneratedFile =
        { SourcePath: string
          Content: string }

    /// <summary>Parses contract and F# inputs, resolves their combined declarations, and emits every generated
    /// file. Input discovery and output handling remain the responsibility of the host.</summary>
    let generate
        (naming: SchemaNaming)
        (fallbackNamespace: string)
        (contractSources: (string * string) list)
        (fsharpSources: (string * string) list)
        : Result<GeneratedFile list, ContractDiagnostic list> =
        let parsed =
            [ yield! contractSources |> List.map (fun (path, source) -> path, Parser.parse path source)
              yield! Records.parseSet naming fsharpSources ]

        let parseErrors =
            parsed
            |> List.collect (fun (_, result) ->
                match result with
                | Error diagnostics -> diagnostics
                | Ok _ -> [])

        if not (List.isEmpty parseErrors) then
            Error parseErrors
        else
            let files =
                parsed
                |> List.choose (fun (_, result) ->
                    match result with
                    | Ok file when not (List.isEmpty file.Contracts) -> Some file
                    | _ -> None)

            let resolveErrors = Resolver.resolve files

            if not (List.isEmpty resolveErrors) then
                Error resolveErrors
            else
                files
                |> List.map (fun file ->
                    { SourcePath = file.FilePath
                      Content = Emitter.emit fallbackNamespace files file })
                |> Ok
