module Reified.Tests.PackageGraphTests

open System.IO
open System.Xml.Linq
open Swensen.Unquote
open Xunit

// The umbrella package is a promise about the graph, not about code: "install Reified and every
// runtime package comes with it". Nothing in the compiler enforces that promise, so these tests read
// the project files and check it directly. A new packable runtime package fails the first test until
// it is added to the umbrella.

let private repoRoot () =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

let private srcDir () = Path.Combine(repoRoot (), "src")

let private umbrellaPath () =
    Path.Combine(srcDir (), "Reified", "Reified.fsproj")

let private load (projectPath: string) = XDocument.Load(projectPath)

let private values (project: XDocument) (element: string) (attribute: string) =
    project.Descendants(XName.Get element)
    |> Seq.choose (fun node ->
        match node.Attribute(XName.Get attribute) with
        | null -> None
        | value -> Some value.Value)
    |> List.ofSeq

let private property (project: XDocument) (name: string) =
    project.Descendants(XName.Get name)
    |> Seq.tryHead
    |> Option.map (fun node -> node.Value)

/// Every project under src/ that ships an assembly to NuGet. Tooling libraries set IsPackable=false
/// and the targets-only build package sets IncludeBuildOutput=false, so both drop out here.
let private packableRuntimeProjects () =
    Directory.EnumerateFiles(srcDir (), "*.fsproj", SearchOption.AllDirectories)
    |> Seq.filter (fun path ->
        let project = load path

        let isPackable =
            property project "IsPackable" |> Option.forall (fun value -> value <> "false")

        let shipsAssembly =
            property project "IncludeBuildOutput"
            |> Option.forall (fun value -> value <> "false")

        isPackable && shipsAssembly)
    |> Seq.map Path.GetFileNameWithoutExtension
    |> Set.ofSeq

let private umbrellaReferences () =
    values (load (umbrellaPath ())) "ProjectReference" "Include"
    |> List.map (fun include' -> Path.GetFileNameWithoutExtension(include'.Replace('\\', '/')))
    |> Set.ofList

[<Fact>]
let ``the umbrella references every packable runtime package`` () =
    test <@ umbrellaReferences () = packableRuntimeProjects () @>

[<Fact>]
let ``the umbrella lists the settled public runtime packages`` () =
    let expected =
        Set.ofList
            [ "Reified.Constraint"
              "Reified.Refinements"
              "Reified.Parse"
              "Reified.Result"
              "Reified.Data"
              "Reified.Schema"
              "Reified.Schema.Json"
              "Reified.Schema.Http" ]

    test <@ umbrellaReferences () = expected @>

[<Fact>]
let ``the umbrella compiles no sources of its own`` () =
    // No Compile items means no second place a Reified name can come from: the umbrella can never
    // grow an API that competes with the package that owns the type.
    test <@ values (load (umbrellaPath ())) "Compile" "Include" = [] @>
    test <@ property (load (umbrellaPath ())) "IncludeBuildOutput" = Some "false" @>

[<Fact>]
let ``the umbrella does not pull repository tooling into a consumer`` () =
    // The contract compiler and the FsCheck adapter are repository tooling. Reaching a consumer
    // through the umbrella would put FCS and FsCheck on an application's dependency graph.
    let references = umbrellaReferences ()
    test <@ not (references.Contains "Reified.Schema.Contracts") @>
    test <@ not (references.Contains "Reified.Schema.Testing") @>

[<Fact>]
let ``the build-integration package stays a direct install`` () =
    // MSBuild build/ assets are not transitive, so an umbrella dependency would install the targets
    // without ever running them. Referencing Reified.Schema.Contracts.Build is the consumer's job.
    test <@ not ((umbrellaReferences ()).Contains "Reified.Schema.Contracts.Build") @>

[<Fact>]
let ``every packable runtime package is packed by the release script`` () =
    let packScript =
        File.ReadAllText(Path.Combine(repoRoot (), "scripts", "pack.sh"))

    let unpacked =
        packableRuntimeProjects ()
        |> Set.add "Reified"
        |> Set.add "Reified.Schema.Contracts.Build"
        |> Set.filter (fun project -> not (packScript.Contains $"/{project}.fsproj"))

    test <@ unpacked = Set.empty @>
