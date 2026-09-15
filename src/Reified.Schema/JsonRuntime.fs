namespace Reified

open System.Text

/// <summary>The exception raised when JSON text cannot be decoded through a compiled schema codec.</summary>
/// <remarks>
/// The path renders like <c>$.contacts[1].value</c>, matching the field names declared on the schema. Codec decoding
/// is the trusted hot path: it reports the first structural failure and does not accumulate path-aware diagnostics.
/// Use schema input parsing (<c>Schema.parse</c> over <c>Data</c>) at untrusted boundaries where complete
/// diagnostics matter more than throughput.
/// </remarks>
type JsonCodecException(path: string, detail: string, ?inner: exn) =
    inherit System.Exception(
        (if path = "" then "JSON decode failed at $: " + detail
         else "JSON decode failed at $" + path + ": " + detail),
        (match inner with
         | Some inner -> inner
         | None -> null)
    )

    /// <summary>Gets the schema-relative location of the failure, such as <c>.contacts[1].value</c>.</summary>
    member _.Path = if path = "" then "$" else "$" + path

    /// <summary>Gets the failure detail without the path prefix.</summary>
    member _.Detail = detail

/// <summary>Indentation applied to nested levels by the JSON writer.</summary>
[<RequireQualifiedAccess>]
type JsonIndent =
    /// <summary>No indentation: the compact single-line form.</summary>
    | None
    /// <summary>Indent each nested level by this many spaces.</summary>
    | Spaces of int
    /// <summary>Indent each nested level with one tab character.</summary>
    | Tab

/// <summary>Line ending used for the indentation newlines and the optional trailing newline.</summary>
[<RequireQualifiedAccess>]
type JsonLineEnding =
    /// <summary><c>\n</c>.</summary>
    | Lf
    /// <summary><c>\r\n</c>.</summary>
    | CrLf

/// <summary>Cosmetic options for the JSON writer.</summary>
/// <remarks>
/// Every combination produces JSON a conformant reader parses back to the same model. Wire-shape choices — field
/// names, union representation, whether an absent field is omitted or written as <c>null</c> — belong on the schema,
/// not here. Pass a configuring function to <see cref="M:Reified.Json.serializeWith``1" />; the unmodified value is
/// <see cref="P:Reified.Json.defaults" />, which matches <see cref="M:Reified.Json.serialize``1" />.
/// </remarks>
type JsonWriteOptions =
    { /// <summary>Indentation for nested levels. <see cref="F:Reified.JsonIndent.None" /> is compact.</summary>
      Indent: JsonIndent
      /// <summary>Append one line ending after the value.</summary>
      TrailingNewline: bool
      /// <summary>Line ending for indentation and the trailing newline.</summary>
      LineEnding: JsonLineEnding
      /// <summary>Escape every non-ASCII scalar as <c>\uXXXX</c> so the output is pure ASCII.</summary>
      AsciiOnly: bool
      /// <summary>Also escape <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>, <c>'</c>, <c>+</c>, and U+2028/U+2029 for safe
      /// embedding in HTML or a pre-ES2019 <c>&lt;script&gt;</c> element.</summary>
      EscapeHtml: bool }

/// Internal JSON lexer and writer primitives shared by the compiled encoder and decoder plans.
module internal JsonRuntime =

    /// A compiled decoder step: reads one value and returns it with the advanced source.
    type Decoder<'value> = ByteSource -> struct ('value * ByteSource)

    /// A compiled encoder step: writes one value.
    type Encoder<'value> = IByteWriter -> 'value -> unit

    let decodeFailure detail = raise (JsonCodecException("", detail))

    /// Prepends a path segment to decode failures raised while decoding beneath it.
    let withFieldPath (name: string) (decode: unit -> 'result) : 'result =
        try
            decode ()
        with
        | :? JsonCodecException as ex ->
            raise (JsonCodecException("." + name + (if ex.Path = "$" then "" else ex.Path.Substring 1), ex.Detail, ex))

    let inline isWhitespaceByte (b: byte) =
        b = byte ' ' || b = byte '\n' || b = byte '\r' || b = byte '\t'

    let inline isDigit (b: byte) = b >= byte '0' && b <= byte '9'

#if NET8_0_OR_GREATER && !FABLE_COMPILER
    let private whitespaceSearchValues =
        System.Buffers.SearchValues.Create(" \n\r\t"B)
#endif

    let skipWhitespace (src: ByteSource) =
        let data = src.Data
        let offset = src.Offset

        if offset >= data.Length || not (isWhitespaceByte data[offset]) then
            src
        else
#if NET8_0_OR_GREATER && !FABLE_COMPILER
            let remaining = src.RemainingSpan

            let nextNonWhitespace =
                System.MemoryExtensions.IndexOfAnyExcept(remaining, whitespaceSearchValues)

            if nextNonWhitespace < 0 then
                ByteSource(data, data.Length)
            else
                ByteSource(data, offset + nextNonWhitespace)
#else
            let mutable i = offset + 1

            while i < data.Length && isWhitespaceByte data[i] do
                i <- i + 1

            ByteSource(data, i)
#endif

    let expectByte (expected: byte) (label: string) (src: ByteSource) =
        let src = skipWhitespace src

        if src.Offset >= src.Data.Length || src.Data[src.Offset] <> expected then
            decodeFailure ("expected " + label)

        skipWhitespace (src.Advance 1)

    /// After a value inside an object or array: advances past `,` (returning true) or the close byte (returning false).
    let readSeparatorOrClose (closeByte: byte) (errorLabel: string) (current: ByteSource) =
        let current = skipWhitespace current
        let data = current.Data

        if current.Offset >= data.Length then
            decodeFailure ("expected , or " + errorLabel)

        if data[current.Offset] = byte ',' then
            struct (skipWhitespace (current.Advance 1), true)
        elif data[current.Offset] = closeByte then
            struct (current.Advance 1, false)
        else
            decodeFailure ("expected , or " + errorLabel)

    let advancePastColon (current: ByteSource) =
        let data = current.Data
        let offset = current.Offset

        if offset < data.Length && data[offset] = byte ':' then
            current.Advance 1
        else
            let current = skipWhitespace current

            if current.Offset >= data.Length || data[current.Offset] <> byte ':' then
                decodeFailure "expected :"

            current.Advance 1

    /// Scans a string token without materializing it: returns content start, length, and whether escapes appeared.
    let stringRaw (src: ByteSource) : struct (int * int * bool * ByteSource) =
        let src = skipWhitespace src
        let data = src.Data
        let dataLength = data.Length

        if src.Offset >= dataLength || data[src.Offset] <> byte '"' then
            decodeFailure "expected string"

        let mutable i = src.Offset + 1
        let mutable finished = false
        let mutable hadEscapes = false

        while i < dataLength && not finished do
#if !FABLE_COMPILER
            let nextInteresting =
                System.MemoryExtensions.IndexOfAny(ByteSource(data, i).RemainingSpan, byte '"', byte '\\')

            if nextInteresting < 0 then i <- dataLength else i <- i + nextInteresting
#else
            while i < dataLength && data[i] <> byte '"' && data[i] <> byte '\\' do
                i <- i + 1
#endif

            if i < dataLength then
                if data[i] = byte '"' then
                    finished <- true
                else
                    hadEscapes <- true
                    i <- i + 1

                    if i >= dataLength then
                        decodeFailure "unterminated escape sequence"

                    if data[i] = byte 'u' then
                        if i + 4 >= dataLength then
                            decodeFailure "unterminated unicode escape"

                        i <- i + 4

                    i <- i + 1

        if not finished then
            decodeFailure "unterminated string"

        struct (src.Offset + 1, i - (src.Offset + 1), hadEscapes, ByteSource(data, i + 1))

    let private hexValue (b: byte) =
        if b >= byte '0' && b <= byte '9' then int b - int (byte '0')
        elif b >= byte 'A' && b <= byte 'F' then int b - int (byte 'A') + 10
        elif b >= byte 'a' && b <= byte 'f' then int b - int (byte 'a') + 10
        else decodeFailure "invalid unicode escape"

    /// Materializes a raw string token, resolving escapes only when the scan found any.
    let materializeString (data: byte[]) (start: int) (length: int) (hadEscapes: bool) : string =
        if not hadEscapes then
            Encoding.UTF8.GetString(data, start, length)
        else
            let builder = StringBuilder(length)
            let endExclusive = start + length
            let mutable i = start
            let mutable segmentStart = i

            while i < endExclusive do
                if data[i] = byte '\\' then
                    if i > segmentStart then
                        builder.Append(Encoding.UTF8.GetString(data, segmentStart, i - segmentStart)) |> ignore

                    i <- i + 1

                    match data[i] with
                    | b when b = byte '"' -> builder.Append('"') |> ignore
                    | b when b = byte '\\' -> builder.Append('\\') |> ignore
                    | b when b = byte '/' -> builder.Append('/') |> ignore
                    | b when b = byte 'b' -> builder.Append('\b') |> ignore
                    | b when b = byte 'f' -> builder.Append('\f') |> ignore
                    | b when b = byte 'n' -> builder.Append('\n') |> ignore
                    | b when b = byte 'r' -> builder.Append('\r') |> ignore
                    | b when b = byte 't' -> builder.Append('\t') |> ignore
                    | b when b = byte 'u' ->
                        let codePoint =
                            ((hexValue data[i + 1]) <<< 12)
                            ||| ((hexValue data[i + 2]) <<< 8)
                            ||| ((hexValue data[i + 3]) <<< 4)
                            ||| (hexValue data[i + 4])

                        builder.Append(char codePoint) |> ignore
                        i <- i + 4
                    | _ -> decodeFailure "invalid escape sequence"

                    i <- i + 1
                    segmentStart <- i
                else
                    i <- i + 1

            if i > segmentStart then
                builder.Append(Encoding.UTF8.GetString(data, segmentStart, i - segmentStart)) |> ignore

            builder.ToString()

    let stringDecoder: Decoder<string> =
        fun src ->
            let struct (start, length, hadEscapes, next) = stringRaw src
            struct (materializeString src.Data start length hadEscapes, next)

    /// Finds the extent of a JSON number token.
    let numberToken (allowFractionAndExponent: bool) (src: ByteSource) =
        let src = skipWhitespace src

        if src.Offset >= src.Data.Length then
            decodeFailure "unexpected end of input"

        let data = src.Data
        let mutable i = src.Offset

        if data[i] = byte '-' then
            i <- i + 1

        if i >= data.Length then
            decodeFailure "expected digit"

        if data[i] = byte '0' then
            i <- i + 1

            if i < data.Length && isDigit data[i] then
                decodeFailure "leading zeroes are not allowed"
        elif isDigit data[i] then
            while i < data.Length && isDigit data[i] do
                i <- i + 1
        else
            decodeFailure "expected digit"

        if allowFractionAndExponent && i < data.Length && data[i] = byte '.' then
            i <- i + 1

            if i >= data.Length || not (isDigit data[i]) then
                decodeFailure "expected digit"

            while i < data.Length && isDigit data[i] do
                i <- i + 1

        if allowFractionAndExponent && i < data.Length && (data[i] = byte 'e' || data[i] = byte 'E') then
            i <- i + 1

            if i < data.Length && (data[i] = byte '+' || data[i] = byte '-') then
                i <- i + 1

            if i >= data.Length || not (isDigit data[i]) then
                decodeFailure "expected digit"

            while i < data.Length && isDigit data[i] do
                i <- i + 1

        struct (src.Offset, i - src.Offset, ByteSource(data, i))

    let intDecoder: Decoder<int> =
        fun src ->
            let struct (start, length, next) = numberToken false src
            struct (parseInt32Bytes src.Data start length, next)

    let int64Decoder: Decoder<int64> =
        fun src ->
            let struct (start, length, next) = numberToken false src
            struct (parseInt64Bytes src.Data start length, next)

    let floatDecoder: Decoder<float> =
        fun src ->
            let struct (start, length, next) = numberToken true src
            struct (parseFloatBytes src.Data start length, next)

    let decimalDecoder: Decoder<decimal> =
        fun src ->
            let struct (start, length, next) = numberToken true src
            struct (parseDecimalBytes src.Data start length, next)

    let boolDecoder: Decoder<bool> =
        fun src ->
            let src = skipWhitespace src
            let data = src.Data
            let remaining = data.Length - src.Offset

            if
                remaining >= 4
                && data[src.Offset] = byte 't'
                && data[src.Offset + 1] = byte 'r'
                && data[src.Offset + 2] = byte 'u'
                && data[src.Offset + 3] = byte 'e'
            then
                struct (true, ByteSource(data, src.Offset + 4))
            elif
                remaining >= 5
                && data[src.Offset] = byte 'f'
                && data[src.Offset + 1] = byte 'a'
                && data[src.Offset + 2] = byte 'l'
                && data[src.Offset + 3] = byte 's'
                && data[src.Offset + 4] = byte 'e'
            then
                struct (false, ByteSource(data, src.Offset + 5))
            else
                decodeFailure "expected true or false"

    /// Skips one complete JSON value of any shape.
    let rec skipValue (src: ByteSource) : ByteSource =
        let src = skipWhitespace src
        let data = src.Data

        if src.Offset >= data.Length then
            decodeFailure "unexpected end of input"

        match data[src.Offset] with
        | b when b = byte '"' ->
            let struct (_, _, _, next) = stringRaw src
            next
        | b when b = byte '{' ->
            let mutable current = skipWhitespace (src.Advance 1)
            let mutable continueLoop = true

            if current.Offset < data.Length && data[current.Offset] = byte '}' then
                current <- current.Advance 1
                continueLoop <- false

            while continueLoop do
                let struct (_, _, _, afterKey) = stringRaw current
                let afterColon = advancePastColon afterKey
                let afterValue = skipValue afterColon
                let struct (next, hasMore) = readSeparatorOrClose (byte '}') "}" afterValue
                current <- next
                continueLoop <- hasMore

            current
        | b when b = byte '[' ->
            let mutable current = skipWhitespace (src.Advance 1)
            let mutable continueLoop = true

            if current.Offset < data.Length && data[current.Offset] = byte ']' then
                current <- current.Advance 1
                continueLoop <- false

            while continueLoop do
                let afterValue = skipValue current
                let struct (next, hasMore) = readSeparatorOrClose (byte ']') "]" afterValue
                current <- next
                continueLoop <- hasMore

            current
        | b when b = byte 't' || b = byte 'f' ->
            let struct (_, next) = boolDecoder src
            next
        | b when b = byte 'n' ->
            if
                src.Offset + 3 < data.Length
                && data[src.Offset + 1] = byte 'u'
                && data[src.Offset + 2] = byte 'l'
                && data[src.Offset + 3] = byte 'l'
            then
                src.Advance 4
            else
                decodeFailure "expected null"
        | b when b = byte '-' || isDigit b ->
            let struct (_, _, next) = numberToken true src
            next
        | _ -> decodeFailure "unexpected token"

    let bytesEqual (expected: byte[]) (data: byte[]) (offset: int) (length: int) =
        if expected.Length <> length then
            false
        else
#if !FABLE_COMPILER
            System.MemoryExtensions.SequenceEqual(
                System.ReadOnlySpan<byte>(data, offset, length),
                System.ReadOnlySpan<byte>(expected)
            )
#else
            let mutable index = 0
            let mutable equal = true

            while index < length && equal do
                if expected[index] <> data[offset + index] then
                    equal <- false
                else
                    index <- index + 1

            equal
#endif

    let inline private needsStringEscape (c: char) = c = '"' || c = '\\' || int c < 32

    let private writeUnicodeEscape (writer: IByteWriter) (code: int) =
        let hexDigit value =
            if value < 10 then byte (int '0' + value) else byte (int 'a' + value - 10)

        writer.WriteByte(byte '\\')
        writer.WriteByte(byte 'u')
        writer.WriteByte(hexDigit ((code >>> 12) &&& 0xF))
        writer.WriteByte(hexDigit ((code >>> 8) &&& 0xF))
        writer.WriteByte(hexDigit ((code >>> 4) &&& 0xF))
        writer.WriteByte(hexDigit (code &&& 0xF))

    let writeEscapedString (writer: IByteWriter) (value: string) =
        let mutable index = 0
        let mutable doneFastScan = false

        while not doneFastScan && index < value.Length do
            if needsStringEscape value[index] then
                doneFastScan <- true
            else
                index <- index + 1

        writer.WriteByte(byte '"')

        if index = value.Length then
            writer.WriteString(value)
        else
            if index > 0 then
                writer.WriteStringSlice(value, 0, index)

            let mutable segmentStart = index

            let flushSegment escaped =
                if index > segmentStart then
                    writer.WriteStringSlice(value, segmentStart, index - segmentStart)

                writer.WriteString(escaped: string)
                segmentStart <- index + 1

            while index < value.Length do
                match value[index] with
                | '"' -> flushSegment "\\\""
                | '\\' -> flushSegment "\\\\"
                | '\b' -> flushSegment "\\b"
                | '\f' -> flushSegment "\\f"
                | '\n' -> flushSegment "\\n"
                | '\r' -> flushSegment "\\r"
                | '\t' -> flushSegment "\\t"
                | c when int c < 32 ->
                    if index > segmentStart then
                        writer.WriteStringSlice(value, segmentStart, index - segmentStart)

                    writeUnicodeEscape writer (int c)
                    segmentStart <- index + 1
                | _ -> ()

                index <- index + 1

            if index > segmentStart then
                writer.WriteStringSlice(value, segmentStart, index - segmentStart)

        writer.WriteByte(byte '"')

    // --- Layout pass -----------------------------------------------------------
    // Reformats already-valid, already-compact JSON: inserts indentation whitespace and, when asked, rewrites
    // non-ASCII or HTML-sensitive characters as `\uXXXX`. Runs once over the compact bytes; the compact encoder
    // above is never on this path.

    let private htmlSensitive (b: byte) =
        b = byte '<' || b = byte '>' || b = byte '&' || b = byte '\'' || b = byte '+'

    let private writeCodePointEscape (sink: IByteWriter) (codePoint: int) =
        if codePoint > 0xFFFF then
            let value = codePoint - 0x10000
            writeUnicodeEscape sink (0xD800 ||| (value >>> 10))
            writeUnicodeEscape sink (0xDC00 ||| (value &&& 0x3FF))
        else
            writeUnicodeEscape sink codePoint

    /// Decodes one UTF-8 scalar at `i`. On a truncated or malformed sequence it returns the lead byte with
    /// length 1 so the pass stays lossless on input it cannot interpret.
    let private decodeUtf8 (data: byte[]) (i: int) (count: int) : struct (int * int) =
        let b0 = int data[i]

        if b0 < 0x80 then
            struct (b0, 1)
        elif b0 >= 0xF0 && i + 3 < count then
            struct (
                ((b0 &&& 0x07) <<< 18)
                ||| ((int data[i + 1] &&& 0x3F) <<< 12)
                ||| ((int data[i + 2] &&& 0x3F) <<< 6)
                ||| (int data[i + 3] &&& 0x3F),
                4
            )
        elif b0 >= 0xE0 && i + 2 < count then
            struct (((b0 &&& 0x0F) <<< 12) ||| ((int data[i + 1] &&& 0x3F) <<< 6) ||| (int data[i + 2] &&& 0x3F), 3)
        elif b0 >= 0xC0 && i + 1 < count then
            struct (((b0 &&& 0x1F) <<< 6) ||| (int data[i + 1] &&& 0x3F), 2)
        else
            struct (b0, 1)

    /// Copies a string token starting at the opening quote `start`, applying the re-escaping options; returns the
    /// index just past the closing quote.
    let private copyStringToken
        (data: byte[])
        (start: int)
        (count: int)
        (sink: IByteWriter)
        (asciiOnly: bool)
        (escapeHtml: bool)
        : int =
        // A byte that can be copied verbatim as string content under the current options.
        let copyable (c: byte) =
            c <> byte '\\'
            && c <> byte '"'
            && (if c < 0x80uy then
                    not (escapeHtml && htmlSensitive c)
                else
                    not asciiOnly && not escapeHtml)

        sink.WriteByte(byte '"')
        let mutable i = start + 1
        let mutable closed = false

        while not closed && i < count do
            let b = data[i]

            if b = byte '\\' then
                if i + 1 < count then
                    let escaped = data[i + 1]

                    if escaped = byte 'u' && i + 5 < count then
                        sink.WriteBytesSlice(data, i, 6)
                        i <- i + 6
                    else
                        sink.WriteBytesSlice(data, i, 2)
                        i <- i + 2
                else
                    sink.WriteByte b
                    i <- i + 1
            elif b = byte '"' then
                sink.WriteByte b
                closed <- true
                i <- i + 1
            elif copyable b then
                let runStart = i
                i <- i + 1

                while i < count && copyable data[i] do
                    i <- i + 1

                sink.WriteBytesSlice(data, runStart, i - runStart)
            elif b < 0x80uy then
                // escapeHtml: a markup-sensitive ASCII byte
                writeCodePointEscape sink (int b)
                i <- i + 1
            elif asciiOnly then
                let struct (codePoint, length) = decodeUtf8 data i count
                writeCodePointEscape sink codePoint
                i <- i + length
            else
                // escapeHtml: a non-ASCII scalar; escape only the JS line separators
                let struct (codePoint, length) = decodeUtf8 data i count

                if codePoint = 0x2028 || codePoint = 0x2029 then
                    writeCodePointEscape sink codePoint
                else
                    sink.WriteBytesSlice(data, i, length)

                i <- i + length

        i

    /// Reformats the first `count` bytes of `source` (well-formed JSON) into `sink` per the write options.
    let formatJson (options: JsonWriteOptions) (source: byte[]) (count: int) (sink: IByteWriter) : unit =
        let indentUnit =
            match options.Indent with
            | JsonIndent.None -> [||]
            | JsonIndent.Spaces n -> Array.create (max 0 n) (byte ' ')
            | JsonIndent.Tab -> [| byte '\t' |]

        let newline =
            match options.LineEnding with
            | JsonLineEnding.Lf -> [| byte '\n' |]
            | JsonLineEnding.CrLf -> [| byte '\r'; byte '\n' |]

        let pretty = options.Indent <> JsonIndent.None
        let asciiOnly = options.AsciiOnly
        let escapeHtml = options.EscapeHtml
        let mutable depth = 0

        let newlineIndent () =
            sink.WriteBytes newline

            for _ in 1 .. depth do
                sink.WriteBytes indentUnit

        let mutable i = 0

        while i < count do
            let b = source[i]

            if b = byte '"' then
                i <- copyStringToken source i count sink asciiOnly escapeHtml
            elif isWhitespaceByte b then
                i <- i + 1
            elif b = byte '{' || b = byte '[' then
                let closeByte = if b = byte '{' then byte '}' else byte ']'
                let mutable j = i + 1

                while j < count && isWhitespaceByte source[j] do
                    j <- j + 1

                if j < count && source[j] = closeByte then
                    sink.WriteByte b
                    sink.WriteByte closeByte
                    i <- j + 1
                else
                    sink.WriteByte b
                    depth <- depth + 1
                    if pretty then newlineIndent ()
                    i <- i + 1
            elif b = byte '}' || b = byte ']' then
                depth <- max 0 (depth - 1)
                if pretty then newlineIndent ()
                sink.WriteByte b
                i <- i + 1
            elif b = byte ',' then
                sink.WriteByte b
                if pretty then newlineIndent ()
                i <- i + 1
            elif b = byte ':' then
                sink.WriteByte b
                if pretty then sink.WriteByte(byte ' ')
                i <- i + 1
            else
                sink.WriteByte b
                i <- i + 1

        if options.TrailingNewline then
            sink.WriteBytes newline
