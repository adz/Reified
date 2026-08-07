# Typed Relational Layer

An immutable, generated, typed relational AST — a *value* describing SQL, with generated table metadata and
row codecs on either side of it. It should not resemble EF's tracked object graph, and it should not copy Ecto's
Changeset wholesale, because Reified already has Schema and path-aware diagnostics.

**Scope: constructing SQL and mapping rows, not running them.** The interesting and durable half is the part
Reified can own — describing a statement as a composable value, generating table and column metadata from a real
catalog, decoding rows into domain types without reflection, and translating database constraint violations into
the same diagnostics vocabulary a schema parse produces. Execution is a solved, unremarkable problem with many
existing answers, and it is the half that would drag an effect system into a description library.

So a `Statement<'result>` is a plain value. Something else runs it: an effect system, `Dapper`, or ordinary ADO.NET
in a `task { }`. The execution signatures sketched below are illustrative of *shape* only — read them as "some
executor consumes this value and produces rows", not as an API this repository would ship. Whoever owns execution
owns connection lifetime, transactions, cancellation, and streaming.

  The result could be genuinely compelling: database-first correctness like SqlHydra, query composition closer to
  Ecto, and immutable F# values throughout.

  ## Where Reified is currently up to

  The existing architecture gives SQL a solid foundation:

  - `Schema<'model>` describes domain shape, constraints, construction, field metadata, parsing, and checking.
  - `Schema.parse` and `Schema.check` return the ordinary admitted value rather than a universal trust wrapper,
    with accumulated path-aware `SchemaErrors` on failure.
  - Schema retains typed construction information and avoids reflection in hot paths. SQL should follow the same
    pattern — the record-plan compiler behind `Json.compile` is the precedent for generated row decoders.
  - `Path` and `SchemaError` already give constraint translation somewhere to land, so a unique-violation can be
    reported at the field that caused it rather than as a driver exception.

  The package dependency direction matters:

  Reified.Schema
        |
   Reified.Sql          // metadata, immutable AST, row codecs — no I/O
        |
  Postgres / Sqlite     // dialect rendering and type mappings

  Schema must not learn about SQL, and `Reified.Sql` must not learn about executing anything.

  ## Three possible interface designs

  ### 1. Minimal/deep interface

  Expose only three conceptual values:

  type Table<'row, 'key>
  type Expr<'scope, 'value>
  type Statement<'result>

  Generated table modules produce immutable statements, and an executor consumes them:

  module Db =
      val run : Statement<'value> -> (* executor-owned result *)

  Usage:

  let activeAdults name =
      Users.query
      |> Query.where (fun u -> u.Active &&. u.Age >=. Expr.value 18)
      |> Query.whereOption name (fun name u ->
          u.Name |> Expr.ilike (Expr.value $"%{name}%"))
      |> Query.orderByDescending (fun u -> u.CreatedAt)
      |> Query.select (fun u ->
          select {
              field u.Id
              field u.Name
          })
      |> Query.toList

  `activeAdults` is a value. It can be built, composed, inspected, and rendered to SQL without a connection
  existing — which is also what makes it testable without a database.

  This design hides virtually everything: query versus command execution, cardinality, row decoding, parameter binding, dialect rendering, and statement
  caching.

  Its strength is conceptual depth. Its weakness is that `Statement<'result>` can conceal meaningful distinctions
  between query cardinality, streaming, and mutation.

  ### 2. Full relational AST

  Expose the underlying distinctions: tables, typed columns, scoped expressions, projections, queries, and mutation
  commands. This surface can represent joins and subqueries, `EXISTS`, grouping and aggregates, set operations,
  conflict clauses, and provider-specific extensions.

  A complex query might read:

  ```fsharp
  query {
      for customer in Customers.table do
      join order in Orders.table on (customer.Id =. order.CustomerId)
      where (
          customer.TenantId =. Expr.value tenant
          &&. order.Total >=. Expr.value minimum
      )
      groupBy (customer.Id, customer.Email)
      having (Expr.count order.Id >. Expr.value 2L)
      sortByDescending (Expr.sum order.Total)
      select
          {| CustomerId = customer.Id
             Email = customer.Email
             Spend = Expr.sum order.Total |}
  }
  ```

  This is the most complete design, but it risks exposing too many types and making straightforward CRUD feel like constructing a compiler AST.

  ### 3. Ecto-inspired design

  Organize the library around generated schemas, queries, mutations and a database service:

  type Query<'row>
  type Insert<'result>
  type Update<'result>
  type Delete<'result>
  type ConstraintRef<'input>

  Generated table modules provide the everyday operations:

  module Customer =
      val schema : Schema<Customer>
      val createSchema : Schema<NewCustomer>

      val table : Table<Customer, CustomerId>
      val id : Column<Customer, CustomerId>
      val email : Column<Customer, Email>

      val emailUnique : ConstraintRef<NewCustomer>

      val query : Query<Customer>
      val insert : NewCustomer -> Insert<Customer>
      val update : CustomerId -> Update<Customer>
      val delete : CustomerId -> Delete<bool>

  The analogue of an Ecto changeset is not one monolithic object. It is the composition of:

  1. an input value admitted by `Schema.parse` or `Schema.check`;
  2. an immutable Insert or Update;
  3. explicit constraint translations attached to the mutation.

  That separation fits Reified much better than copying Changeset.

  ## Recommended synthesis

  Use the Ecto-shaped generated surface over the full relational AST, with the minimal Db.run idea selectively retained.

  The public layers would be:

  type Query<'value>
  type Insert<'value>
  type Update<'value>
  type Delete<'value>

  These are the types `Reified.Sql` would own. The executor-side verbs — `all`, `tryExactlyOne`, `exactlyOne`,
  `stream`, `insert`, `update`, `delete` — belong to whatever runs them, and their result type is that executor's
  concern.

  Separate verbs beat overloading everything into one `run`, because cardinality and intent stay visible at the
  call site. All of them can still share one internal `Statement<'result>` representation, which is the piece
  `Reified.Sql` actually defines.

  ## Immutable writes

  Writes should never involve tracking or mutable entity state.

  let rename customerId name expectedVersion =
      Customer.update customerId
      |> Update.set Customer.name name
      |> Update.where (Customer.version =. Expr.value expectedVersion)
      |> Update.increment Customer.version
      |> Update.returningRow

  Nullable updates must distinguish “unchanged” from “set to null”:

  type Assignment<'value> =
      | Unchanged
      | Set of 'value
      | SetNull

  Generated patch records could use that representation:

  type CustomerPatch =
      {
          Name: Assignment<string>
          Phone: Assignment<string>
      }

  Bulk inserts, upserts, deletes and conflict handling should all produce immutable command values.

  ## Query ergonomics

  The expression system should be explicit rather than translating arbitrary F# quotations:

  customer.Active =. Expr.value true
  customer.Email |> Expr.ilike pattern
  Expr.exists openOrders
  customer.DeletedAt |> Expr.isNull
  Expr.coalesce customer.Nickname customer.Name

  That means:

  - unsupported operations fail at compile time;
  - nothing silently becomes client-side evaluation;
  - the AST is AOT-safe and trimming-safe;
  - parameterization is guaranteed;
  - query values are reusable and branchable.

  Both a pipeline API and a query computation expression should lower into the same AST. The CE is syntax, not a second query implementation.

  Complex queries should compose as ordinary values:

  let forTenant tenant query =
      query
      |> Query.where (fun row ->
          row.TenantId =. Expr.value tenant)

  let active =
      Customers.query
      |> Query.where (fun row ->
          row.Active =. Expr.value true)

  let activeForTenant tenant =
      active |> forTenant tenant

  ## Generated database modules

  Generation should emit physical database metadata separately from Schema metadata:

  module Customer =
      type Row =
          {
              Id: CustomerId
              Email: Email
              Name: string
              Version: int64
          }

      type New =
          {
              Email: Email
              Name: string
          }

      val schema : Schema<Row>
      val newSchema : Schema<New>

      val table : Table<Row, CustomerId>

      val id : Column<Row, CustomerId>
      val email : Column<Row, Email>
      val name : Column<Row, string>
      val version : Column<Row, int64>

      val primaryKey : Key<Row, CustomerId>
      val emailUnique : ConstraintRef<New>

      val query : Query<Row>
      val insert : New -> Insert<Row>

  Not every table row should automatically become a domain model. The generator should distinguish:

  - database row records;
  - insert/input records;
  - explicit mappings to hand-authored domain types.

  Schema can describe row and input shapes, but SQL metadata must remain responsible for:

  - keys;
  - foreign keys;
  - indexes;
  - defaults;
  - identities and generated columns;
  - database nullability;
  - database types;
  - named constraints;
  - provider annotations.

  A Schema<'t> is not automatically a relational mapping: nested objects, unions, collections and maps require explicit JSON, array or related-table
  mappings.

  ## Constraint errors and Schema diagnostics

  This is where Reified can be unusually good.

  type DbError =
      | Constraint of ConstraintViolation
      | Cardinality of CardinalityError
      | Decode of DecodeError
      | Unsupported of feature: string * dialect: string
      | Connection of DbFault
      | SerializationFailure of DbFault
      | Deadlock of DbFault
      | Provider of DbFault

  type ConstraintViolation =
      {
          Kind: ConstraintKind
          Name: string option
          Table: string option
          Columns: string list
          Diagnostics: SchemaErrors option
          Detail: string option
      }

  Generated ConstraintRefs connect a database constraint to the schema `Path` its violation should be reported at:

  Customer.emailUnique
  // DB constraint name + affected columns + the path "email"

  Path is the right currency because it is already what `SchemaErrors` carries and what problem-details rendering
  consumes, so a unique-violation lands in exactly the place a parse failure on the same field would.

  However, conversion to Schema diagnostics must be explicit and semantic. Not every persistence failure is invalid input:

  - unique email: normally applicable to the email field;
  - not-null: applicable when the input schema owns that field;
  - check constraint: applicable only when it corresponds to an understood Schema constraint;
  - foreign key: sometimes a field error, sometimes a domain conflict;
  - serialization/deadlock/connection failures: never Schema errors.

  An Ecto-style mapping could look like:

  let command =
      Customer.insert customer
      |> Insert.mapConstraint
          Customer.emailUnique
          CustomerNew.email
          (SchemaError.custom
              "email.taken"
              "This email is already registered.")

  Execution could then return:

  type WriteError =
      | Invalid of SchemaErrors
      | Database of DbError

  Or the lower-level API can always return DbError, leaving the application to map it:

  let! customer =
      Customer.insert model
      |> Database.insert
      |> Bind.mapError (function
          | DbError.Constraint violation
              when violation.Diagnostics.IsSome ->
              RegistrationError.Invalid violation.Diagnostics.Value

          | error ->
              RegistrationError.Storage error)

  The database remains authoritative, which handles race conditions correctly.

  ## Dump, interrogation and generation

  Your proposed pipeline is the right one:

  schema.sql / migrations
            │
            ▼
   disposable real database
            │
            ▼
   catalog interrogation
            │
            ▼
   versioned .reifieddb.json snapshot
            │
            ▼
   deterministic F# source generation

  I would not initially skip database initialization and introspection.

  Parsing full PostgreSQL DDL correctly means understanding:

  - search paths;
  - domains and enums;
  - extensions;
  - casts;
  - generated expressions;
  - quoted identifiers;
  - provider type resolution;
  - version-specific grammar.

  The database engine is already the correct parser and semantic analyzer. SQLite is simpler, but its original DDL still contains information that PRAGMAs
  do not always expose cleanly.

  Commands could be:

  reified sql import --provider postgres schema.sql --output db.reified.json
  reified sql snapshot --provider sqlite app.db --output db.reified.json
  reified sql generate db.reified.json
  reified sql verify db.reified.json --connection ...

  Normal builds consume the checked-in snapshot and never need a live database. This gives:

  - hermetic builds;
  - reviewable schema changes;
  - deterministic generation;
  - CI drift checking;
  - a stable provider-neutral catalog format.

  A direct SQL parser can later become an optimization, not an architectural dependency.

  ## Provider expansion

  Each provider implements internal service-provider interfaces roughly equivalent to:

  - catalog reader;
  - type registry and codecs;
  - AST compiler/dialect renderer;
  - parameter binder;
  - exception classifier;
  - migration renderer;
  - feature capabilities;
  - optional bulk-copy implementation.

  The public AST represents relational semantics. Provider-specific features remain typed extensions:

  Postgres.Json.pathText Customer.metadata [ "address"; "city" ]
  Postgres.Array.contains Customer.tags tag
  Sqlite.Fts5.matches Search.content query

  Running a query against an unsupported dialect should fail during compilation, before database I/O.

  ## Transactions — out of scope, and deliberately so

  Transactions are the clearest example of why execution is not this library's half. They need lexical scoping,
  rollback on failure, defect, and cancellation, savepoints for nesting, and — the genuinely hard part — a way for
  nested repository calls to reach the transaction-bound connection without hiding it in ambient state.

  Every one of those is a property of the *executor*, and a statement value has nothing to say about them. A
  library that describes SQL should not grow a transaction scope; the host that opens connections already has one.

  ## Performance model

  Internally:

  1. Normalize the immutable relational AST.
  2. Validate dialect capabilities.
  3. Compile it into parameterized SQL and an ordered parameter plan.
  4. Attach a generated typed row decoder.
  5. Cache by AST shape and dialect, never parameter values.

  Generated readers should construct records directly from ordinals, following Schema/Codec’s retained typed-chain approach:

  - no per-row reflection;
  - no obj array constructor dispatch;
  - no expression compilation required at runtime;
  - no mutable tracking;
  - sequential reader support;
  - optional prepared statements.

  Cancellation and streaming are executor concerns and are not listed here. Steps 1–4 are pure functions over the
  AST; only step 5's cache is stateful, and it keys on AST shape and dialect, never on parameter values.

  ## Package layout

  Reified.Sql             // metadata, immutable AST, row codecs
  Reified.Sql.Postgres    // dialect rendering and type mappings
  Reified.Sql.Sqlite
  Reified.Sql.Tooling
  Reified.Sql.Generator   // catalog-driven generation

  None of these execute anything, so none of them depend on an effect system, a driver, or `System.Data`. An
  execution adapter is a separate package in whichever repository owns the effect model — and if it never gets
  written, the query construction and mapping halves are still independently useful, which is the test that this
  split is the right one.

  ## What makes it preferable

  The differentiating combination is:

  - immutable, reusable query and mutation values;
  - no DbContext, unit of work, proxies or tracking;
  - generated, refactor-safe columns and constraints;
  - full relational query composition;
  - explicit SQL semantics rather than quotation magic;
  - provider-neutral core with typed provider extensions;
  - real-catalog-driven generation;
  - direct generated codecs;
  - Schema-native, path-aware persistence diagnostics;
  - explicit race-safe constraint translation;
  - and no opinion at all about how the statement is executed.

  ## Proposed sequencing

  1. Prove generated Table/Column/RowCodec metadata from a SQLite catalog snapshot.
  2. Implement select/where/order/limit plus typed projections and direct decoding.
  3. Add inserts, updates, deletes and RETURNING.
  4. Add PostgreSQL rendering, codecs and constraint classification.
  5. Add explicit constraint-to-diagnostics mappings.
  6. Add joins, subqueries, grouping, aggregates and CTEs.
  7. Add compiled query caching and bulk statement construction.
  8. Add provider extensions, drift verification and migration support.

  The biggest prototype risks are typed anonymous-record projections and alias/scope typing across complex joins.
  Both are pure type-level problems in the AST, so both can be proven without a database connection — which is a
  further argument for keeping execution out.
