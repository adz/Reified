open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Templating
open FSharp.Compiler.Symbols
open System
open System.IO
open System.Reflection
open System.Collections.Generic
open System.Net
open System.Text.RegularExpressions

let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))
let githubRepoUrl = "https://github.com/adz/Reified"
let githubBranch = "main"

type PageSpec = {
    OutPath: string list
    Title: string
    Description: string
    Intro: string
    SymbolIds: (string * string list) list
    Alias: string option
}

let normalize (name: string) =
    if String.IsNullOrEmpty name then ""
    else
        name.Replace("Reified.", "").Replace("Reified.", "").Replace("Services.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s ->
            s
                .Split('(').[0]
                .Trim('.')
                |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"``[0-9]+(?=$|[.])", "")
                |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"`[0-9]+(?=$|[.])", "")
                |> fun value -> value.Replace("`", "").Replace("'", ""))

let cleanName (name: string) =
    if String.IsNullOrEmpty name then ""
    else
        name.Replace("Reified.", "").Replace("Reified.", "").Replace("Services.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> s.Trim('.'))
        |> (fun s -> 
            s
            |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"``[0-9]+(?=$|[.])", "")
            |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"`[0-9]+(?=$|[.])", "")
        )
        |> (fun s -> s.Replace("'", ""))
        |> (fun s -> if s.EndsWith(".Static") then s.Substring(0, s.Length - 7) else s)

/// Collapses consecutive duplicate dotted segments, e.g. "Flow.Flow.ToAsync" -> "Flow.ToAsync",
/// "Schema.Schema" -> "Schema". Module names frequently coincide with their enclosing namespace's
/// last segment, which otherwise shows up doubled in generated titles and index links.
let dedupeAdjacentSegments (name: string) =
    if String.IsNullOrEmpty name then name
    else
        let parts = name.Split('.')
        let result = ResizeArray<string>()
        for part in parts do
            if result.Count = 0 || result.[result.Count - 1] <> part then
                result.Add part
        String.Join(".", result)

let sanitizeFilename (name: string) =
    name.Replace("`", "-").Replace("'", "-").Replace(" ", "-").Replace(".", "-").ToLower()
    |> (fun s -> s.Trim('-'))

let formatterApiSlug (name: string) =
    name.Replace("`", "-").Replace("'", "").Replace(".", "-").Replace("+", "-").ToLowerInvariant()

let getPageName (id: string) =
    let kind = id.[0].ToString().ToLower()
    let namePart = id.Substring(2).Split('(').[0]
    let clean = 
        namePart.Replace("Reified.", "").Replace("Reified.", "").Replace("Services.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> System.Text.RegularExpressions.Regex.Replace(s, @"`[0-9]+", ""))
        |> (fun s -> s.Replace("'", "").Trim('.'))
        
    let finalName = sanitizeFilename clean
    $"{kind}-{finalName}.md"

let safeFullName (sym: FSharp.Compiler.Symbols.FSharpSymbol) =
    match sym with
    | :? FSharp.Compiler.Symbols.FSharpEntity as e ->
        try e.FullName with _ -> e.DisplayName
    | _ -> 
        try sym.FullName with _ -> sym.DisplayName

let logicalName (sym: FSharp.Compiler.Symbols.FSharpSymbol) =
    match sym with
    | :? FSharpMemberOrFunctionOrValue as mfv when mfv.IsExtensionMember ->
        try 
            match mfv.ApparentEnclosingEntity with
            | Some ent -> $"{ent.FullName}.{mfv.DisplayName}"
            | None -> safeFullName sym
        with _ -> safeFullName sym
    | _ -> safeFullName sym

let hasAttribute named (attrs: seq<FSharpAttribute>) =
    attrs
    |> Seq.exists (fun attr ->
        attr.AttributeType.DisplayName = named
        || attr.AttributeType.FullName.EndsWith("." + named, StringComparison.Ordinal))

let enclosingEntity (sym: FSharp.Compiler.Symbols.FSharpSymbol) =
    match sym with
    | :? FSharpMemberOrFunctionOrValue as mfv -> mfv.DeclaringEntity
    | :? FSharpField as field -> field.DeclaringEntity
    | _ -> None

let memberQualifier (m: ApiDocMember) =
    match m.Symbol with
    | :? FSharpMemberOrFunctionOrValue as mfv when mfv.IsExtensionMember ->
        try 
            match mfv.ApparentEnclosingEntity with
            | Some ent -> cleanName ent.FullName
            | None -> ""
        with _ -> ""
    | _ ->
        match enclosingEntity m.Symbol with
        | Some ent ->
            let moduleName = cleanName ent.FullName
            let isAutoOpen = hasAttribute "AutoOpenAttribute" ent.Attributes
            let isRequireQualifiedAccess = hasAttribute "RequireQualifiedAccessAttribute" ent.Attributes

            if isAutoOpen && not isRequireQualifiedAccess then ""
            else moduleName
        | None -> ""

let qualifyUsageHtml usageName (html: string) =
    let encodedUsageName = WebUtility.HtmlEncode usageName
    let parts = usageName.Split('.')
    let shortName = parts.[parts.Length - 1]
    let encodedShortName = WebUtility.HtmlEncode shortName

    // We look for patterns like <span>log&#32; or <span>Runtime.log&#32;
    // or without trailing space for property-like access.
    
    let patterns = [
        $"<span>{encodedShortName}&#32;", $"<span>{encodedUsageName}&#32;"
        $"<span>{encodedShortName}</span>", $"<span>{encodedUsageName}</span>"
    ]

    let mutable result = html
    let mutable replaced = false

    for pat, rep in patterns do
        if not replaced && result.Contains pat then
            result <- result.Replace(pat, rep)
            replaced <- true
    
    if not replaced && parts.Length > 1 then
        let midName = parts.[parts.Length - 2] + "." + shortName
        let encodedMidName = WebUtility.HtmlEncode midName
        let midPatterns = [
            $"<span>{encodedMidName}&#32;", $"<span>{encodedUsageName}&#32;"
            $"<span>{encodedMidName}</span>", $"<span>{encodedUsageName}</span>"
        ]
        for pat, rep in midPatterns do
            if not replaced && result.Contains pat then
                result <- result.Replace(pat, rep)
                replaced <- true
                
    result

let platformLabel (qualifiedName: string) =
    let netOnly =
        [ ".ToTask"; ".ToValueTask"; ".RunSynchronously"; ".fromTask"; ".fromValueTask" ]

    let fableCompatible =
        [ ".ToAsync"; ".fromAsync" ]

    if netOnly |> List.exists qualifiedName.EndsWith then
        Some ".NET only"
    elif fableCompatible |> List.exists qualifiedName.EndsWith then
        Some "Fable compatible"
    else
        None

let renderMemberPage (rewriteHtml: string -> string) (weight: int) (m: ApiDocMember) =
    let fullName = logicalName m.Symbol
    let qualifiedName = cleanName fullName |> dedupeAdjacentSegments
    let shortName = cleanName m.Name
    
    // Better link title for CEs
    let linkTitle = 
        if m.Name = "flow" then "flow { }"
        elif m.Name = "validate" then "validate { }"
        elif m.Name = "result" then "result { }"
        elif m.Name = "stm" then "stm { }"
        else shortName

    let mutable content = 
        $"---\ntitle: \"{qualifiedName}\"\nlinkTitle: \"{linkTitle}\"\nweight: {weight}\n---\n\n"
    
    // Description
    content <- content + rewriteHtml m.Comment.Summary.HtmlText + "\n\n"

    match platformLabel qualifiedName with
    | Some label ->
        content <- content + $"**Platform:** {label}\n\n"
    | None -> ()

    // Signature
    let qualifier = memberQualifier m
    let usageName = if String.IsNullOrEmpty qualifier then m.Name else qualifier + "." + m.Name
    let usageHtml =
        m.UsageHtml.HtmlText
        |> qualifyUsageHtml usageName
        |> rewriteHtml

    content <- content + "## Signature\n\n"
    content <- content + "<div class=\"fsdocs-usage\">\n" + usageHtml + "\n</div>\n\n"

    if not m.Parameters.IsEmpty then
        content <- content + "## Parameters\n\n"
        content <- content + "| Name | Type | Description |\n"
        content <- content + "| --- | --- | --- |\n"
        for p in m.Parameters do
            let docs =
                match p.ParameterDocs with
                | Some html -> rewriteHtml html.HtmlText
                | None -> ""

            content <- content + $"| `{p.ParameterNameText}` | {rewriteHtml p.ParameterType.HtmlText} | {docs} |\n"
        content <- content + "\n"

    content <- content + "## Returns\n\n"
    content <- content + "| Type | Description |\n"
    content <- content + "| --- | --- |\n"
    let returnDocs =
        match m.ReturnInfo.ReturnDocs with
        | Some html -> rewriteHtml html.HtmlText
        | None -> ""

    let returnType =
        match m.ReturnInfo.ReturnType with
        | Some (_, html) -> rewriteHtml html.HtmlText
        | None -> "<code>unit</code>"

    content <- content + $"| {returnType} | {returnDocs} |\n\n"

    match m.Comment.Remarks with
    | Some r -> content <- content + "## Remarks\n\n" + rewriteHtml r.HtmlText + "\n\n"
    | None -> ()

    if not m.Comment.Examples.IsEmpty then
        content <- content + "## Examples\n\n"
        for e in m.Comment.Examples do
            content <- content + rewriteHtml e.HtmlText + "\n\n"

    match m.SourceLocation with
    | Some url -> content <- content + $"\n[Source]({url})\n\n"
    | None -> ()

    content

let renderEntityPage (rewriteHtml: string -> string) (weight: int) (e: ApiDocEntity) =
    let fullName = safeFullName e.Symbol
    let qualifiedName = cleanName fullName |> dedupeAdjacentSegments
    let shortName = cleanName e.Name
    
    let mutable content = 
        $"---\ntitle: \"{qualifiedName}\"\nlinkTitle: \"{shortName}\"\nweight: {weight}\n---\n\n"
    
    // Construct signature
    let signature = 
        match e.Symbol with
        | :? FSharp.Compiler.Symbols.FSharpEntity as ent ->
            let generics = 
                if ent.GenericParameters.Count > 0 then
                    "<" + (ent.GenericParameters |> Seq.map (fun p -> "'" + p.DisplayName) |> String.concat ", ") + ">"
                else ""
            $"type {ent.DisplayName}{generics}"
        | _ -> $"type {shortName}"

    content <- content + rewriteHtml e.Comment.Summary.HtmlText + "\n\n"

    content <- content + "## Signature\n\n"
    content <- content + "<div class=\"fsdocs-usage\">\n" + $"<code>{signature}</code>" + "\n</div>\n\n"
    
    match e.Symbol with
    | :? FSharp.Compiler.Symbols.FSharpEntity as ent ->
        if ent.GenericParameters.Count > 0 then
            content <- content + "## Type Parameters\n\n"
            content <- content + "| Name |\n"
            content <- content + "| --- |\n"
            for tp in ent.GenericParameters do
                content <- content + $"| `{tp.DisplayName}` |\n"
            content <- content + "\n"

        if e.UnionCases.Length > 0 then
            content <- content + "## Union Cases\n\n"
            content <- content + "| Case | Description |\n"
            content <- content + "| --- | --- |\n"
            for c in e.UnionCases do
                let summary = rewriteHtml c.Comment.Summary.HtmlText
                content <- content + $"| `{c.Name}` | {summary} |\n"
            content <- content + "\n"

        if e.RecordFields.Length > 0 then
            content <- content + "## Record Fields\n\n"
            content <- content + "| Field | Description |\n"
            content <- content + "| --- | --- |\n"
            for f in e.RecordFields do
                let summary = rewriteHtml f.Comment.Summary.HtmlText
                content <- content + $"| `{f.Name}` | {summary} |\n"
            content <- content + "\n"
    | _ -> ()

    match e.Comment.Remarks with
    | Some r -> content <- content + "## Remarks\n\n" + rewriteHtml r.HtmlText + "\n\n"
    | None -> ()

    if not e.Comment.Examples.IsEmpty then
        content <- content + "## Examples\n\n"
        for ex in e.Comment.Examples do
            content <- content + rewriteHtml ex.HtmlText + "\n\n"

    match e.SourceLocation with
    | Some url -> content <- content + $"\n[Source]({url})\n\n"
    | None -> ()

    content

let pageSpecs = [
    {
        OutPath = ["schema"; "_index.md"]
        Title = "Schema"
        Description = "Source-documented universal schema definitions for Reified."
        Intro = "This page shows `Schema<'value>`, the universal catalog for primitive, collection, optional, union, refined, and record declarations. The same declaration can be parsed, checked, inspected, encoded, documented, and used for generation."
        SymbolIds = [
            "Core types", ["T:Reified.Schema.Schema`1"; "T:Reified.Schema.Field`2"; "T:Reified.Schema.UnionCase`1"]
            "Catalog", ["P:Reified.Schema.Schema.text"; "P:Reified.Schema.Schema.int"; "P:Reified.Schema.Schema.decimal"; "P:Reified.Schema.Schema.bool"; "P:Reified.Schema.Schema.dateTime"; "P:Reified.Schema.Schema.guid"; "M:Reified.Schema.Schema.list"; "M:Reified.Schema.Schema.option"; "M:Reified.Schema.Schema.constrain"; "M:Reified.Schema.Schema.constrainAll"; "M:Reified.Schema.Schema.mustSupply"; "M:Reified.Schema.Schema.mayOmit"; "M:Reified.Schema.Schema.refine"; "M:Reified.Schema.Schema.validate"; "M:Reified.Schema.Schema.union"; "M:Reified.Schema.UnionCase.create"; "T:Reified.Schema.Supply"]
            "Record builder", ["P:Reified.SchemaSyntax.schema"; "M:Reified.SchemaSyntax.field"; "M:Reified.SchemaSyntax.construct"; "M:Reified.SchemaSyntax.constructResult"]
            "Inspection", ["T:Reified.Schema.SchemaShape"; "T:Reified.Schema.SchemaDescription"; "T:Reified.Schema.FieldDescription"; "T:Reified.Schema.ModelDescription"; "T:Reified.Schema.UnionDescription"; "T:Reified.Schema.UnionCaseDescription"; "M:Reified.Schema.Inspect.model"; "M:Reified.Schema.Inspect.schema"; "M:Reified.Schema.Inspect.field"]
            "JSON Schema generation", ["M:Reified.Schema.JsonSchema.generate"; "M:Reified.Schema.JsonSchema.generateValue"]
            "Schema derivation attributes (read by schemagen at generation time)", ["T:Reified.DerivedSchema.DeriveSchemaAttribute"; "T:Reified.DerivedSchema.SchemaNameAttribute"; "T:Reified.DerivedSchema.DeriveUnionAttribute"; "T:Reified.DerivedSchema.SchemaConstructorAttribute"; "T:Reified.DerivedSchema.PatternAttribute"; "T:Reified.DerivedSchema.MinAttribute"; "T:Reified.DerivedSchema.MaxAttribute"; "T:Reified.DerivedSchema.LengthAttribute"; "T:Reified.DerivedSchema.LengthBetweenAttribute"; "T:Reified.DerivedSchema.PresentAttribute"; "T:Reified.DerivedSchema.SuppliedAttribute"; "T:Reified.DerivedSchema.FormatAttribute"; "T:Reified.DerivedSchema.AtLeastAttribute"; "T:Reified.DerivedSchema.GreaterThanAttribute"; "T:Reified.DerivedSchema.AtMostAttribute"; "T:Reified.DerivedSchema.LessThanAttribute"; "T:Reified.DerivedSchema.MultipleOfAttribute"; "T:Reified.DerivedSchema.DistinctAttribute"; "T:Reified.DerivedSchema.EmailAttribute"; "T:Reified.DerivedSchema.DefaultAttribute"]
        ]
        Alias = None
    }
    {
        OutPath = ["schema"; "interpreters"; "_index.md"]
        Title = "Schema Interpreters"
        Description = "Source-documented schema input parsing, checking, and refined-value interpreters."
        Intro = "This page shows structured boundary data, universal schema parsing into `Result`, opt-in input retention with `RetainedParseResult`, checking of existing values, and refined schemas. Core schema metadata stays in [Schema](../); interpreters attach path-aware `SchemaErrors` and optional redisplay behavior to it."
        SymbolIds = [
            "Structured data", ["T:Reified.Data"; "T:Reified.DataPathSegment"; "T:Reified.DataPath"; "M:Reified.DataModule.ofMap"; "M:Reified.DataModule.ofNameValues"; "M:Reified.DataModule.ofCliArgs"; "M:Reified.DataModule.ofJsonElement"; "M:Reified.DataModule.ofJsonDocument"; "M:Reified.DataModule.ofConfiguration"; "M:Reified.DataModule.redisplay"; "M:Reified.DataModule.redisplayPath"]
            "Input parsing", ["M:Reified.Schema.Schema.parse"; "M:Reified.Schema.Schema.parseRetainingInput"; "M:Reified.Schema.Schema.parseWith"; "T:Reified.Schema.SchemaParseOptions"; "T:Reified.Schema.RetainedParseResult`1"; "M:Reified.Schema.RetainedParseResultModule.create"; "M:Reified.Schema.RetainedParseResultModule.renderErrors"]
            "Errors", ["T:Reified.Schema.SchemaError"; "T:Reified.Schema.SchemaPath"; "M:Reified.Schema.SchemaPath.root"; "M:Reified.Schema.SchemaPath.key"; "M:Reified.Schema.SchemaPath.index"; "M:Reified.Schema.SchemaPath.append"; "M:Reified.Schema.SchemaPath.format"; "M:Reified.Schema.SchemaPath.fold"; "T:Reified.Schema.SchemaIssue"; "T:Reified.Schema.SchemaErrors"; "M:Reified.Schema.SchemaErrors.toList"; "M:Reified.Schema.SchemaErrors.count"; "M:Reified.Schema.SchemaErrors.isEmpty"; "M:Reified.Schema.SchemaErrors.toString"; "M:Reified.Schema.SchemaErrors.messages"; "M:Reified.Schema.SchemaErrors.fullMessages"; "M:Reified.Schema.SchemaErrors.toStringWith"; "T:Reified.Schema.SchemaMessages"; "P:Reified.Schema.SchemaMessages.keys"; "P:Reified.Schema.SchemaMessages.arguments"; "P:Reified.Schema.SchemaMessages.english"]
            "Refined catalog schemas", ["P:Reified.Schema.RefinedSchemas.nonBlankString"; "P:Reified.Schema.RefinedSchemas.finiteFloat"; "P:Reified.Schema.RefinedSchemas.unitInterval"; "M:Reified.Schema.RefinedSchemas.nonEmptyList"; "M:Reified.Schema.RefinedSchemas.nonEmptyArray"; "M:Reified.Schema.RefinedSchemas.distinctList"; "M:Reified.Schema.RefinedSchemas.interval"; "P:Reified.Schema.RefinedSchemas.dateRange"; "M:Reified.Schema.RefinedSchemas.bounded"]
            "Existing values", ["M:Reified.Schema.Schema.check"]
        ]
        Alias = None
    }
    {
        OutPath = ["data"; "_index.md"]
        Title = "Data"
        Description = "Source-documented portable structured values."
        Intro = "This page shows `Reified.Data`: one owned tree for literals, source adapters, immutable edits, named cases, exact differences, and produced-data proofs. It has no dependencies on other Reified packages."
        SymbolIds = [
            "The tree", ["T:Reified.Data"; "T:Reified.DataPathSegment"; "T:Reified.DataPath"]
            "Constructors", ["M:Reified.DataModule.ofMap"; "M:Reified.DataModule.ofNameValues"; "M:Reified.DataModule.ofCliArgs"; "M:Reified.DataModule.ofJsonElement"; "M:Reified.DataModule.ofJsonDocument"; "M:Reified.DataModule.ofConfiguration"]
            "Literal syntax", ["T:Reified.DataField"; "M:Reified.DataModule.assoc"; "M:Reified.DataModule.optionalAssoc"; "M:Reified.DataModule.data"; "M:Reified.DataModule.number"; "M:Reified.DataModule.fields"; "M:Reified.DataModule.Syntax.data"; "M:Reified.DataModule.Syntax.op_EqualsGreater"; "M:Reified.DataModule.Syntax.op_QmarkEqualsGreater"; "P:Reified.DataModule.Syntax.nil"; "M:Reified.DataModule.Syntax.num"; "M:Reified.DataModule.Syntax.fields"]
            "Edits", ["T:Reified.DataEdit"; "T:Reified.DataPatchFailure"; "T:Reified.DataPatchException"; "M:Reified.DataEditModule.set"; "M:Reified.DataEditModule.replace"; "M:Reified.DataEditModule.remove"; "M:Reified.DataEditModule.append"; "M:Reified.DataEditModule.prepend"; "M:Reified.DataEditModule.insert"; "M:Reified.DataEditModule.rename"; "M:Reified.DataEditModule.update"; "M:Reified.DataModule.applyEdit"; "M:Reified.DataModule.patch"; "M:Reified.DataModule.set"; "M:Reified.DataModule.replace"; "M:Reified.DataModule.remove"; "M:Reified.DataModule.append"; "M:Reified.DataModule.prepend"; "M:Reified.DataModule.insert"; "M:Reified.DataModule.rename"; "M:Reified.DataModule.update"; "M:Reified.DataModule.Syntax.set"; "M:Reified.DataModule.Syntax.replace"; "M:Reified.DataModule.Syntax.remove"; "M:Reified.DataModule.Syntax.append"; "M:Reified.DataModule.Syntax.prepend"; "M:Reified.DataModule.Syntax.insert"; "M:Reified.DataModule.Syntax.rename"; "M:Reified.DataModule.Syntax.update"; "M:Reified.DataModule.tryPatch"]
            "Cases", ["T:Reified.DataVariation"; "T:Reified.DataCase"; "T:Reified.DataDimension"; "M:Reified.DataModule.Syntax.variant"; "M:Reified.DataModule.Syntax.variants"; "M:Reified.DataModule.Syntax.dimension"; "M:Reified.DataModule.Syntax.matrix"]
            "Comparison and matching", ["T:Reified.DataDifference"; "T:Reified.DataDifferenceCause"; "T:Reified.DataPattern"; "T:Reified.DataExpectation"; "T:Reified.DataMismatch"; "T:Reified.DataMatchException"; "M:Reified.DataModule.diff"; "M:Reified.DataModule.compare"; "M:Reified.DataModule.tryMatch"; "M:Reified.DataModule.Syntax.at"; "M:Reified.DataModule.Syntax.absent"; "M:Reified.DataModule.Syntax.matching"; "M:Reified.DataModule.Syntax.exactly"; "M:Reified.DataModule.Syntax.containing"; "M:Reified.DataModule.Syntax.containingItems"; "M:Reified.DataModule.Syntax.inOrder"; "M:Reified.DataModule.Syntax.allItems"; "M:Reified.DataModule.Syntax.someItem"; "P:Reified.DataModule.Syntax.any"; "P:Reified.DataModule.Syntax.anyText"; "P:Reified.DataModule.Syntax.anyNumber"; "M:Reified.DataModule.Syntax.oneOf"; "M:Reified.DataModule.Syntax.satisfying"]
            "JSON rendering", ["P:Reified.DataModule.Json.render"; "P:Reified.DataModule.Json.renderIndented"]
            "Rendering and extraction", ["M:Reified.DataModule.render"; "M:Reified.DataModule.renderIndented"; "M:Reified.DataModule.tryText"; "M:Reified.DataModule.tryBool"; "M:Reified.DataModule.tryNumberToken"; "M:Reified.DataModule.tryList"; "M:Reified.DataModule.tryObject"]
            "Redisplay", ["M:Reified.DataModule.redisplay"; "M:Reified.DataModule.redisplayPath"]
        ]
        Alias = None
    }
    {
        OutPath = ["codec"; "_index.md"]
        Title = "Codec"
        Description = "Source-documented compiled JSON codecs over built model schemas."
        Intro = "This page shows the `Reified.Schema.Json` surface: `Json.parseData` parses JSON into the source-neutral `Data` tree on .NET and Fable, while `Json.compile` turns a built `Schema<'model>` into a reusable `JsonCodec<'model>` with compiler-directed, runtime-reflection-free, constructor-specialized encode and decode plans. The codec is the trusted hot path for typed serialization; parse untrusted boundary input through `Data` and [schema input parsing](../schema/interpreters/) when path-aware diagnostics matter."
        SymbolIds = [
            "Core types", ["T:Reified.Schema.Json.JsonCodec`1"; "T:Reified.Schema.Json.JsonCodecException"]
            "Module functions", ["M:Reified.Schema.Json.Json.parseData"; "M:Reified.Schema.Json.Json.compile"; "M:Reified.Schema.Json.Json.serialize"; "M:Reified.Schema.Json.Json.serializeBytes"; "M:Reified.Schema.Json.Json.serializeToStream"; "M:Reified.Schema.Json.Json.deserialize"; "M:Reified.Schema.Json.Json.deserializeBytes"; "M:Reified.Schema.Json.Json.deserializeStreamAsync"; "M:Reified.Schema.Json.Json.tryDeserialize"]
        ]
        Alias = None
    }
    {
        OutPath = ["schema"; "http"; "_index.md"]
        Title = "Schema HTTP Boundary"
        Description = "Source-documented host-neutral HTTP boundary support for schemas."
        Intro = "This page shows the host-neutral server boundary in `Reified.Schema.Http`: `BoundaryInput` builds structured data from the name/value surfaces HTTP servers hand over, `ProblemDetails` renders failed parses as RFC 9457 bodies with RFC 6901 JSON pointers, and `EndpointSpec` values assemble into OpenAPI 3.1 documents whose schemas are embedded from `JsonSchema.generate` output. Host-specific Flow lowering is documented under [ASP.NET Core](./aspnetcore/) and [GenHTTP](./genhttp/); see the [HTTP servers guide](/schema/http-servers/) for complete usage."
        SymbolIds = [
            "Boundary input", ["M:Reified.Schema.Http.BoundaryInput.ofQuery"; "M:Reified.Schema.Http.BoundaryInput.ofForm"]
            "Problem details", ["T:Reified.Schema.Http.ProblemDetails"; "T:Reified.Schema.Http.ProblemError"; "P:Reified.Schema.Http.ProblemDetailsModule.malformedJson"; "M:Reified.Schema.Http.ProblemDetailsModule.ofParsed"; "M:Reified.Schema.Http.ProblemDetailsModule.ofErrors"; "M:Reified.Schema.Http.ProblemDetailsModule.toJson"; "M:Reified.Schema.Http.ProblemDetailsModule.writeTo"; "M:Reified.Schema.Http.JsonPointer.ofPath"]
            "Endpoint specs", ["T:Reified.Schema.Http.EndpointSpec"; "T:Reified.Schema.Http.ResponseSpec"; "M:Reified.Schema.Http.Endpoint.get"; "M:Reified.Schema.Http.Endpoint.post"; "M:Reified.Schema.Http.Endpoint.put"; "M:Reified.Schema.Http.Endpoint.patch"; "M:Reified.Schema.Http.Endpoint.delete"; "M:Reified.Schema.Http.Endpoint.summary"; "M:Reified.Schema.Http.Endpoint.operationId"; "M:Reified.Schema.Http.Endpoint.tag"; "M:Reified.Schema.Http.Endpoint.accepts"; "M:Reified.Schema.Http.Endpoint.returnsJson"; "M:Reified.Schema.Http.Endpoint.returns"; "M:Reified.Schema.Http.Endpoint.returnsProblemDetails"]
            "OpenAPI assembly", ["T:Reified.Schema.Http.OpenApiInfo"; "M:Reified.Schema.Http.OpenApi.info"; "M:Reified.Schema.Http.OpenApi.document"; "M:Reified.Schema.Http.OpenApi.writeTo"]
        ]
        Alias = None
    }
    {
        OutPath = ["constraint"; "_index.md"]
        Title = "Constraint"
        Description = "Source-documented reusable value rules for Reified."
        Intro = "This page shows `Constraint<'value>`: one reusable description of valid values, shared by direct checking, refined-value admission, Schema, and export. `check` runs it, `test` answers the same question as a `bool`, and `guard` keeps the input after success. There is no separate Check type and no second constructor catalogue. Interpreted constructors build one `ConstraintAtom` that drives both execution and description; `custom`, `customWith`, `notWith`, and `contramap` are the opaque escape hatch, which runs normally and is honestly invisible to export and proof."
        SymbolIds = [
            "Core types", ["T:Reified.Constraint.Constraint`1"; "T:Reified.Constraint.Violation"; "T:Reified.Constraint.AtomicViolation"; "T:Reified.Constraint.ConstraintDescription"; "T:Reified.Constraint.ConstraintExpression"; "T:Reified.Constraint.ConstraintAtom"; "T:Reified.Constraint.OpaqueConstraint"; "T:Reified.Constraint.ConstraintValue"]
            "Expectations", ["T:Reified.Constraint.Presence"; "T:Reified.Constraint.Cardinality"; "T:Reified.Constraint.RelationOperator"; "T:Reified.Constraint.Relation"; "T:Reified.Constraint.Membership"; "T:Reified.Constraint.Format"; "T:Reified.Constraint.Number"; "T:Reified.Constraint.UnsupportedOperation"]
            "Execution", ["M:Reified.Constraint.ConstraintModule.test"; "M:Reified.Constraint.ConstraintModule.check"; "M:Reified.Constraint.ConstraintModule.guard"; "M:Reified.Constraint.ConstraintModule.inspect"]
            "Composition", ["M:Reified.Constraint.ConstraintModule.all"; "M:Reified.Constraint.ConstraintModule.any"; "M:Reified.Constraint.ConstraintModule.optional"; "M:Reified.Constraint.ConstraintModule.notWith"; "M:Reified.Constraint.ConstraintModule.custom"; "M:Reified.Constraint.ConstraintModule.customLocalized"; "M:Reified.Constraint.ConstraintModule.customLocalizedWith"; "M:Reified.Constraint.ConstraintModule.customWith"; "M:Reified.Constraint.ConstraintModule.contramap"; "M:Reified.Constraint.ConstraintModule.describe"]
            "Presence and size", ["M:Reified.Constraint.ConstraintModule.present"; "M:Reified.Constraint.ConstraintModule.blank"; "M:Reified.Constraint.ConstraintModule.length"; "M:Reified.Constraint.ConstraintModule.minLength"; "M:Reified.Constraint.ConstraintModule.maxLength"; "M:Reified.Constraint.ConstraintModule.lengthBetween"]
            "Text formats", ["M:Reified.Constraint.ConstraintModule.email"; "M:Reified.Constraint.ConstraintModule.trimmed"; "M:Reified.Constraint.ConstraintModule.numeric"; "M:Reified.Constraint.ConstraintModule.alphanumeric"; "M:Reified.Constraint.ConstraintModule.pattern"]
            "Relations and membership", ["M:Reified.Constraint.ConstraintModule.equalTo"; "M:Reified.Constraint.ConstraintModule.notEqualTo"; "M:Reified.Constraint.ConstraintModule.greaterThan"; "M:Reified.Constraint.ConstraintModule.lessThan"; "M:Reified.Constraint.ConstraintModule.atLeast"; "M:Reified.Constraint.ConstraintModule.atMost"; "M:Reified.Constraint.ConstraintModule.between"; "M:Reified.Constraint.ConstraintModule.oneOf"; "M:Reified.Constraint.ConstraintModule.contains"; "M:Reified.Constraint.ConstraintModule.distinct"]
            "Numeric properties", ["M:Reified.Constraint.ConstraintModule.multipleOf"; "M:Reified.Constraint.ConstraintModule.finite"; "M:Reified.Constraint.ConstraintModule.finite32"]
            "Messages", ["T:Reified.Constraint.MessageTree"; "T:Reified.Constraint.MessageLeaf"; "T:Reified.Constraint.MessageDescriptor"; "T:Reified.Constraint.MessageFormatSpec"; "T:Reified.Constraint.MessageKeyError"; "T:Reified.Constraint.MessageFormatSpecError"; "M:Reified.Constraint.MessageDescriptorModule.key"; "M:Reified.Constraint.MessageDescriptorModule.arguments"; "M:Reified.Constraint.MessageDescriptorModule.segments"; "M:Reified.Constraint.MessageDescriptorModule.Advanced.create"; "M:Reified.Constraint.MessageDescriptorModule.Advanced.tryCreate"; "M:Reified.Constraint.MessageDescriptorModule.Advanced.ofSegments"; "M:Reified.Constraint.MessageFormatSpecModule.descriptor"; "M:Reified.Constraint.MessageFormatSpecModule.fallback"; "M:Reified.Constraint.MessageFormatSpecModule.pluralArgument"; "M:Reified.Constraint.MessageFormatSpecModule.Advanced.create"; "M:Reified.Constraint.MessageFormatSpecModule.Advanced.tryCreate"]
            "Rendering", ["T:Reified.Constraint.Renderer"; "T:Reified.Constraint.MessageLookup"; "T:Reified.Constraint.MessageRequest"; "T:Reified.Constraint.MessageResolution"; "T:Reified.Constraint.MessageResolver"; "T:Reified.Constraint.ValueFormatRequest"; "P:Reified.Constraint.RendererModule.english"; "M:Reified.Constraint.RendererModule.ofLookup"; "M:Reified.Constraint.RendererModule.ofResourceManager"; "M:Reified.Constraint.RendererModule.ofResourceManagerWithCultures"; "M:Reified.Constraint.RendererModule.ofCurrentCulture"; "M:Reified.Constraint.RendererModule.context"; "M:Reified.Constraint.RendererModule.attribute"; "M:Reified.Constraint.RendererModule.unscoped"; "M:Reified.Constraint.RendererModule.withValues"; "M:Reified.Constraint.RendererModule.attributeName"; "M:Reified.Constraint.RendererModule.fullMessage"; "M:Reified.Constraint.RendererModule.Advanced.ofResolver"; "M:Reified.Constraint.RendererModule.Advanced.withValueFormatting"; "M:Reified.Constraint.RendererModule.Advanced.attributePath"; "M:Reified.Constraint.RendererModule.Advanced.lookupCandidates"; "M:Reified.Constraint.RendererModule.Advanced.messageRequests"; "M:Reified.Constraint.RendererModule.Advanced.attributeCandidates"; "M:Reified.Constraint.RendererModule.Advanced.format"]
            "Catalogue", ["P:Reified.Constraint.Catalogue.keys"; "P:Reified.Constraint.Catalogue.arguments"; "P:Reified.Constraint.Catalogue.english"; "P:Reified.Constraint.Catalogue.pluralArgument"]
            "Violations", ["M:Reified.Constraint.ViolationModule.render"; "M:Reified.Constraint.ViolationModule.message"; "M:Reified.Constraint.ViolationModule.fullMessage"; "M:Reified.Constraint.ViolationModule.renderWith"; "M:Reified.Constraint.ViolationModule.toMessageTree"; "M:Reified.Constraint.ViolationModule.children"; "M:Reified.Constraint.ViolationModule.flatten"; "M:Reified.Constraint.ViolationModule.tryExpectation"; "M:Reified.Constraint.ViolationModule.tryActual"; "M:Reified.Constraint.ViolationModule.tryDescription"; "M:Reified.Constraint.ViolationModule.conjoin"; "M:Reified.Constraint.ViolationModule.alternatives"]
            "Descriptions and values", ["M:Reified.Constraint.ConstraintDescriptionModule.children"; "M:Reified.Constraint.ConstraintDescriptionModule.atoms"; "M:Reified.Constraint.ConstraintDescriptionModule.isOpaque"; "M:Reified.Constraint.ConstraintAtomModule.key"; "M:Reified.Constraint.ConstraintAtomModule.render"; "M:Reified.Constraint.ConstraintAtomModule.arguments"; "M:Reified.Constraint.ConstraintValueModule.tryCreate"; "M:Reified.Constraint.ConstraintValueModule.render"]
        ]
        Alias = None
    }
    {
        OutPath = ["result"; "_index.md"]
        Title = "Result"
        Description = "Source-documented fail-fast Result helpers for Reified."
        Intro = "This page shows `Reified.Result`: helpers over the standard F# `Result<'value, 'error>` type. Use `Result.requireTrue` when a bare `bool` condition should become a `Result` (nothing to preserve). Use `Result.okIf`/`Result.failIf` (mirroring `Option.filter`) when a predicate over the value itself should keep that value on success, then attach the real error afterward with `Result.orError`. Extraction helpers such as `Result.someOr` change the success shape. The `result { }` builder sequences fail-fast steps; `result.list { }` and `result.array { }` accumulate independent failures through `and!`. The package is a standalone leaf: for reusable value rules and the structured `Violation` they produce, see the Values reference."
        SymbolIds = [
            "Core helpers", ["M:Reified.Result.Result.ok"; "M:Reified.Result.Result.error"; "M:Reified.Result.Result.map"; "M:Reified.Result.Result.mapError"; "M:Reified.Result.Result.bind"; "M:Reified.Result.Result.orElse"; "M:Reified.Result.Result.orElseWith"]
            "Lifts and conversions", ["M:Reified.Result.Result.requireTrue"; "M:Reified.Result.Result.okIf"; "M:Reified.Result.Result.failIf"; "M:Reified.Result.Result.orError"; "M:Reified.Result.Result.fromTry"; "M:Reified.Result.Result.fromChoice"; "M:Reified.Result.Result.toOption"; "M:Reified.Result.Result.toValueOption"; "M:Reified.Result.Result.defaultValue"]
            "Extraction helpers", ["M:Reified.Result.Result.someOr"; "M:Reified.Result.Result.noneOr"; "M:Reified.Result.Result.valueSomeOr"; "M:Reified.Result.Result.valueNoneOr"; "M:Reified.Result.Result.nullableOr"; "M:Reified.Result.Result.notNullOr"; "M:Reified.Result.Result.okOr"; "M:Reified.Result.Result.errorOr"; "M:Reified.Result.Result.headOr"]
            "Traversal", ["M:Reified.Result.Result.traverse"; "M:Reified.Result.Result.sequence"]
            "Side effects", ["M:Reified.Result.Result.tap"; "M:Reified.Result.Result.tapError"]
            "Builder", ["P:Reified.Result.Syntax.result"]
        ]
        Alias = None
    }
    {
        OutPath = ["parse"; "_index.md"]
        Title = "Parse"
        Description = "Source-documented serialized primitive parsers for Reified."
        Intro = "`Reified.Parse` decodes serialized strings into primitive F# values. Every named parser returns `Result<'value, ParseError>` and remains independent of Constraint, Refined, Result, and Schema."
        SymbolIds = [
            "Error", ["T:Reified.Parse.ParseError"]
            "Functions", ["M:Reified.Parse.Parse.int"; "M:Reified.Parse.Parse.long"; "M:Reified.Parse.Parse.decimal"; "M:Reified.Parse.Parse.float"; "M:Reified.Parse.Parse.bool"; "M:Reified.Parse.Parse.guid"; "M:Reified.Parse.Parse.dateTime"; "M:Reified.Parse.Parse.dateTimeOffset"; "M:Reified.Parse.Parse.dateOnly"; "M:Reified.Parse.Parse.timeOnly"; "M:Reified.Parse.Parse.enum"; "M:Reified.Parse.Parse.optional"; "M:Reified.Parse.Parse.optionalOr"; "M:Reified.Parse.Parse.intOption"; "M:Reified.Parse.Parse.boolOption"; "M:Reified.Parse.Parse.decimalOption"; "M:Reified.Parse.Parse.guidOption"; "M:Reified.Parse.Parse.intOrDefault"; "M:Reified.Parse.Parse.boolOrDefault"; "M:Reified.Parse.Parse.decimalOrDefault"]
        ]
        Alias = None
    }
    {
        OutPath = ["refined"; "_index.md"]
        Title = "Refined"
        Description = "Source-documented invariant-carrying values and refinements for Reified."
        Intro = "`Reified.Refinements` supplies invariant-carrying values and the operations that justify them. A type earns its place by making a partial operation total, guaranteeing a property later operations rely on, or removing a branch from consumers — validation that carries no invariant past the boundary belongs in `Constraint` instead. `Refinement` couples checking, total construction, and a total reverse projection."
        SymbolIds = [
            "Refined types", [
                "T:Reified.Refinements.NonBlankString"
                
                
                "T:Reified.Refinements.FiniteFloat"; "T:Reified.Refinements.FiniteFloat32"; "T:Reified.Refinements.UnitInterval"
                "T:Reified.Refinements.NonEmptyList`1"; "T:Reified.Refinements.NonEmptyArray`1"; "T:Reified.Refinements.DistinctList`1"
                "T:Reified.Refinements.Interval`1"; "T:Reified.Refinements.Bounded`1"
            ]
            "Text", ["M:Reified.Refinements.Refine.Text.nonBlankString"]
            "Collection", ["M:Reified.Refinements.Refine.Collection.nonEmptyList"; "M:Reified.Refinements.Refine.Collection.nonEmptyArray"; "M:Reified.Refinements.Refine.Collection.distinctList"]
            "Interval", ["M:Reified.Refinements.Interval.between"; "M:Reified.Refinements.Interval.create"; "M:Reified.Refinements.Interval.lower"; "M:Reified.Refinements.Interval.upper"; "M:Reified.Refinements.Interval.duration"; "M:Reified.Refinements.Interval.widthInt"; "M:Reified.Refinements.Interval.widthDecimal"; "M:Reified.Refinements.Interval.singleton"; "M:Reified.Refinements.Interval.contains"; "M:Reified.Refinements.Interval.intersect"; "M:Reified.Refinements.Interval.overlaps"; "M:Reified.Refinements.Interval.clamp"; "M:Reified.Refinements.Interval.span"]
            "Character", ["M:Reified.Refinements.Refine.Character.isAsciiDigit"; "M:Reified.Refinements.Refine.Character.isAsciiHexDigit"; "M:Reified.Refinements.Refine.Character.isLowercase"; "M:Reified.Refinements.Refine.Character.isUppercase"; "M:Reified.Refinements.Refine.Character.isWhitespace"; "M:Reified.Refinements.Refine.Character.isControl"; "M:Reified.Refinements.Refine.Character.isNumeric"]
            "Choice", ["M:Reified.Refinements.Refine.Choice.orElse"; "M:Reified.Refinements.Refine.Choice.tryAny"]
            "Refinement", ["T:Reified.Refinements.Refinement`2"; "M:Reified.Refinements.Refinement.define"; "M:Reified.Refinements.Refinement.create"; "M:Reified.Refinements.Refinement.underlying"; "M:Reified.Refinements.Refinement.constraint'"]
            "Invariant-preserving operations", ["M:Reified.Refinements.NonBlankString.value"; "M:Reified.Refinements.NonBlankString.create"; "M:Reified.Refinements.NonBlankString.append"; "M:Reified.Refinements.NonBlankString.trim"; "M:Reified.Refinements.NonBlankString.split"; "M:Reified.Refinements.NonEmptyList.toList"; "M:Reified.Refinements.NonEmptyList.create"; "M:Reified.Refinements.NonEmptyList.cons"; "M:Reified.Refinements.NonEmptyList.map"; "M:Reified.Refinements.NonEmptyList.head"; "M:Reified.Refinements.NonEmptyList.last"; "M:Reified.Refinements.NonEmptyList.reduce"; "M:Reified.Refinements.NonEmptyList.traverseResult"; "M:Reified.Refinements.NonEmptyList.groupBy"; "M:Reified.Refinements.NonEmptyList.chunkBySize"; "M:Reified.Refinements.NonEmptyList.zip"; "M:Reified.Refinements.NonEmptyList.filter"; "M:Reified.Refinements.NonEmptyList.tryFilter"; "M:Reified.Refinements.NonEmptyList.sumBy"; "M:Reified.Refinements.NonEmptyList.average"; "M:Reified.Refinements.NonEmptyList.scan"; "M:Reified.Refinements.NonEmptyList.truncate"; "M:Reified.Refinements.NonEmptyList.item"; "M:Reified.Refinements.DistinctList.toMap"; "M:Reified.Refinements.DistinctList.toSet"; "M:Reified.Refinements.UnitInterval.multiply"; "M:Reified.Refinements.UnitInterval.complement"; "M:Reified.Refinements.UnitInterval.lerp"; "M:Reified.Refinements.UnitInterval.inverseLerp"; "M:Reified.Refinements.FiniteFloat.create"; "M:Reified.Refinements.FiniteFloat.negate"; "M:Reified.Refinements.FiniteFloat.average"; "M:Reified.Refinements.Bounded.clamp"]
            "Refine facade", ["M:Reified.Refinements.Refine.nonBlankString"; "M:Reified.Refinements.Refine.finiteFloat"; "M:Reified.Refinements.Refine.unitInterval"; "M:Reified.Refinements.Refine.interval"; "M:Reified.Refinements.Refine.nonEmptyList"; "M:Reified.Refinements.Refine.nonEmptyArray"; "M:Reified.Refinements.Refine.distinctList"]

        ]
        Alias = None
    }
]

let sectionDirectory (spec: PageSpec) (sectionTitle: string) (id: string) =
    match spec.OutPath, sectionTitle with
    | ["result"; "_index.md"], "Builder" -> Some "result-ce"
    | ["result"; "_index.md"], _ -> Some "result"
    | ["parse"; "_index.md"], _ -> None
    | ["refined"; "_index.md"], "Refined types" -> Some "types"
    | ["refined"; "_index.md"], "Text" -> Some "text"
    | ["refined"; "_index.md"], "Collection" -> Some "collection"
    | ["refined"; "_index.md"], "Temporal" -> Some "temporal"
    | ["refined"; "_index.md"], "Character" -> Some "character"
    | ["refined"; "_index.md"], "Choice" -> Some "choice"
    | ["refined"; "_index.md"], "Re-certifying helpers" when id.Contains(".NonBlankString.") -> Some "non-blank-string"
    | ["refined"; "_index.md"], "Re-certifying helpers" when id.Contains(".NonEmptyList.") -> Some "non-empty-list"
    | ["refined"; "_index.md"], "Refine facade" -> Some "refine"
    | ["refined"; "_index.md"], "Builder" -> Some "refine-ce"
    | _ -> None

let sectionTitleForDirectory = function
    | "result" -> "Result"
    | "result-ce" -> "Result CE"
    | "types" -> "Types"
    | "parse" -> "Parse"
    | "text" -> "Text"
    | "collection" -> "Collection"
    | "temporal" -> "Temporal"
    | "character" -> "Character"
    | "choice" -> "Choice"
    | "non-blank-string" -> "NonBlankString"
    | "non-empty-list" -> "NonEmptyList"
    | "refine" -> "Refine"
    | "refine-ce" -> "Refine CE"
    | other -> other

let sectionIntroForDirectory = function
    | "result" -> "This page shows the helpers on the `Result` module."
    | "result-ce" -> "This page shows the `result { }` computation expression."
    | "types" -> "Errors and refined value types defined by `Reified.Refinements`."
    | "parse" -> "`Parse` functions convert serialized strings into primitive values."
    | "text" -> "`Text` functions construct refined string values."
    | "collection" -> "Functions in this section operate on collections."
    | "temporal" -> "`Temporal` functions construct refined date and time values."
    | "character" -> "`Character` functions test individual characters."
    | "choice" -> "`Choice` functions try alternative refinement functions."
    | "non-blank-string" -> "`NonBlankString` functions construct, inspect, and transform non-blank strings."
    | "non-empty-list" -> "`NonEmptyList` functions construct, inspect, and transform non-empty lists."
    | "refine" -> "`Refine` contains type-directed construction and the common built-in refinement functions."
    | "refine-ce" -> ""
    | _ -> "This page shows the members in this reference subgroup."

let finalSegment (name: string) =
    let parts = name.Split('.')
    parts[parts.Length - 1]

let candidateNamesForMember (m: ApiDocMember) =
    let qualifier = memberQualifier m
    let rawNames =
        match m.Symbol with
        | :? FSharpMemberOrFunctionOrValue as mfv ->
            [
                mfv.DisplayName
                mfv.CompiledName
                if String.IsNullOrEmpty qualifier then mfv.DisplayName else qualifier + "." + mfv.DisplayName
                if String.IsNullOrEmpty qualifier then mfv.CompiledName else qualifier + "." + mfv.CompiledName
            ]
        | _ -> []

    [
        cleanName (logicalName m.Symbol)
        cleanName (safeFullName m.Symbol)
        if String.IsNullOrEmpty qualifier then cleanName m.Name else cleanName (qualifier + "." + m.Name)
        cleanName m.Name
        yield!
            rawNames
            |> List.map cleanName
    ]
    |> List.distinct

let candidateNamesForEntity (e: ApiDocEntity) =
    [ cleanName (safeFullName e.Symbol); cleanName e.Name ]
    |> List.distinct

let matchScore (idNorm: string) (candidate: string) =
    if String.IsNullOrEmpty candidate then 0
    elif candidate = idNorm then 1000
    elif candidate.EndsWith("." + idNorm, StringComparison.Ordinal) then 850
    elif idNorm.EndsWith("." + candidate, StringComparison.Ordinal) then
        if finalSegment candidate = finalSegment idNorm then 400 else 150
    elif finalSegment candidate = finalSegment idNorm then 75
    else 0

type ResolvedSymbol =
    | ResolvedMember of ApiDocMember
    | ResolvedEntity of ApiDocEntity

let findBestSymbol (allEntities: ApiDocEntity list) (id: string) =
    let rawId = id.Substring(2).Split('(').[0]
    let idNorm = cleanName rawId

    let candidates =
        seq {
            for e in allEntities do
                let entityScore =
                    if safeFullName e.Symbol = rawId || logicalName e.Symbol = rawId then
                        5000
                    else
                        candidateNamesForEntity e
                        |> List.map (matchScore idNorm)
                        |> List.max

                if id[0] = 'T' && entityScore > 0 then
                    yield entityScore, ResolvedEntity e

                for m in e.AllMembers do
                    let memberScore =
                        if safeFullName m.Symbol = rawId || logicalName m.Symbol = rawId then
                            5000
                        else
                            candidateNamesForMember m
                            |> List.map (matchScore idNorm)
                            |> List.max

                    if memberScore > 0 then
                        yield memberScore, ResolvedMember m
        }
        |> Seq.sortByDescending fst
        |> Seq.toList

    candidates
    |> List.tryHead
    |> Option.map snd

let relativeLinkFrom (fromFile: string) (toFile: string) =
    Path.GetRelativePath(Path.GetDirectoryName(fromFile), toFile).Replace("\\", "/")

// Slugs for public types that intentionally have no standalone reference page (compiler-plumbing
// CE step types marked [<EditorBrowsable(EditorBrowsableState.Never)>] / <exclude />). Links to
// these slugs are unwrapped to plain text instead of being left as broken hrefs.
let noLinkGeneratedReferenceSlugs =
    set [
        "reified-schema-fieldbuilder-2"          // FieldBuilder<'model, 'value> (<exclude/>, EditorBrowsable.Never)
        "reified-schema-constructorstep-2"       // ConstructorStep<'model, 'constructor> (EditorBrowsable.Never)
        "reified-schema-checkedconstructorstep-2" // CheckedConstructorStep<'model, 'constructor> (EditorBrowsable.Never)
        "reified-schema-schemabuilder-1"         // SchemaBuilder<'model> (<exclude/>, EditorBrowsable.Never)
    ]

let rewriteApiDocHtml (slugMap: IDictionary<string, string>) (filePath: string) (content: string) =
    let unresolved = ResizeArray<string>()

    let linksRewritten =
        Regex.Replace(
            content,
            "<a href=\"(?:https://adz\\.github\\.io/Reified)?/reference/Reified/([a-z0-9\\-]+)\\.html(#[^\"]*)?\">((?:(?!</a>).)*)</a>",
            MatchEvaluator(fun m ->
                let slug = m.Groups[1].Value
                let fragment = m.Groups[2].Value
                let text = m.Groups[3].Value
                match slugMap.TryGetValue slug with
                | true, target ->
                    $"<a href=\"{relativeLinkFrom filePath target}{fragment}\">{text}</a>"
                | _ ->
                    if noLinkGeneratedReferenceSlugs.Contains slug then
                        text
                    else
                        unresolved.Add slug
                        m.Value))

    if unresolved.Count > 0 then
        let unique = unresolved |> Seq.distinct |> String.concat ", "
        printfn "Warning: unresolved generated reference links in %s -> %s" filePath unique

    // FSharp.Formatting checks isolated XML examples without the source file's namespace context. In Reified.Data
    // examples it can therefore bind the short name `Data` to Microsoft.FSharp.Data and emit a false hover tooltip.
    // Keep the copyable source unchanged, but remove tooltip bindings whose generated definition is known to be wrong.
    let incorrectTooltipIds =
        Regex.Matches(
            linksRewritten,
            "<div popover class=\"fsdocs-tip\" id=\"([^\"]+)\">namespace Microsoft\\.FSharp\\.Data</div>")
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Groups[1].Value)
        |> Seq.distinct
        |> Seq.toList

    (linksRewritten, incorrectTooltipIds)
    ||> List.fold (fun html tooltipId ->
        html
        |> fun value ->
            Regex.Replace(
                value,
                $" data-fsdocs-tip=\"{Regex.Escape tooltipId}\" data-fsdocs-tip-unique=\"[^\"]+\"",
                "")
        |> fun value -> value.Replace($"<div popover class=\"fsdocs-tip\" id=\"{tooltipId}\">namespace Microsoft.FSharp.Data</div>", ""))

let rec collectAllEntities (e: ApiDocEntity) =
    seq {
        yield e
        for n in e.NestedEntities do
            yield! collectAllEntities n
    }

let pageWeight (spec: PageSpec) =
    match spec.OutPath with
    | ["result"; "_index.md"] -> 60
    | ["constraint"; "_index.md"] -> 70
    | ["result"; "builders-result.md"] -> 2000
    | _ -> 500

let childPageWeight (id: string) (sectionOrdinal: int) (itemOrdinal: int) =
    let ordinal = sectionOrdinal * 100 + itemOrdinal
    match id.[0] with
    | 'T' -> 1000 + ordinal
    | _ -> 2000 + ordinal

let normalizeGeneratedMarkdown (content: string) =
    content.Replace("\r\n", "\n").Split('\n')
    |> Array.map (fun line -> line.TrimEnd())
    |> String.concat "\n"
    |> fun text -> text.TrimEnd() + "\n"

[<EntryPoint>]
let main argv =
    let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))
    let artifactsDir = Path.Combine(root, "artifacts/bin")

    let product =
        match Environment.GetEnvironmentVariable "REIFIED_DOCS_PRODUCT" with
        | null | "" -> "all"
        | value -> value.Trim().ToLowerInvariant()

    if product <> "all" && product <> "data" && product <> "result" && product <> "values" && product <> "schema" then
        invalidArg "REIFIED_DOCS_PRODUCT" "Expected 'data', 'result', 'values', or 'schema'."
    
    let outRoot =
        match Environment.GetEnvironmentVariable "REIFIED_DOCS_OUT_ROOT" with
        | null | "" ->
            match product with
            | "data" -> Path.Combine(root, "docs/data/reference")
            | "result" -> Path.Combine(root, "docs/result/reference")
            | "values" -> Path.Combine(root, "docs/values/reference")
            | "schema" -> Path.Combine(root, "docs/schema/reference")
            | _ -> Path.Combine(root, "docs/reference")
        | path -> Path.GetFullPath path
    
    if Directory.Exists outRoot then
        for f in Directory.GetFiles(outRoot, "*", SearchOption.AllDirectories) do
            if Path.GetFileName(f) <> "_index.md" then
                File.Delete(f)

        for d in Directory.GetDirectories(outRoot, "*", SearchOption.AllDirectories) |> Array.sortByDescending String.length do
            if not (Directory.EnumerateFileSystemEntries(d) |> Seq.isEmpty) then () else Directory.Delete(d)
    else
        Directory.CreateDirectory(outRoot) |> ignore

    // All inputs load their net8.0 build so the reference always reflects the widest TFM-gated
    // surface (e.g. ValueSchema.date and ofJsonElement); netstandard2.1-only builds would
    // silently drop those members from the docs instead of describing them as unavailable there.
    let resultDllPaths = [
        Path.Combine(artifactsDir, "Reified.Result/debug_net8.0/Reified.Result.dll")
    ]

    let valuesDllPaths = [
        Path.Combine(artifactsDir, "Reified.Constraint/debug_net8.0/Reified.Constraint.dll")
        Path.Combine(artifactsDir, "Reified.Parse/debug_net8.0/Reified.Parse.dll")
        Path.Combine(artifactsDir, "Reified.Refinements/debug_net8.0/Reified.Refinements.dll")
    ]

    let validationDllPaths = resultDllPaths @ valuesDllPaths |> List.distinct

    let schemaDllPaths = [
        yield! validationDllPaths
        Path.Combine(artifactsDir, "Reified.Data/debug_net8.0/Reified.Data.dll")
        Path.Combine(artifactsDir, "Reified.Schema/debug_net8.0/Reified.Schema.dll")
        Path.Combine(artifactsDir, "Reified.Schema.Json/debug_net8.0/Reified.Schema.Json.dll")
        Path.Combine(artifactsDir, "Reified.Schema.Http/debug/Reified.Schema.Http.dll")
    ]

    let dataDllPaths = [
        Path.Combine(artifactsDir, "Reified.Data/debug_net8.0/Reified.Data.dll")
    ]

    let dllPaths =
        match product with
        | "data" -> dataDllPaths
        | "result" -> resultDllPaths
        | "values" -> valuesDllPaths
        | "schema" -> schemaDllPaths
        | _ -> dataDllPaths @ resultDllPaths @ valuesDllPaths @ schemaDllPaths |> List.distinct

    let apiDocInputs = [
        for dll in dllPaths do
            if File.Exists dll then
                yield ApiDocInput.FromFile(
                    dll,
                    sourceFolder = repoRoot,
                    sourceRepo = $"{githubRepoUrl}/blob/{githubBranch}"
                )
    ]

    let substitutions = Substitutions.Empty
    let dependencyDirectories =
        [ typeof<Fable.Core.JS.Promise<_>>.Assembly.Location ]
        |> List.map Path.GetDirectoryName
        |> List.distinct

    let model =
        ApiDocs.GenerateModel(
            apiDocInputs,
            "Reified",
            substitutions,
            root="/",
            qualify=true,
            libDirs=dependencyDirectories)
    
    let allEntities = 
        model.EntityInfos 
        |> Seq.map (fun ei -> ei.Entity)
        |> Seq.collect collectAllEntities
        |> Seq.toList

    // Result is its own product; Values groups Constraint, Refined, and Parse. "validation" and "diagnostics" are
    // retired group names kept here so a stale page can still be routed and cleaned up rather than orphaned.
    let resultReferenceGroups =
        set [ "result" ]

    let valuesReferenceGroups =
        set [ "constraint"; "predicate"; "validation"; "diagnostics"; "parse"; "refined" ]

    let validationReferenceGroups =
        Set.union resultReferenceGroups valuesReferenceGroups

    let dataReferenceGroups =
        set [ "data" ]

    let schemaReferenceGroups =
        set [ "schema"; "codec" ]

    let selectedPageSpecs =
        let forProduct =
            match product with
            | "data" -> pageSpecs |> List.filter (fun spec -> dataReferenceGroups.Contains spec.OutPath.Head)
            | "result" -> pageSpecs |> List.filter (fun spec -> resultReferenceGroups.Contains spec.OutPath.Head)
            | "values" -> pageSpecs |> List.filter (fun spec -> valuesReferenceGroups.Contains spec.OutPath.Head)
            | "schema" -> pageSpecs |> List.filter (fun spec -> schemaReferenceGroups.Contains spec.OutPath.Head)
            | _ -> pageSpecs

        match Environment.GetEnvironmentVariable "REIFIED_DOCS_PAGE_PREFIX" with
        | null | "" -> forProduct
        | prefix -> forProduct |> List.filter (fun spec -> String.concat "/" spec.OutPath |> fun path -> path.StartsWith(prefix, StringComparison.Ordinal))

    let productOutPath (spec: PageSpec) = spec.OutPath

    let referenceRootForSpec (spec: PageSpec) =
        if dataReferenceGroups.Contains spec.OutPath.Head then
            Path.Combine(root, "docs/data/reference")
        elif resultReferenceGroups.Contains spec.OutPath.Head then
            Path.Combine(root, "docs/result/reference")
        elif valuesReferenceGroups.Contains spec.OutPath.Head then
            Path.Combine(root, "docs/values/reference")
        elif schemaReferenceGroups.Contains spec.OutPath.Head then
            Path.Combine(root, "docs/schema/reference")
        else
            Path.Combine(root, "docs/flow/reference")

    let referenceTargetMap = Dictionary<string, string>()

    let registerReferenceTarget (symbolFullName: string) (absolutePath: string) =
        if not (String.IsNullOrWhiteSpace symbolFullName) then
            referenceTargetMap[formatterApiSlug symbolFullName] <- absolutePath

    let registerReferenceId (id: string) (absolutePath: string) =
        let rawName = id.Substring(2).Split('(').[0]
        referenceTargetMap[formatterApiSlug rawName] <- absolutePath

    for spec in pageSpecs do
        let outPath = Path.Combine(referenceRootForSpec spec, Path.Combine(Array.ofList spec.OutPath))

        for sectionTitle, ids in spec.SymbolIds do
            for id in ids do
                let targetDir =
                    match sectionDirectory spec sectionTitle id with
                    | Some dir -> Path.Combine(Path.GetDirectoryName(outPath), dir)
                    | None -> Path.GetDirectoryName outPath

                let pagePath = Path.Combine(targetDir, getPageName id)

                match findBestSymbol allEntities id with
                | Some (ResolvedMember m) ->
                    registerReferenceId id pagePath
                    registerReferenceTarget (safeFullName m.Symbol) pagePath
                    registerReferenceTarget (logicalName m.Symbol) pagePath
                | Some (ResolvedEntity e) ->
                    registerReferenceId id pagePath
                    registerReferenceTarget (safeFullName e.Symbol) pagePath
                | _ -> ()

    let canonicalAliases =
        dict [
            formatterApiSlug "Reified.ConstraintModule", Path.Combine(outRoot, "constraint", "_index.md")
            formatterApiSlug "Reified.ResultBuilder", Path.Combine(outRoot, "result", "result-ce", "p-errorhandling--result.md")
            formatterApiSlug "Reified.RefineBuilder", Path.Combine(outRoot, "refined", "refine-ce", "p-refined--refine.md")
            formatterApiSlug "Reified.Result.ResultBuilder", Path.Combine(outRoot, "result", "result-ce", "p-errorhandling--result.md")
            formatterApiSlug "Reified.Refinements.RefineBuilder", Path.Combine(outRoot, "refined", "refine-ce", "p-refined--refine.md")
            formatterApiSlug "Reified.Schema.Json.Json", Path.Combine(outRoot, "codec", "_index.md")
        ]

    for KeyValue(slug, path) in canonicalAliases do
        if not (referenceTargetMap.ContainsKey slug) then
            referenceTargetMap[slug] <- path

    let sectionMembers = Dictionary<string, ResizeArray<string * string * string>>()
    
    // Debug: print all entity names
    // for e in allEntities do printfn "Entity: %s" (safeFullName e.Symbol)

    for spec in selectedPageSpecs do
        let outPath = Path.Combine(outRoot, Path.Combine(Array.ofList (productOutPath spec)))
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)) |> ignore
        
        let mutable indexContent = 
            $"---\ntitle: \"{spec.Title}\"\nweight: {pageWeight spec}\n---\n\n{spec.Intro}\n\n"
            
        for sectionOrdinal, (sectionTitle, ids) in spec.SymbolIds |> List.indexed do
            indexContent <- indexContent + $"## {sectionTitle}\n\n"
            for itemOrdinal, id in ids |> List.indexed do
                let targetDir =
                    match sectionDirectory spec sectionTitle id with
                    | Some dir ->
                        let dirPath = Path.Combine(Path.GetDirectoryName(outPath), dir)
                        Directory.CreateDirectory dirPath |> ignore
                        dirPath
                    | None ->
                        Path.GetDirectoryName outPath

                match findBestSymbol allEntities id with
                | Some (ResolvedMember m) ->
                    let pageName = getPageName id
                    let qualifier = memberQualifier m
                    let linkText = (if String.IsNullOrEmpty qualifier then m.Name else qualifier + "." + m.Name) |> dedupeAdjacentSegments
                    let pagePath = Path.Combine(targetDir, pageName)
                    let relativeLink = relativeLinkFrom outPath pagePath
                    let rewriteHtml = rewriteApiDocHtml referenceTargetMap pagePath
                    let summaryHtml = rewriteHtml m.Comment.Summary.HtmlText
                    indexContent <- indexContent + $"- [`{linkText}`](./{relativeLink}): {summaryHtml}\n"
                    let memberPageContent = renderMemberPage rewriteHtml (childPageWeight id sectionOrdinal itemOrdinal) m
                    File.WriteAllText(pagePath, normalizeGeneratedMarkdown memberPageContent)

                    match sectionDirectory spec sectionTitle id with
                    | Some dir ->
                        let key = Path.Combine(Path.GetDirectoryName(outPath), dir, "_index.md")
                        let items =
                            match sectionMembers.TryGetValue key with
                            | true, existing -> existing
                            | _ ->
                                let created = ResizeArray()
                                sectionMembers[key] <- created
                                created

                        items.Add(linkText, pageName, summaryHtml)
                    | None -> ()
                    
                    match spec.Alias with
                    | Some a -> File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), a), normalizeGeneratedMarkdown memberPageContent)
                    | None -> ()

                | Some (ResolvedEntity e) ->
                    let pageName = getPageName id
                    let eFullName = safeFullName e.Symbol
                    let linkText = cleanName eFullName |> dedupeAdjacentSegments
                    let pagePath = Path.Combine(targetDir, pageName)
                    let relativeLink = relativeLinkFrom outPath pagePath
                    let rewriteHtml = rewriteApiDocHtml referenceTargetMap pagePath
                    let summaryHtml = rewriteHtml e.Comment.Summary.HtmlText
                    indexContent <- indexContent + $"- [`{linkText}`](./{relativeLink}): {summaryHtml}\n"
                    let entityPageContent = renderEntityPage rewriteHtml (childPageWeight id sectionOrdinal itemOrdinal) e
                    File.WriteAllText(pagePath, normalizeGeneratedMarkdown entityPageContent)

                    match sectionDirectory spec sectionTitle id with
                    | Some dir ->
                        let key = Path.Combine(Path.GetDirectoryName(outPath), dir, "_index.md")
                        let items =
                            match sectionMembers.TryGetValue key with
                            | true, existing -> existing
                            | _ ->
                                let created = ResizeArray()
                                sectionMembers[key] <- created
                                created

                        items.Add(linkText, pageName, summaryHtml)
                    | None -> ()
                | _ -> 
                    printfn "Warning: symbol not found: %s" id
            indexContent <- indexContent + "\n"
            
        File.WriteAllText(outPath, normalizeGeneratedMarkdown indexContent)

    for KeyValue(indexPath, members) in sectionMembers do
        let dirName = Path.GetFileName(Path.GetDirectoryName indexPath)
        let title = sectionTitleForDirectory dirName
        let intro = sectionIntroForDirectory dirName

        let mutable content =
            $"---\ntitle: \"{title}\"\n---\n\n{intro}\n\n"

        for linkText, pageName, summary in members do
            content <- content + $"- [`{linkText}`](./{pageName}): {summary}\n"

        File.WriteAllText(indexPath, normalizeGeneratedMarkdown content)

    0
