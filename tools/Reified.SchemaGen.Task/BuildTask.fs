namespace Reified.SchemaGen.MSBuild

open System
open System.IO
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Reified.SchemaGen

/// Discovers schema declarations, generates changed files, and preserves F# compile order.
type GenerateSchemas() =
    inherit Task()

    let mutable sources: ITaskItem array = [||]
    let mutable contracts: ITaskItem array = [||]
    let mutable compileItems: ITaskItem array = [||]
    let mutable generatedCompileItems: ITaskItem array = [||]
    let mutable naming = "camel"
    let mutable contractNamespace = ""
    let mutable projectDirectory = ""
    let mutable intermediateOutputPath = ""
    let mutable outputMode = "Intermediate"

    [<Required>]
    member _.Sources with get () = sources and set value = sources <- value

    member _.Contracts with get () = contracts and set value = contracts <- value

    [<Required>]
    member _.CompileItems with get () = compileItems and set value = compileItems <- value

    member _.Naming with get () = naming and set value = naming <- value
    member _.ContractNamespace with get () = contractNamespace and set value = contractNamespace <- value

    [<Required>]
    member _.ProjectDirectory with get () = projectDirectory and set value = projectDirectory <- value

    [<Required>]
    member _.IntermediateOutputPath with get () = intermediateOutputPath and set value = intermediateOutputPath <- value

    member _.OutputMode with get () = outputMode and set value = outputMode <- value

    [<Output>]
    member _.GeneratedCompileItems with get () = generatedCompileItems and set value = generatedCompileItems <- value

    member private this.Fail(message: string) =
        this.Log.LogError message
        false

    override this.Execute() =
        try
            let schemaNaming =
                match naming.ToLowerInvariant() with
                | "camel" -> Some SchemaNaming.CamelCase
                | "snake" -> Some SchemaNaming.SnakeCase
                | "verbatim" -> Some SchemaNaming.Verbatim
                | _ -> None

            match schemaNaming with
            | None -> this.Fail $"Unknown ReifiedSchemaNaming '{naming}' (expected camel, snake, or verbatim)."
            | Some schemaNaming ->
                let sourcePaths =
                    sources
                    |> Array.map (fun item -> Path.GetFullPath item.ItemSpec)
                    // The generated F# frontend skips files without a schema declaration, so pass every
                    // ordinary source through. In particular, a final Program.fs may legally omit a
                    // namespace/module declaration.
                    |> Array.filter (fun path ->
                        path.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)
                        && not (path.EndsWith(".g.fs", StringComparison.OrdinalIgnoreCase)))
                    |> Array.distinct

                let contractPaths = contracts |> Array.map (fun item -> Path.GetFullPath item.ItemSpec) |> Array.distinct

                if contractPaths.Length > 0 && String.IsNullOrWhiteSpace contractNamespace then
                    this.Fail "ReifiedContractNamespace must be set when ReifiedContract items are declared."
                else
                    let fallbackNamespace = if String.IsNullOrWhiteSpace contractNamespace then "Generated" else contractNamespace

                    let generated =
                        GenerationPipeline.generate
                            schemaNaming
                            fallbackNamespace
                            [ for path in contractPaths -> path, File.ReadAllText path ]
                            [ for path in sourcePaths -> path, File.ReadAllText path ]

                    match generated with
                    | Error errors ->
                        for error in errors do
                            this.Log.LogError(string error)

                        false
                    | Ok generatedFiles ->
                        let checkedIn = outputMode.Equals("CheckedIn", StringComparison.OrdinalIgnoreCase)

                        if not checkedIn && not (outputMode.Equals("Intermediate", StringComparison.OrdinalIgnoreCase)) then
                            this.Fail $"Unknown ReifiedSchemaGeneratedFiles value '{outputMode}' (expected Intermediate or CheckedIn)."
                        else
                            let projectRoot = Path.GetFullPath projectDirectory
                            let generatedRoot = Path.GetFullPath(Path.Combine(projectRoot, intermediateOutputPath, "Reified.Schema"))

                            let outputPath (inputPath: string) =
                                if checkedIn then
                                    Path.ChangeExtension(inputPath, ".g.fs")
                                else
                                    let relative = Path.GetRelativePath(projectRoot, inputPath)
                                    let safeRelative =
                                        if relative.StartsWith(".." + string Path.DirectorySeparatorChar, StringComparison.Ordinal) then
                                            Path.Combine("external", string (uint (inputPath.GetHashCode())), Path.GetFileName inputPath)
                                        else
                                            relative

                                    Path.ChangeExtension(Path.Combine(generatedRoot, safeRelative), ".g.fs")

                            let outputsByInput =
                                generatedFiles
                                |> List.map (fun file ->
                                    let input = Path.GetFullPath file.SourcePath
                                    let output = outputPath input
                                    Directory.CreateDirectory(Path.GetDirectoryName output) |> ignore
                                    let existing = if File.Exists output then File.ReadAllText(output).Replace("\r\n", "\n") else ""

                                    if existing <> file.Content then
                                        File.WriteAllText(output, file.Content)
                                        this.Log.LogMessage(MessageImportance.High, $"Generated {output}")

                                    input, output)
                                |> Map.ofList

                            let generatedPaths = outputsByInput |> Map.toSeq |> Seq.map snd |> Set.ofSeq
                            let checkedInSiblings = outputsByInput |> Map.toSeq |> Seq.map (fun (input, _) -> Path.ChangeExtension(input, ".g.fs")) |> Set.ofSeq
                            let result = ResizeArray<ITaskItem>()

                            // Preserve ReifiedContract item order: generated declarations can reference contracts
                            // from earlier files, just like ordinary F# Compile items.
                            for input in contractPaths do
                                match Map.tryFind input outputsByInput with
                                | Some output -> result.Add(TaskItem output)
                                | None -> ()

                            for item in compileItems do
                                let path = Path.GetFullPath item.ItemSpec

                                if not (generatedPaths.Contains path || checkedInSiblings.Contains path) then
                                    result.Add(TaskItem item)

                                    match Map.tryFind path outputsByInput with
                                    | Some output -> result.Add(TaskItem output)
                                    | None -> ()

                            generatedCompileItems <- result.ToArray()
                            true
        with ex ->
            this.Log.LogErrorFromException(ex, true)
            false
