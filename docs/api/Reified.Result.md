# `Result`

`Reified.Result` composes the standard F# `Result<'value,'error>`; it never wraps or replaces that type. The collection
combinators apply a fallible operation across a sequence, stopping at the first error or collecting every one.

## Collection traversal signatures

All four take any `seq<_>` and produce a **list**:

```fsharp no-check reason="Shows the functions' type signatures, not a compilation unit."
Result.traverse    : ('a -> Result<'b, 'e>) -> seq<'a> -> Result<'b list, 'e>
Result.sequence    : seq<Result<'a, 'e>> -> Result<'a list, 'e>
Result.traverseAll : ('a -> Result<'b, 'e>) -> seq<'a> -> Result<'b list, 'e list>
Result.sequenceAll : seq<Result<'a, 'e>> -> Result<'a list, 'e list>
```

`traverse` maps each item with a `Result`-returning function and stops at the first failure; `sequence` is
`traverse id` over a sequence of results. `traverseAll` and `sequenceAll` run every mapping in input order and return
one error per failing item, in input order. Nothing is flattened: a mapping that itself returns `Result<_, 'error list>`
produces a list of lists, so map it down first.

All four convert the input to a list up front, so a lazy sequence is fully forced and an infinite one will not
terminate through any of them.

See [Working with collections](/result-handling/collections.html) for the walkthrough.
