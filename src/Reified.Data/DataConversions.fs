namespace Reified

open System
#if !FABLE_COMPILER
open System.Collections.Specialized
#endif
#if FABLE_COMPILER
open Fable.Core.JsInterop
#endif

/// <summary>How <c>Data.ofEnvironmentWith</c> selects environment variables and splits their names into sections.</summary>
/// <remarks>
/// Pass a configuring function to <c>Data.ofEnvironmentWith</c>; the value it receives is the default used by
/// <c>Data.ofEnvironment</c>: no prefix and the separator <c>__</c>.
/// </remarks>
type DataEnvironmentOptions =
    { /// <summary>
      /// Takes only variables whose name starts with this text, compared ordinally (case-sensitive), and strips it.
      /// Include any separator in the prefix: <c>Some "MYAPP__"</c> turns <c>MYAPP__Port</c> into <c>Port</c>.
      /// <c>None</c> takes every variable.
      /// </summary>
      Prefix: string option
      /// <summary>
      /// The text that separates section names inside a variable name. The default <c>__</c> is the
      /// Microsoft.Extensions.Configuration convention, chosen because <c>:</c> is not valid in POSIX shell names.
      /// </summary>
      Separator: string }

/// <summary>Internal conversions from supported source representations into owned structured data.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal DataConversions =
    /// <summary>Builds an object from an F# map, ordered by key.</summary>
    let objectOfMap (fields: Map<string, Data>) : Data =
        if isNull (box fields) then nullArg (nameof fields)
        fields |> Map.toList |> Data.Object

    type private ConfigurationNode =
        | Value of Data
        | Branch of Map<string, ConfigurationNode>

    let private ensureName (name: string) =
        if isNull name then
            nullArg (nameof name)

        if name = "" then
            invalidArg (nameof name) "Structured data field names cannot be empty."

        name

    let private ensureValues name values =
        if isNull (box values) then
            nullArg name

        values

    let private textOrNull (value: string) =
        if isNull value then Data.Null else Data.Text value

    let private fieldValue (values: string list) =
        match values with
        | [] -> Data.Null
        | [ value ] -> textOrNull value
        | values -> values |> List.map textOrNull |> Data.List

    let private objectFromGroupedValues (values: seq<string * string list>) =
        values
        |> Seq.map (fun (name, values) -> ensureName name, fieldValue values)
        |> Seq.toList
        |> Data.Object

    let private addField name value fields =
        let name = ensureName name

        let append existing =
            match existing with
            | None -> Some value
            | Some(Data.List values) -> Some(Data.List(values @ [ value ]))
            | Some existing -> Some(Data.List [ existing; value ])

        fields |> Map.change name append

    let private tryNonNegativeInt (text: string) =
        match Int32.TryParse text with
        | true, value when value >= 0 -> Some value
        | _ -> None

    let private insertConfigurationValue (segments: string list) value node =
        let rec insert remaining current =
            match remaining, current with
            // Last write wins, matching .NET configuration layering — except that a null value never
            // overrides an existing section, because IConfiguration.AsEnumerable() emits every section
            // key with a null value alongside that section's children.
            | [], Branch children when not children.IsEmpty && value = Data.Null -> current
            | [], _ -> Value value
            // A later section path replaces an earlier scalar at the same key: last write wins there too.
            | _ :: _, Value _ -> insert remaining (Branch Map.empty)
            | segment :: rest, Branch children ->
                let segment = ensureName segment
                let child = children |> Map.tryFind segment |> Option.defaultValue (Branch Map.empty)
                Branch(children |> Map.add segment (insert rest child))

        insert segments node

    let rec private convertConfigurationNode node =
        match node with
        | Value value -> value
        | Branch children when children.IsEmpty -> Data.Object []
        | Branch children ->
            let indexed =
                children
                |> Map.toList
                |> List.map (fun (key, child) -> tryNonNegativeInt key |> Option.map (fun index -> index, child))

            if indexed |> List.forall Option.isSome then
                let byIndex = indexed |> List.choose id |> Map.ofList
                let maximum = byIndex |> Map.toSeq |> Seq.map fst |> Seq.max

                [ for index in 0..maximum ->
                      byIndex
                      |> Map.tryFind index
                      |> Option.map convertConfigurationNode
                      |> Option.defaultValue Data.Null ]
                |> Data.List
            else
                children
                |> Map.toList
                |> List.map (fun (name, child) -> name, convertConfigurationNode child)
                |> Data.Object

    /// <summary>Builds object-shaped structured data from a list of named structured data fields.</summary>
    /// <remarks>Field order and repeated names are preserved.</remarks>
    /// <example>
    /// <code>
    /// [ "email", Data.Text "ada@example.com"
    ///   "age", Data.Text "42" ]
    /// |> Data.objectOfList
    /// </code>
    /// </example>
    let objectOfList (fields: (string * Data) list) : Data =
        ensureValues (nameof fields) fields
        |> List.map (fun (name, value) -> ensureName name, value)
        |> Data.Object

    /// <summary>Builds object-shaped structured data from a map of scalar field values.</summary>
    let ofMap (values: Map<string, string>) : Data =
        if isNull (box values) then
            nullArg (nameof values)

        values
        |> Map.toSeq
        |> Seq.map (fun (name, value) -> ensureName name, textOrNull value)
        |> Seq.toList
        |> Data.Object

    /// <summary>Builds object-shaped structured data from a .NET dictionary of scalar field values.</summary>
    /// <remarks>
    /// A C#-friendly equivalent of <c>ofMap</c>: takes <see cref="T:System.Collections.Generic.IDictionary`2" />
    /// instead of an F# <c>Map</c>, so callers do not need to construct an F# map value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="values" /> is null.</exception>
    let ofDictionary (values: System.Collections.Generic.IDictionary<string, string>) : Data =
        if isNull values then
            nullArg (nameof values)

        values
        |> Seq.map (fun pair -> ensureName pair.Key, textOrNull pair.Value)
        |> Seq.toList
        |> Data.Object

    /// <summary>Builds object-shaped structured data from name/value pairs, grouping repeated names into <c>Many</c>.</summary>
    let ofNameValues (values: seq<string * string>) : Data =
        ensureValues (nameof values) values
        |> Seq.groupBy fst
        |> Seq.map (fun (name, grouped) -> name, grouped |> Seq.map snd |> Seq.toList)
        |> objectFromGroupedValues

#if !FABLE_COMPILER
    /// <summary>Builds object-shaped structured data from a .NET name-value collection.</summary>
    let ofNameValueCollection (values: NameValueCollection) : Data =
        if isNull values then
            nullArg (nameof values)

        values.AllKeys
        |> Seq.map (fun name ->
            let name = ensureName name

            let fieldValues =
                match values.GetValues name with
                | null -> []
                | fieldValues -> fieldValues |> Array.toList

            name, fieldValues)
        |> objectFromGroupedValues
#endif

#if FABLE_COMPILER
    [<Fable.Core.Emit("typeof $0")>]
    let private jsTypeOf (value: obj) : string = Unchecked.defaultof<string>

    [<Fable.Core.Emit("Array.isArray($0)")>]
    let private jsIsArray (value: obj) : bool = false

    [<Fable.Core.Emit("Object.keys($0)")>]
    let private jsObjectKeys (value: obj) : string[] = Unchecked.defaultof<string[]>

    [<Fable.Core.Emit("$0[$1]")>]
    let private jsProperty (value: obj) (name: string) : obj = null

    [<Fable.Core.Emit("String($0)")>]
    let private jsString (value: obj) : string = Unchecked.defaultof<string>

    /// <summary>Copies a value returned by JavaScript <c>JSON.parse</c> into structured data.</summary>
    /// <remarks>
    /// JavaScript parsing has already discarded duplicate object fields and the original spelling of number tokens.
    /// Use <c>Reified.Json.parseData</c> when those distinctions must be retained.
    /// </remarks>
    let rec ofJsonValue (value: obj) : Data =
        if isNull value then
            Data.Null
        elif jsIsArray value then
            value |> unbox<obj[]> |> Array.map ofJsonValue |> Array.toList |> Data.List
        else
            match jsTypeOf value with
            | "string" -> Data.Text(unbox<string> value)
            | "boolean" -> Data.Bool(unbox<bool> value)
            | "number" -> Data.Number(jsString value)
            | "object" ->
                jsObjectKeys value
                |> Array.map (fun name -> name, ofJsonValue (jsProperty value name))
                |> Array.toList
                |> Data.Object
            | actual -> invalidArg (nameof value) $"Expected a value returned by JSON.parse but found JavaScript {actual}."
#endif

    /// <summary>
    /// Builds structured data from command-line arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supports <c>--name value</c>, <c>--name=value</c>, <c>-n value</c>, boolean flags, <c>--no-name</c>, and repeated
    /// options. Positional arguments are stored under the <c>_</c> field as a collection.
    /// </para>
    /// </remarks>
    let ofCliArgs (args: seq<string>) : Data =
        let args = ensureValues (nameof args) args |> Seq.toList

        let rec loop remaining fields positionals =
            match remaining with
            | [] ->
                let fields =
                    match List.rev positionals with
                    | [] -> fields
                    | positionals -> fields |> addField "_" (positionals |> List.map Data.Text |> Data.List)

                fields |> Map.toList |> Data.Object
            | "--" :: rest ->
                loop [] fields (List.rev rest @ positionals)
            | arg :: rest when isNull arg ->
                nullArg (nameof args)
            | arg :: rest when arg.StartsWith("--no-", StringComparison.Ordinal) && arg.Length > 5 ->
                let name = arg.Substring 5
                loop rest (fields |> addField name (Data.Text "false")) positionals
            | arg :: rest when arg.StartsWith("--", StringComparison.Ordinal) && arg.Length > 2 ->
                let optionText = arg.Substring 2
                let equalsIndex = optionText.IndexOf('=')

                if equalsIndex >= 0 then
                    let name = optionText.Substring(0, equalsIndex)
                    let value = optionText.Substring(equalsIndex + 1)
                    loop rest (fields |> addField name (Data.Text value)) positionals
                else
                    match rest with
                    | value :: tail when not (isNull value) && not (value.StartsWith("-", StringComparison.Ordinal)) ->
                        loop tail (fields |> addField optionText (Data.Text value)) positionals
                    | _ -> loop rest (fields |> addField optionText (Data.Text "true")) positionals
            | arg :: rest when arg.StartsWith("-", StringComparison.Ordinal) && arg.Length > 1 ->
                let optionText = arg.Substring 1
                let equalsIndex = optionText.IndexOf('=')

                if equalsIndex >= 0 then
                    let name = optionText.Substring(0, equalsIndex)
                    let value = optionText.Substring(equalsIndex + 1)
                    loop rest (fields |> addField name (Data.Text value)) positionals
                else
                    match rest with
                    | value :: tail when not (isNull value) && not (value.StartsWith("-", StringComparison.Ordinal)) ->
                        loop tail (fields |> addField optionText (Data.Text value)) positionals
                    | _ -> loop rest (fields |> addField optionText (Data.Text "true")) positionals
            | arg :: rest -> loop rest fields (arg :: positionals)

        loop args Map.empty []

#if NET8_0_OR_GREATER && !FABLE_COMPILER
    /// <summary>Builds structured data from a <see cref="T:System.Text.Json.JsonElement" />.</summary>
    /// <remarks>
    /// <para>
    /// This converts JSON parsed with <c>System.Text.Json</c> into a reusable structured value. JSON value kinds remain
    /// distinct, and number tokens are carried without narrowing them to one CLR numeric type. Other JSON syntax,
    /// such as whitespace and source locations, is not represented.
    /// </para>
    /// <para>
    /// The adapter is available on .NET 8+ targets where <c>System.Text.Json</c> ships in-box, keeping the package
    /// dependency-free and Fable-safe on other targets.
    /// </para>
    /// <para>netstandard2.1: not available.</para>
    /// </remarks>
    let rec ofJsonElement (element: System.Text.Json.JsonElement) : Data =
        match element.ValueKind with
        | System.Text.Json.JsonValueKind.Null
        | System.Text.Json.JsonValueKind.Undefined -> Data.Null
        | System.Text.Json.JsonValueKind.String -> textOrNull (element.GetString())
        | System.Text.Json.JsonValueKind.Number -> Data.Number(element.GetRawText())
        | System.Text.Json.JsonValueKind.True -> Data.Bool true
        | System.Text.Json.JsonValueKind.False -> Data.Bool false
        | System.Text.Json.JsonValueKind.Array ->
            element.EnumerateArray() |> Seq.map ofJsonElement |> Seq.toList |> Data.List
        | _ ->
            element.EnumerateObject()
            |> Seq.map (fun property -> ensureName property.Name, ofJsonElement property.Value)
            |> Seq.toList
            |> Data.Object

    /// <summary>Builds structured data from the root element of a <see cref="T:System.Text.Json.JsonDocument" />.</summary>
    /// <remarks>netstandard2.1: not available.</remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="document" /> is null.</exception>
    let ofJsonDocument (document: System.Text.Json.JsonDocument) : Data =
        if isNull document then
            nullArg (nameof document)

        ofJsonElement document.RootElement
#endif

    /// <summary>
    /// Builds structured data from flattened configuration keys using <c>:</c> as the path separator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Numeric path segments are interpreted as collection indexes, matching the common .NET configuration convention
    /// for arrays such as <c>contacts:0:value</c>.
    /// </para>
    /// <para>
    /// Later pairs override earlier ones at the same path, matching .NET configuration layering: a repeated key
    /// keeps its last value, and a later scalar or section replaces an earlier section or scalar at that key.
    /// Collections come from numeric segments, never from repetition — repeated names as multi-value input is a
    /// wire convention that belongs to <c>ofNameValues</c>. A null value never overrides an existing section,
    /// because <c>IConfiguration.AsEnumerable()</c> emits every section key with a null value alongside that
    /// section's children.
    /// </para>
    /// </remarks>
    let ofConfiguration (values: seq<string * string>) : Data =
        let values = ensureValues (nameof values) values

        values
        |> Seq.fold
            (fun node (key, value) ->
                let key = ensureName key
                let segments = key.Split([| ':' |], StringSplitOptions.None) |> Array.toList

                if segments |> List.exists ((=) "") then
                    invalidArg (nameof values) $"Configuration key cannot contain an empty segment: {key}"

                insertConfigurationValue segments (textOrNull value) node)
            (Branch Map.empty)
        |> convertConfigurationNode

    /// <summary>Builds structured data from configuration key/value pairs, such as .NET <c>IConfiguration.AsEnumerable()</c>.</summary>
    /// <remarks>
    /// A C#-friendly equivalent of <c>ofConfiguration</c>: takes
    /// <see cref="T:System.Collections.Generic.IEnumerable`1" /> of
    /// <see cref="T:System.Collections.Generic.KeyValuePair`2" /> instead of a sequence of F# tuples, matching what
    /// <c>Microsoft.Extensions.Configuration</c>'s <c>IConfiguration.AsEnumerable()</c> returns directly.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="values" /> is null.</exception>
    let ofConfigurationPairs
        (values: System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>)
        : Data =
        if isNull values then
            nullArg (nameof values)

        values |> Seq.map (fun pair -> pair.Key, pair.Value) |> ofConfiguration

    /// <summary>The options <c>ofEnvironment</c> uses: no prefix and the separator <c>__</c>.</summary>
    let environmentDefaults: DataEnvironmentOptions = { Prefix = None; Separator = "__" }

    /// <summary>Builds structured data from environment variable name and value pairs using configured options.</summary>
    /// <remarks>
    /// <para>
    /// The caller reads the environment and passes the pairs in, so the conversion itself performs no I/O. Each name
    /// is split on <c>Separator</c> into a path and its value becomes a <c>Data.Text</c> leaf; the schema that parses
    /// the result decides whether <c>"8080"</c> is a number. Names keep the case they were given. Field matching is the
    /// schema's concern: a schema field <c>port</c> does not match a variable <c>PORT</c> unless it declares
    /// <c>alias "PORT"</c>.
    /// </para>
    /// <para>
    /// Numeric segments are list indexes, as in <c>ofConfiguration</c>, so <c>Servers__0__Host</c> builds a
    /// <c>Data.List</c>. This is the Microsoft.Extensions.Configuration convention for arrays. A gap in the indexes is
    /// filled with <c>Data.Null</c>. The root is always a <c>Data.Object</c>, even when every name is numeric.
    /// </para>
    /// <para>
    /// The environment has no meaningful order, so names are applied in ordinal order rather than input order. A
    /// name that is also a section therefore always loses to the section: <c>A=1</c> with <c>A__B=2</c> gives
    /// <c>{ "A": { "B": "2" } }</c> whichever order the pairs arrive in. A name repeated in the input keeps its
    /// last value. A name that is empty after removing the prefix, or that contains an empty segment
    /// (<c>__CF_USER_TEXT_ENCODING</c>, <c>A____B</c>, <c>TRAILING__</c>), cannot address a path and is skipped
    /// rather than rejected, because the process environment is not authored by the caller.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [ "MYAPP__Database__Port", "5432"; "PATH", "/usr/bin" ]
    /// |&gt; Data.ofEnvironmentWith (fun options -&gt; { options with Prefix = Some "MYAPP__" })
    /// // Data.Object [ "Database", Data.Object [ "Port", Data.Text "5432" ] ]
    /// </code>
    /// </example>
    /// <exception cref="T:System.ArgumentNullException">Thrown when the configuring function, its result, the
    /// variables, or a variable name is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when the separator is null or empty, or the prefix is
    /// <c>Some</c> null or empty text.</exception>
    let ofEnvironmentWith
        (configure: DataEnvironmentOptions -> DataEnvironmentOptions)
        (variables: seq<string * string>)
        : Data =
        if isNull (box configure) then
            nullArg (nameof configure)

        let variables = ensureValues (nameof variables) variables
        let options = configure environmentDefaults

        if isNull (box options) then
            nullArg (nameof configure)

        if String.IsNullOrEmpty options.Separator then
            invalidArg (nameof configure) "The environment section separator cannot be empty."

        match options.Prefix with
        | Some prefix when String.IsNullOrEmpty prefix ->
            invalidArg (nameof configure) "The environment prefix cannot be empty; use None to take every variable."
        | _ -> ()

        let separator = [| options.Separator |]

        let pathOf (name: string) =
            if isNull name then
                nullArg (nameof variables)

            let key =
                match options.Prefix with
                | None -> Some name
                | Some prefix when name.StartsWith(prefix, StringComparison.Ordinal) -> Some(name.Substring prefix.Length)
                | Some _ -> None

            key
            |> Option.bind (fun key ->
                let segments = key.Split(separator, StringSplitOptions.None) |> Array.toList

                if segments |> List.exists String.IsNullOrEmpty then None else Some(key, segments))

        let root =
            variables
            |> Seq.choose (fun (name, value) -> pathOf name |> Option.map (fun (key, segments) -> key, segments, value))
            |> Seq.toList
            // Stable, so a repeated name keeps its last value; a value sorts before any section that extends it.
            |> List.sortWith (fun (left, _, _) (right, _, _) -> String.CompareOrdinal(left, right))
            |> List.fold (fun node (_, segments, value) -> insertConfigurationValue segments (textOrNull value) node) (Branch Map.empty)

        match root with
        | Branch children ->
            children
            |> Map.toList
            |> List.map (fun (name, child) -> name, convertConfigurationNode child)
            |> Data.Object
        | Value value -> value

    /// <summary>Builds structured data from environment variable name and value pairs.</summary>
    /// <remarks>
    /// Uses no prefix and the separator <c>__</c>; see <c>ofEnvironmentWith</c> for the full rules.
    /// </remarks>
    /// <example>
    /// <code>
    /// Data.ofEnvironment [ "Logging__Level", "Debug"; "Port", "8080" ]
    /// // Data.Object [ "Logging", Data.Object [ "Level", Data.Text "Debug" ]; "Port", Data.Text "8080" ]
    /// </code>
    /// </example>
    let ofEnvironment (variables: seq<string * string>) : Data = ofEnvironmentWith id variables
