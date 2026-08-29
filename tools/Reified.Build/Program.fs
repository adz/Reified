open System
open System.Diagnostics
open System.IO
open System.Xml.Linq
open Fake.Core
open Fake.Core.TargetOperators

let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))
Directory.SetCurrentDirectory root
Context.setExecutionContextFromCommandLineArgs "tools/Reified.Build/Program.fs"

let runCapture file args env =
    let info = ProcessStartInfo(file, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    args |> List.iter info.ArgumentList.Add
    env |> List.iter (fun (key, value) -> info.Environment[key] <- value)
    use child = Process.Start info
    let output = child.StandardOutput.ReadToEndAsync()
    let error = child.StandardError.ReadToEndAsync()
    child.WaitForExit()
    let stdout, stderr = output.Result, error.Result
    if stdout.Length > 0 then Console.Write stdout
    if stderr.Length > 0 then Console.Error.Write stderr
    child.ExitCode, stdout + stderr

let run file args =
    let code, _ = runCapture file args []
    if code <> 0 then failwith $"{file} failed with exit code {code}"

let dotnet args = run "dotnet" args
let configuredVersion () =
    let xml = XDocument.Load "Directory.Build.props"
    xml.Descendants(XName.Get "ReifiedVersion") |> Seq.exactlyOne |> _.Value
let releaseVersion () = Environment.GetEnvironmentVariable "REIFIED_VERSION" |> Option.ofObj |> Option.defaultWith configuredVersion
let historyPath () = Environment.GetEnvironmentVariable "LIVEDOCS_HISTORY" |> Option.ofObj |> Option.defaultValue ".livedocs/history.json"
let target name action = Target.create name (fun _ -> action ())

let packProjects =
    [ "src/Reified.Data/Reified.Data.fsproj"; "src/Reified.Result/Reified.Result.fsproj"
      "src/Reified.Constraint/Reified.Constraint.fsproj"; "src/Reified.Refinements/Reified.Refinements.fsproj"
      "src/Reified.Parse/Reified.Parse.fsproj"; "src/Reified.Schema/Reified.Schema.fsproj"
      "src/Reified.Schema.Http/Reified.Schema.Http.fsproj"
      "src/Reified.Schema.Contracts.Build/Reified.Schema.Contracts.Build.fsproj"; "src/Reified/Reified.fsproj" ]

target "CleanPackages" (fun () ->
    Directory.CreateDirectory "artifacts/package" |> ignore
    Directory.EnumerateFiles("artifacts/package")
    |> Seq.filter (fun path -> path.EndsWith(".nupkg") || path.EndsWith(".snupkg"))
    |> Seq.iter File.Delete)

target "Restore" (fun () -> dotnet [ "tool"; "restore" ]; dotnet [ "restore"; "--force" ])
target "Build" (fun () -> dotnet [ "build"; "Reified.slnx"; "--configuration"; "Release"; "--no-restore"; "--nologo"; "-v"; "minimal" ])
target "Test" (fun () -> dotnet [ "test"; "Reified.slnx"; "--configuration"; "Release"; "--no-build"; "--nologo"; "-v"; "minimal" ])

target "SourceInventory" (fun () ->
    let relative path = Path.GetRelativePath(root, Path.GetFullPath path).Replace('\\', '/')
    let projects =
        [ "src"; "tests"; "tools" ]
        |> Seq.collect (fun directory -> Directory.EnumerateFiles(directory, "*.fsproj", SearchOption.AllDirectories))
        |> Seq.filter (fun path -> not (path.Contains "package-consumers"))
        |> Seq.map relative
        |> Set.ofSeq
    let solution =
        XDocument.Load("Reified.slnx").Descendants(XName.Get "Project")
        |> Seq.choose (fun element -> element.Attribute(XName.Get "Path") |> Option.ofObj |> Option.map _.Value)
        |> Seq.filter (fun path -> path.StartsWith("src/") || path.StartsWith("tests/") || path.StartsWith("tools/"))
        |> Set.ofSeq
    if projects <> solution then
        failwith $"Source project inventory mismatch. Missing from solution: {Set.difference projects solution}; stale: {Set.difference solution projects}"
    let sources =
        [ "src"; "tests"; "tools" ]
        |> Seq.collect (fun directory -> Directory.EnumerateFiles(directory, "*.fs", SearchOption.AllDirectories))
        |> Seq.filter (fun path -> not (path.Contains "package-consumers"))
        |> Seq.map relative
        |> Set.ofSeq
    let included =
        projects
        |> Seq.collect (fun project ->
            let document = XDocument.Load project
            let directory = Path.GetDirectoryName project
            document.Descendants(XName.Get "Compile")
            |> Seq.choose (fun element ->
                element.Attribute(XName.Get "Include")
                |> Option.ofObj
                |> Option.map (fun attribute -> relative (Path.Combine(directory, attribute.Value)))))
        |> Set.ofSeq
    if sources <> included then
        failwith $"Source inventory mismatch. Uncompiled: {Set.difference sources included}; stale: {Set.difference included sources}"
    printfn "Source inventory covers src/tests/tools .fs and .fsproj files.")

target "SchemaCompilerErrors" (fun () ->
    for project in [ "Reified.Result"; "Reified.Schema" ] do dotnet [ "build"; $"src/{project}/{project}.fsproj"; "--nologo"; "-v"; "quiet" ]
    let fixtures = [ "raw-field-without-refine.fsx", "A field block must finish with the getter type"; "missing-refinement.fsx", "static member Refinement"; "constraint-after-refine.fsx", "No overloads match for method 'Constrain'"; "validation-at-wrong-stage.fsx", "No overloads match for method 'Validate'"; "constructor-mismatch.fsx", "The type 'int' does not match the type 'string'"; "ambiguous-refinement.fsx", "Duplicate method"; "optional-on-non-option-field.fsx", "The type ''a option' does not match the type 'string'" ]
    for fixture, expected in fixtures do
        let code, output = runCapture "dotnet" [ "fsi"; "--exec"; $"tests/compile-fail/schema-ce/{fixture}" ] []
        if code = 0 || not (output.Contains expected) then failwith $"{fixture} did not report: {expected}\n{output}")

target "Fable" (fun () ->
    let out = "artifacts/fable-js-surface"
    if Directory.Exists out then Directory.Delete(out, true)
    Directory.CreateDirectory out |> ignore; File.WriteAllText(Path.Combine(out, "package.json"), "{ \"type\": \"module\" }")
    let code, _ = runCapture "dotnet" [ "fable"; "examples/Reified.FableProbe/Reified.FableProbe.fsproj"; "--lang"; "javascript"; "--outDir"; out ] [ "TreatWarningsAsErrors", "false" ]
    if code <> 0 then failwith "Fable compilation failed"
    if not (File.Exists(Path.Combine(out, "src/Reified.Schema/Json.js"))) then failwith "Reified.Schema's JSON codec was absent from Fable output"
    let _, output = runCapture "node" [ Path.Combine(out, "Program.js") ] []
    for expected in [ "Schema record plan: ok"; "Codec round-trip: ok"; "Constraints: ok"; "Operand agreement: ok"; "Localization: ok"; "Data JSON boundaries: ok"; "Reified Fable probe: ok" ] do if not (output.Contains expected) then failwith $"Fable probe missing: {expected}"
    if Directory.EnumerateFiles(out, "*", SearchOption.AllDirectories) |> Seq.exists (fun p -> File.ReadAllText(p).Contains "ResourceManager") then failwith "ResourceManager leaked into Fable output")

target "NativeAot" (fun () -> for product in [ "Result"; "Constraint"; "Refinements"; "Schema" ] do let dir = $"artifacts/publish/Reified.{product}.AotProbe/linux-x64" in dotnet [ "publish"; $"examples/Reified.{product}.AotProbe/Reified.{product}.AotProbe.fsproj"; "-c"; "Release"; "-r"; "linux-x64"; "-o"; dir ]; run (Path.Combine(dir, $"Reified.{product}.AotProbe")) [])

let packAll () =
    let version = releaseVersion ()
    for project in packProjects do
        dotnet [ "pack"; project; "--configuration"; "Release"; "--output"; "artifacts/package"; $"-p:ReifiedVersion={version}" ]

let testPackageConsumers () =
    let version = releaseVersion ()
    for package in [ "Reified"; "Reified.Result"; "Reified.Parse"; "Reified.Constraint"; "Reified.Refinements"; "Reified.Data"; "Reified.Schema"; "Reified.Schema.Http" ] do
        let cached = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages", package.ToLowerInvariant(), version)
        if Directory.Exists cached then Directory.Delete(cached, true)
    for fixture in Directory.EnumerateDirectories("tests/package-consumers", "Consumer.*") do
        let name = Path.GetFileName fixture
        dotnet [ "run"; "--project"; Path.Combine(fixture, name + ".fsproj"); $"-p:ReifiedPackageVersion={version}"; "--configuration"; "Release"; "--nologo" ]

target "Pack" packAll
target "PackageConsumers" testPackageConsumers
target "ReleasePackages" (fun () ->
    Directory.CreateDirectory "artifacts/package" |> ignore
    Directory.EnumerateFiles("artifacts/package")
    |> Seq.filter (fun path -> path.EndsWith(".nupkg") || path.EndsWith(".snupkg"))
    |> Seq.iter File.Delete
    packAll ()
    testPackageConsumers ())

target "DocsAudit" (fun () -> dotnet [ "livedocs"; "audit"; "--warn-as-error"; "--interactive"; "false"; "--banner"; "false" ])
target "DocsBuild" (fun () -> dotnet [ "livedocs"; "build"; "--version"; releaseVersion (); "--interactive"; "false"; "--banner"; "false" ])
target "DocsCapture" (fun () ->
    let capsule = $"artifacts/Reified-{releaseVersion ()}-livedocs.zip"
    if File.Exists capsule then File.Delete capsule
    dotnet [ "livedocs"; "capture"; "--version"; releaseVersion (); "--output"; capsule; "--interactive"; "false"; "--banner"; "false" ])
target "CheckReleaseNotes" (fun () ->
    let version = releaseVersion ()
    if configuredVersion () <> version || File.ReadAllText("NEXT_VERSION").Trim() <> version || not (File.Exists $"dev-docs/releases/{version}.md") then
        failwith $"Directory.Build.props, NEXT_VERSION, and release notes are not prepared for {version}")
let mergeReleasedCapsules manifest expected =
    let releases = Path.GetTempFileName()
    try
        let repository = Environment.GetEnvironmentVariable "GITHUB_REPOSITORY"
        if String.IsNullOrWhiteSpace repository then failwith "GITHUB_REPOSITORY is required for release history discovery."
        let mutable complete = false
        for attempt in 1..5 do
            if not complete then
                let code, json = runCapture "gh" [ "api"; "--paginate"; "--slurp"; $"repos/{repository}/releases?per_page=100" ] []
                if code <> 0 && json.Contains "GH_TOKEN" then
                    failwith "GitHub CLI authentication is unavailable. Set GH_TOKEN for the Pages target."
                if code = 0 then
                    File.WriteAllText(releases, json)
                    let check, _ = runCapture "dotnet" ([ "run"; "--project"; "tools/Reified.ReleaseChecks"; "--"; "merge-releases"; manifest; releases ] @ expected) []
                    if check = 0 then complete <- true
                if not complete && attempt < 5 then Threading.Thread.Sleep 5000
        if not complete then failwith "Released capsules were not visible with the expected metadata after five attempts."
    finally
        File.Delete releases

target "AddCandidateHistory" (fun () ->
    let version = releaseVersion ()
    let directory = "artifacts/release-candidate"
    let manifest = Path.Combine(directory, "history.json")
    Directory.CreateDirectory directory |> ignore
    File.Copy(".livedocs/history.json", manifest, true)
    mergeReleasedCapsules manifest []
    Environment.SetEnvironmentVariable("LIVEDOCS_HISTORY", manifest)
    dotnet [ "livedocs"; "history-add"; version; "--capsule"; Path.GetFullPath($"artifacts/Reified-{version}-livedocs.zip"); "--output"; manifest; "--interactive"; "false"; "--banner"; "false" ])
target "SyncHistory" (fun () ->
    let supplied name =
        Environment.GetEnvironmentVariable name
        |> Option.ofObj
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
    let expected =
        match supplied "RELEASE_VERSION", supplied "RELEASE_CAPSULE_URL", supplied "RELEASE_CAPSULE_SHA256" with
        | None, None, None -> []
        | Some version, Some url, Some sha -> [ version; url; sha ]
        | _ -> failwith "RELEASE_VERSION, RELEASE_CAPSULE_URL, and RELEASE_CAPSULE_SHA256 must be supplied together."
    mergeReleasedCapsules (historyPath ()) expected)
target "SortHistory" (fun () -> dotnet [ "run"; "--project"; "tools/Reified.ReleaseChecks"; "--"; "sort-history"; historyPath () ])

let buildHistory () =
    let mutable complete = false
    for attempt in 1..3 do
        if not complete then
            let code, _ = runCapture "dotnet" [ "livedocs"; "build-history"; historyPath (); "--interactive"; "false"; "--banner"; "false" ] []
            if code = 0 then complete <- true
            elif attempt < 3 then Threading.Thread.Sleep 10000
    if not complete then failwith "LiveDocs history build failed after three attempts"

let addCompatibilityRoutes () =
    let redirects =
        [ "api/packages/Reified.Schema.Json.html", "Reified.Schema.html"
          "api/Reified.Schema.Json.html", "Reified.Schema.html"
          "api/Reified.Schema.Json.Json.html", "Reified.Json.html"
          "api/Reified.Schema.Json.JsonCodec`1.html", "Reified.JsonCodec`1.html"
          "api/Reified.Schema.Json.JsonCodecException.html", "Reified.JsonCodecException.html" ]
    for route, destination in redirects do
        let path = Path.Combine("output", route)
        let destinationPath = Path.Combine(Path.GetDirectoryName path, destination)
        if not (File.Exists path) && File.Exists destinationPath then
            Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
            File.WriteAllText(path, $"<!doctype html><html><head><meta http-equiv=\"refresh\" content=\"0; url={destination}\"><link rel=\"canonical\" href=\"{destination}\"></head><body><a href=\"{destination}\">Moved</a></body></html>")

let checkPages () =
    dotnet [ "run"; "--project"; "tools/Reified.ReleaseChecks"; "--"; "check-output"; historyPath (); "output" ]

target "BuildCandidateHistory" buildHistory
target "AddCandidateCompatibilityRoutes" addCompatibilityRoutes
target "CheckCandidatePages" checkPages
target "BuildSyncedHistory" buildHistory
target "AddSyncedCompatibilityRoutes" addCompatibilityRoutes
target "CheckSyncedPages" checkPages
target "Logo" (fun () -> run "python3" [ "scripts/build-logo.py" ])
target "Benchmarks" (fun () ->
    let arguments =
        Environment.GetEnvironmentVariable "BENCHMARK_ARGS"
        |> Option.ofObj
        |> Option.map (fun value -> value.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> Array.toList)
        |> Option.defaultValue []
    dotnet ([ "run"; "--configuration"; "Release"; "--project"; "benchmarks/Reified.Schema.Benchmarks/ReifiedBenchmarks.fsproj"; "--" ] @ arguments))

target "Core" ignore
target "Verify" ignore
target "Docs" ignore
target "ReleaseCandidate" ignore
target "Pages" ignore
target "Default" ignore

"Restore" ==> "Build" ==> "Test" ==> "Verify" |> ignore
"SourceInventory" ==> "Verify" |> ignore
"Build" ==> "SchemaCompilerErrors" ==> "Verify" |> ignore
"Build" ==> "Fable" ==> "Verify" |> ignore
"Build" ==> "NativeAot" ==> "Verify" |> ignore
[ "Test"; "SourceInventory"; "SchemaCompilerErrors" ]
|> List.iter (fun dependency -> dependency ==> "Core" |> ignore)
"Build" ==> "DocsAudit" ==> "DocsBuild" ==> "DocsCapture" |> ignore
"CleanPackages" ==> "Pack" ==> "PackageConsumers" |> ignore
"DocsCapture" ==> "AddCandidateHistory" ==> "BuildCandidateHistory" ==> "AddCandidateCompatibilityRoutes" ==> "CheckCandidatePages" ==> "ReleasePackages" |> ignore
"SyncHistory" ==> "BuildSyncedHistory" ==> "AddSyncedCompatibilityRoutes" ==> "CheckSyncedPages" ==> "Pages" |> ignore
[ "Verify"; "ReleasePackages"; "CheckCandidatePages"; "CheckReleaseNotes" ]
|> List.iter (fun dependency -> dependency ==> "ReleaseCandidate" |> ignore)
"Verify" ==> "Default" |> ignore

let arguments = Environment.GetCommandLineArgs() |> Array.toList
let requestedTarget =
    arguments
    |> List.tryFindIndex (fun argument -> argument = "--target" || argument = "-t")
    |> Option.bind (fun index -> arguments |> List.tryItem (index + 1))
    |> Option.defaultValue "Default"
Target.runOrDefaultWithArguments requestedTarget
