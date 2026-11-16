module Reified.Schema.Http.Tests.BoundaryTests

open Reified

open System.Text.Json
open Xunit
open Swensen.Unquote
open Reified.Schema.Http
open Reified.Schema.Http.Tests.Fixtures
open Reified.SchemaDSL

[<Fact>]
let ``json pointers render names, keys, and indexes`` () =
    let path =
        SchemaPath.root
        |> fun path -> SchemaPath.append path (SchemaPath.key "address")
        |> fun path -> SchemaPath.append path (SchemaPath.key "some/key~x")
        |> fun path -> SchemaPath.append path (SchemaPath.index 2)

    test <@ JsonPointer.ofPath path = "/address/some~1key~0x/2" @>

[<Fact>]
let ``the empty path renders as the whole-document pointer`` () =
    test <@ JsonPointer.ofPath SchemaPath.root = "" @>

[<Fact>]
let ``form pairs nest through dotted names and group repeats`` () =
    let raw =
        BoundaryInput.ofForm
            [ "name", "Ada Lovelace"
              "age", "36"
              "address.street", "12 Analytical Way"
              "address.city", "London"
              "tags", "vip"
              "tags", "founder" ]

    let parsed = Schema.parseRetainingInput (signupSchema ()) raw
    test <@ parsed.IsValid @>
    test <@ parsed.Value.Address.City = "London" @>
    test <@ parsed.Value.Tags = [ "vip"; "founder" ] @>

[<Fact>]
let ``indexed form names become ordered collections`` () =
    let raw =
        BoundaryInput.ofForm
            [ "name", "Ada Lovelace"
              "age", "36"
              "address.street", "12 Analytical Way"
              "address.city", "London"
              "tags.1", "founder"
              "tags.0", "vip" ]

    let parsed = Schema.parseRetainingInput (signupSchema ()) raw
    test <@ parsed.IsValid @>
    test <@ parsed.Value.Tags = [ "vip"; "founder" ] @>

[<Fact>]
let ``query pairs parse flat models`` () =
    let schema =
        schema<{| Page: int; Terms: string list |}> {
            fieldAs "page" (fun (value: {| Page: int; Terms: string list |}) -> value.Page)
            fieldAs "terms" (fun (value: {| Page: int; Terms: string list |}) -> value.Terms) {
                withSchema (Schema.listWith Schema.text)
            }
            construct (fun page terms -> {| Page = page; Terms = terms |})
        }

    let parsed =
        Schema.parseRetainingInput schema (BoundaryInput.ofQuery [ "page", "3"; "terms", "one"; "terms", "two" ])

    test <@ parsed.IsValid @>
    test <@ parsed.Value.Page = 3 @>
    test <@ parsed.Value.Terms = [ "one"; "two" ] @>

[<Fact>]
let ``failed parses render problem details with json pointers`` () =
    use document = JsonDocument.Parse invalidJson
    let parsed = Schema.parseRetainingInput (signupSchema ()) (Data.ofJsonDocument document)

    let details =
        match ProblemDetails.ofParsed parsed with
        | Some details -> details
        | None -> failwith "expected a failed parse"

    test <@ details.Status = 400 @>
    test <@ details.Errors |> List.exists (fun error -> error.Pointer = "/address/city") @>
    test <@ details.Errors |> List.exists (fun error -> error.Pointer = "/tags") @>

    let json = ProblemDetails.toJson details
    use body = JsonDocument.Parse json
    test <@ body.RootElement.GetProperty("status").GetInt32() = 400 @>
    test <@ body.RootElement.GetProperty("errors").GetArrayLength() > 0 @>

[<Fact>]
let ``successful parses produce no problem details`` () =
    use document = JsonDocument.Parse validJson
    let parsed = Schema.parseRetainingInput (signupSchema ()) (Data.ofJsonDocument document)
    test <@ ProblemDetails.ofParsed parsed = None @>

[<Fact>]
let ``ProblemError has stable null-safe one-line text`` () =
    let error: ProblemError = { Pointer = null; Message = "First\r\nSecond\rThird\nFourth" }
    test
        <@
            error.ToString() =
                "ProblemError (pointer '<null>', message 'First | Second | Third | Fourth')"
        @>

[<Fact>]
let ``ProblemDetails omits absent detail and normalizes public strings`` () =
    let problem: Reified.Schema.Http.ProblemDetails =
        { Type = "about:\rblank"
          Title = "Invalid\nrequest"
          Status = 400
          Detail = None
          Errors = [ { Pointer = "/name"; Message = "Expected text." } ] }
    test
        <@
            problem.ToString() =
                "ProblemDetails (type 'about: | blank', title 'Invalid | request', status 400, errors [ProblemError (pointer '/name', message 'Expected text.')])"
        @>

[<Fact>]
let ``ProblemDetails distinguishes literal detail and null collections`` () =
    let problem: Reified.Schema.Http.ProblemDetails =
        { Type = null
          Title = null
          Status = 500
          Detail = Some "none\r\ncontinued"
          Errors = Unchecked.defaultof<ProblemError list> }
    test
        <@
            problem.ToString() =
                "ProblemDetails (type '<null>', title '<null>', status 500, detail 'none | continued', errors [<null>])"
        @>

[<Fact>]
let ``openapi documents embed generated schemas per endpoint`` () =
    let spec =
        Endpoint.post "/signups"
        |> Endpoint.summary "Create a signup"
        |> Endpoint.accepts (signupSchema ())
        |> Endpoint.returnsJson 201 "The trusted signup that was parsed." (signupSchema ())
        |> Endpoint.returnsProblemDetails

    let document = OpenApi.document (OpenApi.info "Signup API" "1.0.0") [ spec ]

    use parsed = JsonDocument.Parse document
    let root = parsed.RootElement
    Assert.Equal("3.1.0", root.GetProperty("openapi").GetString())

    let operation = root.GetProperty("paths").GetProperty("/signups").GetProperty("post")
    Assert.Equal("Create a signup", operation.GetProperty("summary").GetString())

    let requestSchema =
        operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")

    Assert.True(requestSchema.GetProperty("properties").TryGetProperty("name") |> fst)

    let responses = operation.GetProperty "responses"
    Assert.True(responses.TryGetProperty("201") |> fst)

    let problem = responses.GetProperty("400").GetProperty("content")
    Assert.True(problem.TryGetProperty(ProblemDetails.ContentType) |> fst)

type private Search = { Page: int; Terms: string list; Owner: string option }

let private searchSchema () =
    schema<Search> {
        field _.Page
        field _.Terms { withSchema (Schema.listWith Schema.text) }
        field _.Owner
        construct (fun page terms owner -> { Page = page; Terms = terms; Owner = owner })
    }

[<Fact>]
let ``a blank field parses as absent`` () =
    let parsed =
        Schema.parseRetainingInput (searchSchema ()) (BoundaryInput.ofForm [ "page", "2"; "terms.0", "a"; "owner", "" ])

    test <@ parsed.Result = Ok { Page = 2; Terms = [ "a" ]; Owner = None } @>

[<Fact>]
let ``a blank required field reports that it must be present`` () =
    let parsed = Schema.parseRetainingInput (signupSchema ()) (BoundaryInput.ofForm [ "name", ""; "age", "36" ])
    test <@ not (List.isEmpty (parsed.ErrorsFor "name")) @>

[<Fact>]
let ``a model written as pairs reads back from a form`` () =
    let schema = signupSchema ()

    let signup =
        { Name = "Ada Lovelace"
          Age = 36
          Address = { Street = "12 Analytical Way"; City = "London" }
          Tags = [ "vip" ] }

    let pairs = Json.toData (Json.compile schema) signup |> BoundaryInput.toPairs

    test <@ pairs |> List.contains ("address.city", "London") @>
    test <@ pairs |> List.contains ("tags.0", "vip") @>
    test <@ (Schema.parseRetainingInput schema (BoundaryInput.ofForm pairs)).Result = Ok signup @>

[<Fact>]
let ``a model written as pairs reads back from a query string`` () =
    let schema = searchSchema ()
    let search = { Page = 1; Terms = [ "only" ]; Owner = Some "ada" }

    let pairs = Json.toData (Json.compile schema) search |> BoundaryInput.toPairs

    test <@ pairs = [ "page", "1"; "terms.0", "only"; "owner", "ada" ] @>
    test <@ (Schema.parseRetainingInput schema (BoundaryInput.ofQuery pairs)).Result = Ok search @>

[<Fact>]
let ``pairs need object-shaped data`` () =
    raises<System.ArgumentException> <@ BoundaryInput.toPairs (Data.Text "loose") @>

[<Fact>]
let ``query strings escape names and values`` () =
    test <@ BoundaryInput.queryString [ "q", "red car&more"; "page", "2" ] = "q=red%20car%26more&page=2" @>

[<Fact>]
let ``bracketed names read the same as dotted names`` () =
    let dotted =
        BoundaryInput.ofForm
            [ "name", "Ada Lovelace"; "age", "36"
              "address.street", "12 Analytical Way"; "address.city", "London"
              "tags.0", "vip"; "tags.1", "founder" ]

    let bracketed =
        BoundaryInput.ofForm
            [ "name", "Ada Lovelace"; "age", "36"
              "address[street]", "12 Analytical Way"; "address[city]", "London"
              "tags[0]", "vip"; "tags[1]", "founder" ]

    test <@ bracketed = dotted @>

[<Fact>]
let ``each empty bracket takes the next index, so one item still reads as a list`` () =
    let parsed = Schema.parseRetainingInput (searchSchema ()) (BoundaryInput.ofQuery [ "page", "1"; "terms[]", "only" ])
    test <@ parsed.Result = Ok { Page = 1; Terms = [ "only" ]; Owner = None } @>

    let many = BoundaryInput.ofQuery [ "terms[]", "a"; "terms[]", "b" ]
    test <@ many = Data.Object [ "terms", Data.List [ Data.Text "a"; Data.Text "b" ] ] @>

[<Fact>]
let ``brackets and dots mix within one name`` () =
    let data = BoundaryInput.ofForm [ "items[0].name", "bolt"; "items.0[size]", "M6" ]
    test <@ data = Data.Object [ "items", Data.List [ Data.Object [ "name", Data.Text "bolt"; "size", Data.Text "M6" ] ] ] @>

[<Fact>]
let ``a name with an unclosed bracket stays literal`` () =
    test <@ BoundaryInput.ofForm [ "odd[name", "x" ] = Data.Object [ "odd[name", Data.Text "x" ] @>
