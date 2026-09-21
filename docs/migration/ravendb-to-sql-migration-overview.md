# Moving data from RavenDB to SQL

> [!IMPORTANT]
> **This page describes the designed behaviour, not what you can run today.** One migration command is built, `--migration-source-report`, and two of the eighteen categories can be copied. Sections marked **Planned** describe code that has no call path yet. [The instructions](ravendb-to-sql-migration-instructions.md) tell an operator what works today, and [what is built but not yet on a call path](ravendb-to-sql-migration-system-design.md#what-is-built-but-not-yet-on-a-call-path) is the authoritative list for a developer.

## Problem

A customer can already point ServiceControl at SQL Server or PostgreSQL. They cannot bring their existing data with them.

This covers the error instance only. The audit instance has no SQL persister, so a customer who finishes this migration still runs RavenDB for audit.

## Strategy

- Switch over first, and copy only what has to be copied. Retention does most of the work. Error retention is between 5 and 45 days, and event retention defaults to 14 days, so most of the source ages out on its own within weeks. That is why archived and resolved messages are optional rather than required. Retention would have deleted them anyway. An operator can raise event retention as far as 200 days, which is longer than error retention can ever be, so check the two settings rather than assuming the event log is the shorter one.
- The required set is small because unresolved failures are the only category a customer can act on, and the strategy assumes customers keep that number low by resolving and archiving. **A neglected instance breaks that assumption**: unresolved failures can legitimately be months old, and a large backlog of them makes the closed window long rather than short. The dry run is what tells a customer which case they are in.
- Anything not selected simply ages out of RavenDB, and the customer deletes the old database when they are ready.

## Goals

- **Minimal downtime**. Only the required data copies with ServiceControl closed. Optional data copies in the background while it serves traffic.
- **All three RavenDB sources are supported**. Embedded, a container, or RavenDB Cloud, on one code path rather than three.
- **No writes through the client**. The copier never changes the source, but RavenDB's own expiration does. The primary database already has expiration configured, and the sweep keeps deleting failed messages and event log items throughout the migration, and for as long afterwards as the instance is left running. So the old database is a fallback that degrades from the moment you start, and a customer who wants a clean fallback has to back it up first.
- **Abandonable up to a known point, and only up to that point**. While ServiceControl is closed the copy can be thrown away at no cost, because nothing but the copier has written to SQL and the migration has written nothing to RavenDB. See [the free abort, and the moment it closes](#the-free-abort-and-the-moment-it-closes). Once the host opens there is no way back at all.
- **No duplicates and no gaps**. Rows and the resume cursor commit in one transaction, so a crash needs no reconciliation.
- **Every identifier anything depends on is carried across**. The event log and historic retry operations are renumbered, because nothing references their keys.
- **Refuse rather than half-migrate**. Every check runs before the first row moves, and a failure is a host that will not start.
- **No silent loss**. A migration cannot end with a selected category still in progress or halted. Each one must be finished or explicitly abandoned. Abandoning is a deliberate choice, and an abandoned category lets the host open. Skipped rows are counted and reported.
- **Bounded impact on a live instance**. The copy is streamed, so memory does not track the size of the database, and a configurable pause sits between the batches of an optional category.
- **Known before it starts, visible while it runs**. A dry run reports what will move and how long ServiceControl is closed, and every category transition is reported as it happens.
- **Use existing functionality where possible**. Progress is planned to go through custom checks and the activity feed, so no new client or screen is needed.

## Deliberately not built, and not currently planned

- **Zero downtime.** The required data is copied with ServiceControl closed, so there is a real, if short, outage.
- **Reversible once ServiceControl opens.** Nothing copies SQL rows back to RavenDB, so once the host has served traffic there is no rollback of any kind.
- **Steerable while running.** No pause, resume, or abort. Changing anything means editing configuration and restarting.
- **A general-purpose migration tool.** The source is always RavenDB and the target is always a ServiceControl EF Core persister, both at versions this build can read.
- **Custom migration UI via ServicePulse.** Custom checks and the event log will be used for progress reporting, but migration configuration and migration engine control will not be available via the UI.

## What works today

Two categories have both a reader and a writer, `KnownEndpoints` and `EndpointSettings`. The other sixteen throw on both sides, and `RavenMigrationSource.ReadBody` throws for every category, so no message body has ever been copied. `EveryRequiredCategoryCanBeCopiedCheck` therefore refuses every real migration before a row moves. One command is built, `--migration-source-report`, and it prints the source facts plus a document count per RavenDB collection. Everything else on this page is design.

## Supported migration scenarios

The copier runs inside the ServiceControl host, so every row and every message body travels from the source, through ServiceControl, to the target. There is no database-to-database transfer, no backup and restore, and no replication. Whether a combination works therefore comes down to whether the ServiceControl host can reach both ends at once, and how long it takes comes down to how far the data has to travel.

**Supported locations:**

- **The RavenDB source**: embedded on the ServiceControl host (Windows installations only, see below), self-hosted on the same network in a container, VM or bare metal, or RavenDB Cloud
- **The SQL target**: SQL Server or PostgreSQL on the ServiceControl host, elsewhere on the same network, in a container, or as a managed cloud service such as Azure SQL, Amazon RDS or Google Cloud SQL
- **Any combination of the two**, subject to the requirements below

**By where the data has to travel:**

- **On-prem to on-prem.** The common case and the fastest. Embedded or self-hosted RavenDB to SQL on the same host or the same network.
- **On-prem to cloud.** RavenDB on the network, managed SQL in a cloud. Works, but each batch is one round trip, so write latency multiplies by the number of batches rather than being amortised away.
- **Cloud to on-prem.** RavenDB Cloud down to local SQL. Works, and the customer pays egress on everything copied, most of which is archived messages and their bodies.
- **Cloud to cloud.** Works, and is only sensible when ServiceControl runs alongside one of them. A host sitting on-prem between two clouds pulls every byte down and pushes it straight back up.
- **The message body store is a third location.** Bodies come out of RavenDB attachments and go wherever the target is configured to put them: a filesystem, Azure Blob or S3, with small text bodies kept inline in the database. That decision is made at the same time as the database move.

**Infrastructure requirements:**

- The ServiceControl host needs network access to the RavenDB source, the SQL target and the body store simultaneously
- Both RavenDB databases, primary and throughput, on one server or cluster. The migration opens both through a single `IDocumentStore`, and so does the licensing component (`LicensingDataStore.cs:36`)
- A SQL Server target must have Full-Text Search installed. The `AddFullTextSearch` EF Core migration checks `SERVERPROPERTY('IsFullTextInstalled')` and fails if it is absent, because message search is not optional. An EF Core migration runs once, so this is checked the first time `--setup` brings a database up to that migration, not on every `--setup`. A stock SQL Server container image does not include Full-Text Search. PostgreSQL needs nothing extra, since its index is a GIN over `to_tsvector`
- A managed target's transient failures are already survivable: retry on failure is on by default and no settings reader binds the flag that would turn it off

**Not supported:**

- Any server-to-server copy: no backup and restore, no RavenDB ETL or replication into SQL, no external data pipeline
- A host that can reach only one of the two databases at a time, so no staged move by way of an offline copy
- **An embedded RavenDB source when ServiceControl runs in a container.** Reading an embedded database means starting a RavenDB server process, and the container image does not carry one: `ServiceControl.Persistence.RavenDB.csproj:36` excludes the `RavenDBServer` directory from the artifact, and the copy that would restore it at `:44` is skipped when `CI` or `WindowsSelfContained` is set. The Dockerfile sets `CI`. A containerised instance migrating away from embedded RavenDB has to point at an external RavenDB server rather than at a data directory. Windows installations are unaffected: the installer unzips the server unconditionally
- Primary and throughput RavenDB databases in different locations
- Anything but RavenDB as the source, or anything but a ServiceControl EF Core persister as the target

## Migration workflow

**Planned.** Of the steps below, only 1, 3 and 5 can be carried out today, and step 5 ends in a refusal. Steps 4, 8 and 9 name commands and background work that do not exist.

1. Upgrade ServiceControl as normal, still on RavenDB.
2. Set four things in configuration: the new `ServiceControl/PersistenceType`, its connection string, `ServiceControl/Migration/Enabled=true`, and which [optional data](#optional) they want copied.
3. Run `--setup` to create the SQL schema. It fails against a SQL Server instance without Full-Text Search installed.
4. Run the [dry run](#dry-run). It reports what it resolved as a source, what each category holds, and an estimate of how long ServiceControl will be closed. Read [what the dry run reports](#dry-run) before booking an outage around its estimate.
5. Start ServiceControl with `ServiceControl/Migration/Enabled=true`.
6. Every check runs before a single row moves. If one fails the host does not start and names which, having copied nothing, so a wrong database name or unconfigured body storage costs a restart rather than a half-finished migration.
7. The copying of [required data](#required) starts, with ServiceControl still closed. The copy runs inside that same start, before the API begins listening and before any background service runs. This is assumed to be a small amount of data.
8. ServiceControl opens by itself the moment the required copy finishes, with no second restart to perform, and whatever [optional data](#optional) they asked for is copied in the background while the instance runs normally. They can watch it from ServicePulse custom checks and events, but not steer it.
9. They run the verification pass once the background copy has completed. It reports row counts on both sides category by category, accounting for deliberate skips so a difference is explained rather than reported as a fault. They then set `ServiceControl/Migration/Enabled=false` and restart. Verification tolerates more rows in SQL than in RavenDB, because RavenDB keeps expiring rows the copier already took.
10. RavenDB data can be removed.

- If `ServiceControl/Migration/Enabled=false` is set while a selected category is still incomplete, the startup is gated: it refuses and names exactly what is outstanding, or, where the [free abort](#the-free-abort-and-the-moment-it-closes) is still open, starts with a warning that says so. See [turning migration mode off is a gated startup too](#turning-migration-mode-off-is-a-gated-startup-too).
- A category that ended `CompleteWithErrors` counts as complete and does not block, though its skipped count is printed so the loss is stated rather than silent.
- `ServiceControl/Migration/AllowIncompleteExit` exists for a customer who has changed their mind and accepts leaving data behind. It marks the outstanding categories as abandoned, which is a deliberate end state rather than a failure, so the progress check settles and the guard stays armed for any later migration.
- **Steps 5 to 7 are the free abort window**, which is not the same thing as `ServiceControl/Migration/AllowIncompleteExit`. See [the free abort, and the moment it closes](#the-free-abort-and-the-moment-it-closes).
- A category that stops because too many rows failed is `Halted`, and it stays that way until someone acts. Fix the cause and restart to carry on from where it stopped, or abandon it deliberately if you accept the loss. See [a halt stops one category, and clearing it is a restart](#a-halt-stops-one-category-and-clearing-it-is-a-restart).

## Architecture

```mermaid
flowchart TB
    cfg["Configuration + restart<br/>the only way to change anything"]
    checks["Custom checks + activity feed<br/>planned: progress is log output today"]

    subgraph host["One ServiceControl host process, started with ServiceControl/Migration/Enabled = true"]
        direction LR
        raven["RavenDB persister<br/>own AssemblyLoadContext<br/>read-only source lifecycle"]
        engine["MigrationEngine<br/>categories, throttle,<br/>dry run, verification"]
        target["EF Core persister<br/>SQL Server or PostgreSQL<br/>own AssemblyLoadContext"]
        raven -->|"IMigrationSource"| engine
        engine -->|"IMigrationTarget"| target
    end

    old[("Old RavenDB<br/>read only, never written to")]
    sql[("SQL Server or PostgreSQL<br/>plus a new checkpoint table")]
    bodies[("Message body store<br/>filesystem, Azure Blob or S3")]

    cfg --> host
    host --> checks
    old --> raven
    target --> sql
    target --> bodies
```

- **The engine and the host know no store.** They deal in categories, cursors and counts. The source maps a category to what it reads and describes itself as labelled facts, the target maps a category to where it writes and how to count it, and each contributes its own startup checks. RavenDB to SQL is the only pair built, and another pair, such as SQL to RavenDB, would add a source or a target without changing the engine.
- **The source reads the instance's own RavenDB settings**, so an existing customer sets nothing new. Leave them in place when switching `ServiceControl/PersistenceType`.
- **Both persisters load into the same process**, each into its own `AssemblyLoadContext`, and both are live at once during the copy.
- **The engine references neither assembly.** It knows only `IMigrationSource` and `IMigrationTarget`, and treats the resume cursor as an opaque value it passes from one to the other, so it can be tested against fakes on either side.

## Startup sequence

```mermaid
flowchart TB
    A["Restart"] --> T{"Can the checkpoint<br/>table be read?"}
    T -->|"No: the schema predates this feature"| U["Host does not start.<br/>Says to run --setup first."]
    T -->|"Yes"| M{"Migration/Enabled?"}

    M -->|"On"| B["Open the SQL target, exactly as today"]
    B --> C["Open the old RavenDB, read only"]
    C --> D{"All checks pass?"}
    D -->|"No"| E["Host does not start.<br/>Says which check failed.<br/>Nothing has been copied."]
    D -->|"Yes"| F["Copy what cannot be recreated.<br/>Minutes. The API is not listening yet<br/>and no hosted service has started."]
    F -->|"no restart: the same start carries on"| G["ServiceControl opens.<br/>New failed messages go straight to SQL."]
    G --> H["Copy the selected history in the background,<br/>a pause between batches"]
    H --> I["Verify row counts on both sides,<br/>category by category,<br/>then set Migration/Enabled = false and restart,<br/>which comes back through this same gate"]

    M -->|"Off"| N{"Any checkpoint row<br/>still outstanding?"}
    N -->|"No"| L["ServiceControl opens.<br/>RavenDB is never opened again."]
    N -->|"Yes"| O{"AllowIncompleteExit set?"}
    O -->|"Yes"| P["Records each outstanding category as abandoned,<br/>logs what each one leaves behind,<br/>and opens."]
    O -->|"No"| Q{"Has this instance<br/>ever opened on SQL?"}
    Q -->|"No, so the abort is still free"| R["Opens, with a warning naming the two moves:<br/>stop now and point PersistenceType back at RavenDB,<br/>or carry on and lose the way back."]
    Q -->|"Yes"| S["Host does not start.<br/>Names every outstanding category,<br/>its counts, and every route out."]
```

The `Off` branch of that diagram, from node `N` down, is **planned**. Today `ServiceControl/Migration/Enabled=false` means only that the copy is not registered, and nothing inspects the checkpoint rows for outstanding categories.

**Checked before a single row moves,** in this order:

- The checkpoint table can be read at all. This one runs on every SQL start, migrating or not, and it refuses a database whose schema predates the feature
- The source and target are a supported pair. This is answered from settings alone, before anything connects
- This build can copy every required category. Today it cannot, so this is where a real migration stops
- The selected categories are valid
- `ServiceControl/RetryHistoryDepth` is greater than zero. At zero or less, the first completed retry after the migration deletes the entire copied retry history, and no row count would ever show it
- The SQL schema is current
- Message body storage is writable
- Both RavenDB databases are reachable
- The client certificate is valid, where the source is an external server
- The source is at a version this build can read

### Turning migration mode off is a gated startup too

**Planned.** None of this section runs today. `ServiceControl/Migration/Enabled=false` currently means only that the copy is not registered: nothing reads `HasHostOpened`, nothing reads `ServiceControl/Migration/AllowIncompleteExit`, and no code path sets a category to `Abandoned`. What is already wired is the marker the gate will need, which is stamped on any host start over a database that holds checkpoint rows.

*The right-hand branch above is the half a customer meets last and expects least, so it is worth reading before the migration starts rather than at the end of one.*

Every startup on a SQL Server or PostgreSQL instance will look at the checkpoint table before ServiceControl opens, whether `ServiceControl/Migration/Enabled` is on or off. That is what stops a migration ending by accident, and it costs nothing on an instance that has never migrated: a RavenDB instance has no checkpoint table at all, and a SQL instance whose categories all finished has nothing outstanding. Both start exactly as they do today. A SQL instance whose schema predates the feature is the one case that does not start, and the fix is to run `--setup`.

With `ServiceControl/Migration/Enabled` off and at least one category still outstanding, one of three things happens, and each is said out loud at startup rather than discovered weeks later:

- **`ServiceControl/Migration/AllowIncompleteExit` is set.** Every outstanding category is recorded as abandoned, with its copied and skipped counts left as they are, and the host starts. Each one is logged saying what state it was in, how much it had copied, and that whatever it had not copied stays only in RavenDB. Abandoning is final: selecting that category in a later migration does not copy it again.
- **It is not set, and this instance has never opened on SQL.** This is the [free abort](#the-free-abort-and-the-moment-it-closes), so the host starts and warns rather than refusing. The warning names the two moves: stop now and point `ServiceControl/PersistenceType` back at RavenDB, which discards the partial copy and costs nothing else, or carry on, which opens ServiceControl on a partly copied database and ends the free abort. It deliberately does not mention `ServiceControl/Migration/AllowIncompleteExit`, because at that moment nothing is lost yet.
- **It is not set, and this instance has already opened on SQL.** The host does not start. The error names every outstanding category, its state, its copied and skipped counts and its last error. It then names the three routes out. Restart with `ServiceControl/Migration/Enabled=true` to let the copy finish, or to resume a halted category once its cause is fixed. Or set `ServiceControl/Migration/AllowIncompleteExit` to abandon what is outstanding and start without it. Or, if RavenDB is already gone, abandon, because that is the only exit left.

**Which is why the source stays until verification passes.** A customer who decommissions RavenDB while a category is outstanding has both doors shut: `ServiceControl/Migration/Enabled=true` cannot start, because it opens the source before it copies anything, and `ServiceControl/Migration/Enabled=false` refuses. Abandoning is then the only way to start the instance, and it is a real loss whose size is the counts in that message.

## Which class does what, and where it is called from

Everything above is what the migration does. Which type does it, who calls it, and what is wired but not yet on a call path, is in [how the migration is put together](ravendb-to-sql-migration-system-design.md). That page is for someone changing the migration code rather than running a migration.

## Data to be migrated (Categories)

There are eighteen categories, twelve required and six optional. Today only `KnownEndpoints` and `EndpointSettings` can be copied.

### Required

- Unresolved **and retry-issued** failed messages, with their bodies. Attempt history collapses to the newest attempt, because the SQL model has no attempts table. Retry-issued messages are required for the same reason unresolved ones are: issuing a retry deletes the expiry, so they never age out. Leaving one behind means the retry confirmation arrives with no row to mark resolved, and the message stays missing from the customer's list while the retry actually succeeded
- Message redirects
- Endpoint settings, which are copied after known endpoints
- Known endpoints, including the monitored flag. One category, because the flag is a property of the endpoint row and cannot be copied without it
- Notification settings
- The licence trial end date
- Licensing endpoint records, the per-endpoint rows in the throughput database
- Throughput history, which is copied after the licensing endpoint records, because each day's throughput row hangs off one of them
- Retry operations, unacknowledged and historic. One category, because RavenDB holds both lists in a single document
- Licensing report masks
- The uploaded licensed endpoint details file, which nothing recomputes: skipping it means the customer re-downloads it from the licence portal and uploads it again
- Subscriptions

### Optional

- Archived and resolved failed messages, with their bodies: the biggest category by far, and most of the copying time
- The event log, without which the ServicePulse activity feed starts empty
- Custom checks, which cost almost nothing to skip because every check re-reports on its next interval
- Failed error imports, the record of errors that could not be ingested, with their bodies
- Group comments, **copied last of everything**, after archived and resolved messages. A comment survives only once the failed messages its group is built from have arrived, so on a large archive the comments are the last thing to appear. An empty comment field partway through a migration is the copy still running, not data loss
- Failed message edits, the record of which failed messages someone has edited and retried from ServicePulse. Nothing ever deletes these, so they are what stops the same message being edited twice. Skipping them means a message edited before the migration can be edited again afterwards

### Not migrated

- The RavenDB index definitions
- The transient in-flight collections, which are empty when nothing is running: `RetryBatches`, `RetryBatchNowForwardings`, `FailedMessageRetries`, `ArchiveOperations` and `UnarchiveOperations`
- `ArchiveBatches` and `UnarchiveBatches`, which exist only because of how RavenDB works
- `ConnectedApplications`, which only versions 6.0 and 6.1 wrote and nothing has read since
- Integration events still waiting to be sent when you switch over are never sent
- Broker and audit service version details, which refill on the throughput collector's next run

## What does not come across

**Planned.** Sixteen of the eighteen categories have no reader and no writer, so most of the rules below describe a design rather than running code. Where a rule is already enforced, it is marked.

**Whole categories are never copied.** Which ones, and why nothing needs them, is the [not migrated](#not-migrated) list above. Anything in an optional category you did not select is also never copied, and nothing later goes back for it.

**Rows skipped one at a time, and counted.** Each of these shows up in the skipped count for its category, broken out by reason, so you can see how much went and why. Only three skip reasons exist in code today: a body that could not be read, a row missing a value SQL requires, and settings for an unknown endpoint. Each of the other rules below needs a new reason value before it can be written at all, because the checkpoint refuses a batch whose skips do not add up.

- A failed message whose `UniqueMessageId` is not a GUID. The target column is a `uniqueidentifier` and the value is never regenerated, because it is simultaneously the primary key, the ServicePulse URL, the retry correlation key and the body lookup key.
- A failed message with no processing attempts recorded against it. The SQL model keeps the newest attempt and derives the failure time, the failing endpoint and the exception from it, all of which are required columns, so a message with nothing to derive them from cannot be written at all rather than being written blank.
- A failed message whose body cannot be read after three attempts. **The whole message is skipped, not just its body**, because a message with no body is worse than no message. Enforced today, by the engine.
- A subscription whose message type or transport address exceeds 200 characters. The target key columns are capped at 200 characters, so it cannot be stored at all.
- An archived or resolved failed message, or an event log item, already past its retention period. SQL's retention clean-up would delete it on its first pass, so it is counted rather than copied only to be deleted. The `PastRetention` reason exists and nothing writes it yet. Note that it is not currently treated as benign, so when a writer does start using it, those skips will count toward a halt unless that changes too.
- A group comment whose failure group has no failed messages in SQL once the messages are copied. SQL's clean-up removes such a comment, where RavenDB never expired one.
- Endpoint settings for an endpoint ServiceControl does not know. ServiceControl removes those settings shortly after it starts. Enforced today, by `EndpointSettingsWriter`.
- A row missing a value SQL requires, such as a known endpoint with no name or host, or a failed message with no failing endpoint address. An empty group comment is left behind the same way, because ServiceControl never stores one. Enforced today, by `KnownEndpointsWriter`.

**Things that change shape, and are not counted as skips at all.** The dry run counts these before anything moves, so they are a number you see in advance rather than a discovery afterwards. They are also the ones to read twice:

- **Processing attempt history collapses to the newest attempt.** The SQL model has no attempts table. This affects every failed message that failed more than once, in the one category every customer copies. A message that failed five times arrives showing one attempt, and the other four are gone.
- **Subscriptions that differ only in message-type version merge onto one row**, because the target key carries the type name without the version.
- **Endpoint settings for two endpoint names that differ only in case merge onto one row on SQL Server**, because the collation of the name column decides the comparison and the default collation compares names without case, so one of the two settings is kept. It is the column's own collation that decides, not the database default, so a case-sensitive database whose name column was given a case-insensitive collation still merges. PostgreSQL keeps both. The dry run counts this one too, by asking SQL Server how the name column compares, though for unusual characters its count can differ from what the copy does.
- **Event log items and historic retry operations are renumbered.** Their keys are database identities and nothing references them, so this is safe, but the old numbers do not survive.

**Rows RavenDB deletes while the copy is running are an absence, not a skip.** Expiration only deletes a document carrying `@expires`, and only two kinds ever get one: a resolved or archived failed message, and an event log item (`ExpirationManager.cs:34,41`). Everything in the [required](#required) set is therefore safe, since unresolved and retry-issued messages have their expiry removed when the retry is issued. Only the archived and resolved messages category and the event log category can shrink underneath the copier, and both copy in the background where the window is longest. A document the sweep removes before the stream reaches it is never read, so it is counted nowhere: the counts are of rows the source actually handed over, and there is no expected total to fall short of. It is the same population as the retention skip above, and which of the two it becomes is a race with the sweep. The consequence to know is that the dry run's count is a snapshot rather than a promise, and for those two categories the difference between it and the final copied count is not attributed to anything.

**A category can finish with a small amount of loss and still count as complete.** A few skipped rows in a large table leave the category in `CompleteWithErrors`, which blocks nothing. Its skipped count is printed and the ids of the skipped rows are written to the log, so while the RavenDB database still exists you can go and look at exactly what did not make it.

## The free abort, and the moment it closes

While ServiceControl is closed and the required copy is running, nothing except the copier has written to SQL, and the migration has written nothing to RavenDB, which is still authoritative. RavenDB's own expiration still runs, though: unless you disabled it, it keeps deleting expired failed messages and event log items, as [Goals](#goals) describes. If you need your instance back, set `ServiceControl/Migration/Enabled=false`, point `ServiceControl/PersistenceType` back at RavenDB, and start. You lose the copy, not your data, and you can start again later.

That window closes the moment ServiceControl opens. From then on new failed messages are ingesting into SQL, RavenDB is no longer current, and there is no rollback: nothing copies SQL rows back. The choice at that point is to finish the migration or to accept losing whatever has not been copied.

## Reading from RavenDB

- A third RavenDB lifecycle opens the source: connect, check the version, stop. It never calls `DatabaseSetup.Execute`.
- Both source databases must be on the same server or cluster. The migration opens each of them through one `IDocumentStore`.
- The source has to be at a ServiceControl version this build can read. A marker is stamped into both databases on every RavenDB startup, and the source check refuses four cases by name: no marker at all, a marker it cannot parse, a newer major version, and an older major version. The only other version check compares the RavenDB server version to the RavenDB client version, and runs only for an external source.
- **Planned.** Duration scales with distance to the source. The copier already holds the document from the stream, so each body is designed to cost **one** round trip rather than two, but it is one per message and they are not batched. Egress out of RavenDB Cloud is billed to the customer. No body read exists yet: `ReadBody` throws for every category, so the one-round-trip figure is a target rather than a measurement. See [batching and throttling](#batching-and-throttling).

## Writing to SQL

**Planned.** Only the known endpoints and endpoint settings writers exist, so every bullet below except the body-storage one describes a writer that has not been built. The target schema each bullet relies on is real, and was checked.

- A whole `FailedMessage` is written with its stored status intact. No existing caller does that, though the dialect upsert already accepts a status, so the gap is smaller than it looks.
- `UniqueMessageId` keeps its value, but converts type: the source holds a string and the target column is a `uniqueidentifier`. It is the primary key, the ServicePulse URL, the retry correlation key and the body lookup key at once.
- `StatusChangedAt` is reconstructed from `@expires` for resolved and archived messages, which is the only place RavenDB sets it. Unresolved and retry-issued messages have no `@expires`, so the copier uses the newest processing attempt's timestamp. The column is `NOT NULL`, so it cannot be left empty, but the value is harmless for those two: the retention sweep only considers resolved and archived rows, so an unresolved message never ages out whatever is written here.
- Message bodies go through `IBodyStoragePersistence`, which owns the compression threshold and the choice of filesystem, Azure Blob or S3. The separate inline threshold is not there: it defaults to 102,400 bytes, is overridable by `ServiceControl/MaxBodySizeToStore`, and lives on the ingestion path, so the copier has to apply it rather than inherit it.
- Throughput rows are written directly rather than through the collector, and the write sets each day's count rather than adding to it. Throughput is a required category, so it copies while ServiceControl is closed, before any collector has written to SQL. Setting is what makes the category safe to resume after a crash, where adding would double-count, and the collector's own path does add (`LicensingDataStore.cs:211`). Copying the rows is also what stops the audit and broker collectors re-gathering the same days when the host opens, because `LastCollectedDate` is derived from the newest throughput row rather than stored (`LicensingDataStore.cs:45`). The checkpoint is what stops a second pass overwriting days the collectors have written since.
- Identifiers narrow on the way across, and the dry run counts every kind. What narrows, merges or cannot be stored at all is in [what does not come across](#what-does-not-come-across).

## Batching and throttling

- Batch size comes from the provider: SQL Server divides its own parameter budget by the column count, PostgreSQL uses a flat 50 rows.
- The throttle is a pause between batches, defaulting to 100 ms and set by `ServiceControl/Migration/ThrottlePauseMilliseconds`. It applies to optional categories only, and not to the first batch of one. A required category is never throttled, because it runs with ServiceControl closed and nothing is competing with it.
- **Raising** the pause is what relieves a copy competing with production, because the pause is how long the engine waits between batches. Turning `ServiceControl/Migration/Enabled` off and restarting is the other remedy. Because both implemented categories are required, the pause does nothing at all today.

## Checkpointing and resume

A copy that runs for hours will be interrupted at some point: a restart, a dropped connection, a machine reboot. The checkpoint is what makes an interruption cost only the batch that was in flight. It is one row per category, kept on the target and created by `--setup` along with the rest of the schema, and it is written in the same database transaction as the rows it describes. Only the copier writes to it. The status and verify commands will read it.

**What one row holds:** the category it tracks, its state, the resume cursor, how many rows were copied, skipped and already present, a count per skip reason, how many rows the source held when the category started, when it started, when it last made progress, when it settled, the last error, and a version number used to spot a second writer.

**The states, and which ones a restart re-enters.** `Complete`, `CompleteWithErrors` and `Abandoned` are terminal, so a restart passes straight over the category. `Halted` and `Blocked` are not: a halt is resumed from its cursor once the cause is fixed, and a block clears itself once the category it waits on settles, which is how group comments end up behind archived messages. `NotStarted` and `InProgress` both mean there is work to do.

### One batch, and why nothing provisional is ever saved

```mermaid
sequenceDiagram
    participant E as Migration engine
    participant S as RavenDB source
    participant T as SQL target
    participant C as Checkpoint row

    E->>C: Read this category's row
    C-->>E: State, cursor, totals so far
    loop One batch at a time
        E->>S: Read the next batch after the cursor
        S-->>E: Rows, and the cursor they end at
        opt The category carries message bodies
            E->>S: Read each body, up to three attempts
            S-->>E: The bodies, and which ones could not be read
        end
        E->>T: Write the rows, with the totals so far,<br/>the unreadable bodies and the new cursor
        Note over T,C: One transaction. The rows, the target's own skips,<br/>the new totals and the cursor all commit, or none of them do
        T-->>E: The checkpoint exactly as it committed
        E->>E: Halt if too much of this run was skipped
    end
    E->>C: Settle as complete, complete with errors, or halted
```

The thing to read twice is that the counts never travel back through the engine to be saved on some later write. The engine hands the target the totals so far, the target adds its own outcome to them and saves the result beside the rows, and the engine then keeps whatever committed. So there is no window in which the stored row claims rows that are not there, and a crash at any instant leaves counts and cursor that both describe exactly the rows in SQL.

**What that buys, and why each part is needed:**

- **Progress never gets ahead of the data.** The rows and the cursor commit together, so a restart cannot skip past rows that were never written.
- **A crash costs the batch in flight and nothing else.** The next run reads from the committed cursor.
- **Re-reading a batch cannot double-count it.** Unreadable bodies stay off the checkpoint until the write commits, so a batch that is read twice is counted once, and rows the earlier attempt did write come back as `AlreadyPresent` rather than as fresh copies.
- **Every skipped row has a reason, or the save is refused.** The checkpoint rejects a batch whose per-reason counts do not sum exactly to its skipped count, in either direction, because verification has to account for each one rather than report a healthy migration as broken. The engine adds two more guards of the same shape: it refuses a commit whose deltas disagree with the target's own counts, and one whose benign skips exceed its total skips.
- **Each category resumes independently**, so a half-copied category picks up where it stopped while its neighbours are untouched.
- **The halt counters are per run and deliberately not stored.** If the skips that tripped a halt stayed on the row, a restart with the cause fixed would re-trip it on its first batch.
- **A second writer is caught rather than merged.** Each save carries the version it read, and a save against a row that has moved on is refused, so two hosts pointed at one target cannot quietly interleave their progress.
- **If a message is already in SQL the SQL row wins and the copier leaves it alone.** It is counted as already present rather than as a skip, which is what makes every category safe to run twice without moving it toward a halt.

## Error handling

- Which rows are skipped, and why, is in [what does not come across](#what-does-not-come-across). What follows is the mechanics around those rules.
- A body is read up to three times before the message is skipped whole, and the exhausted attempts count toward the halt threshold.
- Deciding whether a row is past the target's retention cutoff needs two retention periods: the source's reverses `@expires` back into the status-change instant, and the target's current one decides whether that instant is past the cutoff.
- A bad row does not stop the copy. Its category finishes `CompleteWithErrors`.
- The halt threshold is proportional with an absolute floor, and a category halts only when both are exceeded. Proportional alone halts a three-row category on one bad row. Absolute alone halts a five-million-row table on its 101st failure at the default floor of 100. Together, a large category keeps going through losses under the percentage and finishes `CompleteWithErrors`, so ten thousand skipped rows out of five million do not halt it.
- **A small category is not protected by the floor.** A separate rule halts any category that lost more than half its rows, whatever the floor says, because a category smaller than the floor would otherwise never reach it however much of it was lost.
- **A category also halts if it reaches the end of the source short.** If copied, skipped and already-present rows together come to less than the source count taken at the start, the category halts even though no threshold was crossed.
- Rows left behind because SQL would remove them anyway are counted and reported, but never halt a category. Today this exemption covers exactly one reason, settings for an unknown endpoint, and it withdraws itself: if the known endpoints copy skipped anything, an unknown endpoint can be this migration's own doing, so those skips start counting toward a halt again.
- The percentage is measured against what the run has processed so far rather than against the category's total, so a run that starts badly looks worse than it is. The floor is what keeps that harmless in a large category, since fewer than 101 skipped rows never consults the percentage at all. More than that, bunched at the start, does halt a category whose overall rate would have been fine, and the cost is one restart: the skipped rows commit with the cursor, so the next run resumes past them with its counters back at zero.
- The number compared against the floor is this run's fault skips, which is the skipped count less the benign ones, so it is not the same number the operator sees reported.
- A source therefore must not read a category in an order that puts the rows most likely to be skipped at the front of it.
- Verification therefore cannot treat any count difference as a fault. It accounts for every skip rule, or it reports every successful migration as broken.

### A halt stops one category, and clearing it is a restart

A halt is the copy refusing to keep going on one category because something is wrong beyond the odd bad row. It is not a crash and not data loss: everything already copied is committed, the cursor points at the row after the last one that committed, and the reason is written on the category. Nothing is retried in the background and nothing waits for a timer. The category sits halted until a person does something about it.

```mermaid
stateDiagram-v2
    [*] --> NotStarted: nothing has run yet
    NotStarted --> InProgress: the host starts with Migration/Enabled = true
    NotStarted --> Blocked: the category it must follow has not settled
    Blocked --> InProgress: that category settles, then the next restart
    InProgress --> InProgress: the host was stopped mid-copy,<br/>so the next start resumes from the cursor
    InProgress --> Complete: every row reached, none skipped
    InProgress --> CompleteWithErrors: every row reached, some skipped
    InProgress --> Halted: too many rows skipped in this run,<br/>most of the category lost,<br/>the source count not reached,<br/>or an unexpected error
    Halted --> InProgress: fix the cause, restart,<br/>carry on from the cursor
    Halted --> Abandoned: accept the loss, deliberately
    InProgress --> Abandoned: accept the loss, deliberately
    Complete --> [*]
    CompleteWithErrors --> [*]
    Abandoned --> [*]
```

**Four things halt a category.** The skipped rows in this run pass both the percentage and the floor, which says the failures are systematic rather than incidental. Or more than half the category was lost, which catches a category too small to reach the floor. Or the category reached the end of the source with fewer rows accounted for than the source held when it started. Or the copy hits an error it did not expect, in which case the error type and the cursor it stopped at are recorded. A host being shut down is none of these: it leaves the category in progress, to be picked up from the cursor next time. Nor is a second host writing to the same checkpoint, which is refused so that the other host's progress stands. Separately from all four, a copy that commits nothing for 30 minutes is cancelled by a stall watchdog, which does not mark the category halted but does stop the copy and keep the host closed.

**A halt stops that category and nothing else.** The remaining categories still run, with one exception: a category that must follow the halted one goes to `Blocked` rather than running early, which is how group comments stay behind the archived messages they belong to. A blocked category is not a failure and needs no separate action, since clearing the halt clears the block on the next restart.

**What it costs depends on which category halted.** A halted optional category means the instance keeps serving traffic and that one slice of history is missing until it is resumed. A halted required category means the host stays closed, so the outage carries on until the halt is cleared or the category is abandoned. That is deliberate: opening the host is the point of no return, and it must not happen with required data left behind by accident.

**Clearing it:**

1. Read the reason on the category, in the custom check or the status command. It names the count that tripped the threshold, or the error, and the cursor either way.
2. Fix the cause. It is usually outside the migration: the body store unreachable, a certificate expired, the source or the target down, or the disk full.
3. Restart the host with `ServiceControl/Migration/Enabled=true`. The category picks up at its cursor, its run counters start again at zero, and the skips already recorded stay on the row so the totals still add up at the end.
4. Repeat only if it halts again. A restart that halts at the same point is telling you the cause is still there, and a restart that gets further has made real progress, because the rows it skipped are committed and will not be read again.

**Or abandon it, on purpose.** Abandoning marks the category as deliberately given up rather than failed, which lets the host open and lets the migration end. It is the right answer when the data is not worth the outage, and the wrong one if it was picked by accident, because nothing goes back for an abandoned category afterwards. What it leaves behind is stated in the counts rather than guessed at.

## Dry run

**Planned.** There is no `--migration-dry-run`. Everything in this section describes a command that has not been built.

Runnable before anything starts, and again later against whatever is still outstanding. It never writes to RavenDB.

What it resolves and reports:

- Whether the source is embedded or external, and which server
- Which two RavenDB databases, and the setting each name came from
- What it found in each of them
- Rows per category, and message-body volume per category
- A duration for the window while ServiceControl is closed, as a range

It runs the same startup checks that gate startup, so a missing setting surfaces before a customer books an outage.

It counts, before anything moves, the rows that cannot cross as they stand:

- Documents whose `UniqueMessageId` will not parse as a GUID
- Subscriptions that differ only in message-type version, and so merge onto one row
- Subscriptions whose message type or transport address exceeds the 200-character key limit
- Integration event dispatches still queued, which are not copied and will never be sent

It reports no duration for the optional categories, and nothing about load on the source.

### When you can run the read-only commands

**Planned, except for the source report.** `--migration-verify`, `--migration-dry-run` and `--migration-status` do not exist. `--migration-source-report` does, and it prints the source facts plus a document count per RavenDB collection. It does not run the startup checks, does not count rows per category, does not measure body volume and does not estimate a duration.

`--migration-source-report`, `--migration-verify` and `--migration-dry-run` all open the RavenDB source. **On an embedded source that means stopping the ServiceControl service first**, because a second RavenDB process cannot attach to a data directory the first one holds. Plan the dry run as part of the outage rather than as something you run the day before while the instance keeps serving traffic. On an external source, a container or RavenDB Cloud, all three run against a live instance with no interruption.

A containerised instance runs all three as a one-off `docker run` of the same image with the command's flag, against an external RavenDB server, as the [instructions](ravendb-to-sql-migration-instructions.md#make-the-source-report) show for the source report. It cannot use an embedded source, for the reason given under [supported migration scenarios](#supported-migration-scenarios).

`--migration-status` is the exception and is deliberately so: it reads only the checkpoint table in SQL and never opens the source, so it will work on every source shape at any time, including during the background copy. It is the command to use for watching progress.

## Configuration and control

- A customer sets `ServiceControl/Migration/Enabled` and the list of categories next to it. A status command and a custom check will report back. **Planned:** neither exists yet, and progress today reaches an operator only as log output.
- Categories are read fresh at every startup. Adding one copies it on the next restart, removing one deletes nothing.
- There is no HTTP API, no pause, no resume, no abort, and no way to add a category to a running instance. All of those mean editing configuration and restarting.
- The checkpoint table is a record of what happened, not a control channel.
- Stopping a copy takes a restart, so it cannot be stopped in ten seconds.

## Out of scope

- The audit instance, which has no EF Core persister at all, so a customer who finishes this migration is still running RavenDB for audit. This is stated up front under [Problem](#problem), because it changes whether the migration is worth doing at all
- The monitoring instance, which keeps its data in memory, so there is nothing to move
