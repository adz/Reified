# Documentation: open questions

The task-folder reorganisation and FsLiveDocs migration these questions came from are both done — see
`dev-docs/decisions/README.md`, 2026-08-17. These two questions outlived that work and are still open.

1. **Is `Data` a Foundation or a Schema satellite?** It exists because building maps of lists by hand in
   tests and docs was miserable, which is a testing story. But it may be the easiest package to adopt
   first, which argues for prominence.
2. **Does a plain-ASP.NET serving path need a package?** Reified declares contracts and emits OpenAPI but
   ships no server. `09-testing`/`08-http-contracts` cover the client side; if the manual wiring for serving
   on plain ASP.NET turns out to be boilerplate people copy every time, it earns a package. If not, a page is
   the whole answer.
