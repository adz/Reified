# How `field _.Email` recovers a wire name

`field _.Email` derives the wire name `"email"` from the property. The mechanism differs per target, and the two
paths are easy to break independently, so this records why each is written the way it is.

Both live in `GetterName.split` (`src/Reified.Schema/Shape.fs`), selected by `#if FABLE_COMPILER`.

## Why `field` is a type and not a function

Two F# rules force it:

- Module `let` bindings cannot be overloaded (`FS0037`).
- `[<ReflectedDefinition>]` auto-quotation — what turns the argument `_.Email` into an `Expr` at the call site —
  applies to **member** parameters only. On a module `let` the argument is checked as an ordinary lambda against
  `Expr<_>` and fails with `FS0002`.

A constructor is a member, so it auto-quotes, and a type's name is in scope through an ordinary `open`. Declaring
`field` as a type is therefore what lets the call site stay unqualified with a single `open Reified.SchemaDSL`.

Do not try to move it back to a module `let`: that shape cannot be called as `field _.Name` at all, and passing it
an explicit quotation compiles but throws, because the quotation then carries no `WithValue` node.

`fieldAs` has one shape and needs no quotation, so it stays an ordinary function in `Syntax`.

## Why the two paths differ

On .NET the constructor uses `ReflectedDefinition(includeValue = true)`, so the quotation carries the
already-compiled getter and `WithValue` hands it straight back. Nothing is evaluated at run time and no reflection
is performed, which is what keeps the AOT and trimming guarantees in `docs/95-notes/82-aot-trimming-fable.md` true.

Fable does not implement `Expr.WithValue`. Using `includeValue = true` there fails to compile:

```
Microsoft.FSharp.Quotations.FSharpExpr.WithValue (static) is not supported by Fable
```

So the Fable constructor takes a plain `ReflectedDefinition` and matches the lambda without the `WithValue`
wrapper. Without `includeValue` the quotation carries no compiled function, so the getter has to be rebuilt — but
it is built as a direct field read, not by evaluating the quotation.

Two Fable traps worth knowing before editing that branch:

- **The matched `property` is not a `PropertyInfo`.** Fable represents that slot as the property-name string,
  hence the `unbox<string>`. Calling `.Name` on it compiles and yields nonsense.
- **Do not reach for `LeafExpressionConverter.EvaluateQuotation`.** It works and it is the obvious thing to write,
  but it returns an *interpreter closure*: Fable's `evaluate` compiles `ExprLambda` to
  `(arg) => { const newEnv = new Map(capturedEnv); ...; return evaluate(expr.body, newEnv); }`, so every call
  allocates a `Map` and re-walks the expression tree. A schema getter runs per parsed and per encoded value, so
  that would put an interpreter on the hot path — exactly what the guarantee in
  `docs/95-notes/82-aot-trimming-fable.md` rules out.

  Instead the property name recovered above is closed over and read directly, through the same
  `[<Fable.Core.Emit("$0[$1]")>]` pattern `Reified.Data`'s JSON interop uses. The emitted JavaScript is
  `(model) => (model[propertyName])`, and `evaluate` no longer appears anywhere in Reified's Fable output.

Like the .NET path, the quotation is therefore read once while the schema value is built, and the getter itself is
a plain field access on both targets.

## Version and target requirements

Quotation support arrived in Fable 5, and the property preservation `_.Name` depends on landed in 5.10.

| Fable target | Derived `field _.Email` | Minimum |
| --- | --- | --- |
| JavaScript, TypeScript, Python, BEAM | Yes | 5.10 |
| Dart | Yes | 5.13 |
| Rust, PHP | No — `fieldAs` only | — |

`.config/dotnet-tools.json` pins **5.13**. Do not lower it below 5.10 without removing the Fable path, and do not
go looking for a quotation compiler flag — there is none. The `fable-compiler-quotations` package from
[Fable PR #1839](https://github.com/fable-compiler/Fable/pull/1839) is an obsolete 2019 experimental fork against
Fable 2; it was never merged and must not be installed.

## What guards this

the `Fable` FAKE target compiles the Fable surface and runs it on Node.
`examples/Reified.FableProbe/Checks.fs` declares its schema with `field _.Name` / `field _.Age`, and
`Program.fs` asserts `planSummary = [ "0:name"; "1:age" ]` — so a regression in either the name derivation or the
rebuilt getter fails the check rather than silently producing wrong wire names.

the `NativeAot` FAKE target executes the .NET path natively under NativeAOT for the same reason.

## Deliberate limits

- Only a direct property getter is accepted. Arbitrary expressions raise, pointing at `fieldAs`.
- Custom wire names stay `fieldAs`; the derived form applies the camelCase policy and never transforms an
  explicit name.
- Rust and PHP are not promised the derived form.
