module Reified.ReleaseChecks.Program

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open Reified
open Reified.ConstraintDSL
open Reified.SchemaDSL

type ReleaseHistoryEntry =
    { Version: string
      CapsulePath: string option
      CapsuleUrl: string option
      CapsuleSha256: string option }

type ReleaseHistory =
    { SchemaVersion: int
      CurrentVersion: string
      Entries: ReleaseHistoryEntry list }

type GitHubAsset =
    { Name: string
      Digest: string option
      BrowserDownloadUrl: string }

type GitHubRelease =
    { TagName: string
      Assets: GitHubAsset list }

let versionConstraint = pattern @"^[0-9]+\.[0-9]+\.[0-9]+$"
let digestConstraint = pattern @"^[0-9a-f]{64}$"

let entrySchema =
    schema<ReleaseHistoryEntry> {
        fieldAs "Version" _.Version { constrain versionConstraint }
        fieldAs "CapsulePath" _.CapsulePath
        fieldAs "CapsuleUrl" _.CapsuleUrl
        fieldAs "CapsuleSha256" _.CapsuleSha256 {
            withSchema (Schema.option (Schema.text |> Schema.constrain digestConstraint))
        }
        construct (fun version path url digest ->
            { Version = version; CapsulePath = path; CapsuleUrl = url; CapsuleSha256 = digest })
    }

let historySchema =
    schema<ReleaseHistory> {
        fieldAs "SchemaVersion" _.SchemaVersion { constrain (equalTo 1) }
        fieldAs "CurrentVersion" _.CurrentVersion { constrain versionConstraint }
        fieldAs "Entries" _.Entries { withSchema (Schema.listWith entrySchema) }
        construct (fun schemaVersion current entries ->
            { SchemaVersion = schemaVersion; CurrentVersion = current; Entries = entries })
    }

let assetSchema =
    schema<GitHubAsset> {
        fieldAs "name" _.Name
        fieldAs "digest" _.Digest
        fieldAs "browser_download_url" _.BrowserDownloadUrl
        construct (fun name digest url -> { Name = name; Digest = digest; BrowserDownloadUrl = url })
    }

let releaseSchema =
    schema<GitHubRelease> {
        fieldAs "tag_name" _.TagName
        fieldAs "assets" _.Assets { withSchema (Schema.listWith assetSchema) }
        construct (fun tag assets -> { TagName = tag; Assets = assets })
    }

let releasesSchema = Schema.listWith (Schema.listWith releaseSchema)
let historyCodec = Json.compile historySchema

let parseFile schema path =
    use document = JsonDocument.Parse(File.ReadAllText path)
    match Schema.parse schema (Data.ofJsonDocument document) with
    | Ok value -> value
    | Error errors ->
        errors
        |> SchemaErrors.toList
        |> List.map (fun issue -> $"{SchemaPath.format issue.Path}: {SchemaError.render issue.Error}")
        |> String.concat Environment.NewLine
        |> failwith

let readHistory path = parseFile historySchema path

let writeHistory path history = File.WriteAllText(path, Json.serialize historyCodec history + Environment.NewLine)

let versionKey (version: string) =
    version.Split('.') |> Array.map Int32.Parse |> fun parts -> parts[0], parts[1], parts[2]

let validateOrder history =
    let versions = history.Entries |> List.map _.Version
    if versions.IsEmpty || List.distinct versions |> List.length <> versions.Length then
        failwith "History must contain unique semantic versions."
    let expected = versions |> List.sortByDescending versionKey
    let displayedVersions = String.Join(", ", versions)
    if versions <> expected then failwith $"History is not newest-first: {displayedVersions}"
    if history.CurrentVersion <> versions.Head then
        failwith $"CurrentVersion '{history.CurrentVersion}' is not newest version '{versions.Head}'."

let sortHistory path =
    let history = readHistory path
    let sorted = { history with Entries = history.Entries |> List.sortByDescending (_.Version >> versionKey) }
    validateOrder sorted
    writeHistory path sorted

let mergeReleases manifestPath releasesPath expectedVersion expectedUrl expectedSha =
    let history = readHistory manifestPath
    let discovered =
        parseFile releasesSchema releasesPath
        |> List.collect id
        |> List.choose (fun release ->
            if not (release.TagName.StartsWith "v") then None
            else
                let version = release.TagName.Substring 1
                match Constraint.check versionConstraint version with
                | Error _ -> None
                | Ok () ->
                    release.Assets
                    |> List.tryFind (fun asset -> asset.Name = $"Reified-{version}-livedocs.zip")
                    |> Option.bind (fun asset ->
                        asset.Digest
                        |> Option.filter (fun digest -> digest.StartsWith "sha256:")
                        |> Option.map (fun digest ->
                            { Version = version
                              CapsulePath = None
                              CapsuleUrl = Some asset.BrowserDownloadUrl
                              CapsuleSha256 = Some(digest.Substring "sha256:".Length) })))
    let byVersion = history.Entries |> List.map (fun entry -> entry.Version, entry) |> Map.ofList
    let merged =
        discovered
        |> List.fold (fun entries entry ->
            match Map.tryFind entry.Version entries with
            | None -> Map.add entry.Version entry entries
            | Some existing when existing.CapsuleUrl = entry.CapsuleUrl && existing.CapsuleSha256 = entry.CapsuleSha256 -> entries
            | Some _ -> failwith $"History contains different capsule metadata for {entry.Version}.") byVersion
        |> Map.values
        |> List.ofSeq
        |> List.sortByDescending (_.Version >> versionKey)
    let updated = { history with CurrentVersion = merged.Head.Version; Entries = merged }
    match expectedVersion, expectedUrl, expectedSha with
    | Some version, Some url, Some sha ->
        let actual = updated.Entries |> List.tryFind (fun entry -> entry.Version = version)
        if actual |> Option.exists (fun entry -> entry.CapsuleUrl = Some url && entry.CapsuleSha256 = Some sha) |> not then
            failwith $"Dispatched release {version} was not found with the expected capsule."
        if updated.CurrentVersion <> version then failwith $"Dispatched release {version} is not current."
    | None, None, None -> ()
    | _ -> failwith "Expected release version, URL, and SHA-256 must be supplied together."
    validateOrder updated
    writeHistory manifestPath updated

let localTarget (output: string) (page: string) (target: string) =
    let target = target.Split([| '#'; '?' |], 2)[0]
    if String.IsNullOrWhiteSpace target || target.StartsWith("http:") || target.StartsWith("https:") || target.StartsWith("mailto:") then None
    else
        let relative =
            if target.StartsWith("/Reified/") then target.Substring("/Reified/".Length)
            elif target.StartsWith('/') then target.TrimStart('/')
            else Path.Combine(Path.GetDirectoryName page, Uri.UnescapeDataString target)
        let path = Path.GetFullPath(Path.Combine(output, relative))
        Some(if Directory.Exists path then Path.Combine(path, "index.html") else path)

let checkOutput manifestPath output =
    let history = readHistory manifestPath
    validateOrder history
    for entry in history.Entries.Tail do
        let path = Path.Combine(output, "history", entry.Version, "index.html")
        if not (File.Exists path) then failwith $"Missing version entry point: {path}"
    let links = Regex("(?:href|src)=\"([^\"]+)\"", RegexOptions.Compiled)
    let failures = ResizeArray<string>()
    for page in Directory.EnumerateFiles(output, "*.html", SearchOption.AllDirectories) do
        let relativePage = Path.GetRelativePath(output, page)
        for found in links.Matches(File.ReadAllText page) do
            match localTarget output relativePage found.Groups[1].Value with
            | Some target when not (File.Exists target) -> failures.Add($"{relativePage} -> {found.Groups[1].Value}")
            | _ -> ()
    if failures.Count > 0 then
        failures |> Seq.truncate 50 |> String.concat Environment.NewLine |> fun detail -> failwith $"Generated links do not resolve:{Environment.NewLine}{detail}"
    let landing = File.ReadAllText(Path.Combine(output, "index.html"))
    let positions = history.Entries |> List.map (fun entry -> landing.IndexOf($">{entry.Version}<", StringComparison.Ordinal))
    if positions |> List.exists (fun position -> position < 0) || positions <> List.sort positions then
        failwith "Version switcher is missing or out of order."
    let pageCount = Directory.EnumerateFiles(output, "*.html", SearchOption.AllDirectories) |> Seq.length
    printfn $"Verified {history.Entries.Length} versions and {pageCount} HTML pages."

[<EntryPoint>]
let main argv =
    try
        match argv with
        | [| "sort-history"; manifest |] -> sortHistory manifest
        | [| "merge-releases"; manifest; releases |] -> mergeReleases manifest releases None None None
        | [| "merge-releases"; manifest; releases; version; url; sha |] ->
            mergeReleases manifest releases (Some version) (Some url) (Some sha)
        | [| "check-output"; manifest; output |] -> checkOutput manifest output
        | _ -> failwith "usage: Reified.ReleaseChecks sort-history <manifest> | merge-releases <manifest> <releases-json> [version url sha256] | check-output <manifest> <output>"
        0
    with error ->
        eprintfn $"{error.Message}"
        1
