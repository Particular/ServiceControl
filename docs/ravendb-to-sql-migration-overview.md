# Moving data from RavenDB to SQL

## Problem

A customer can already point ServiceControl at SQL Server or PostgreSQL. They cannot bring their existing data with them.

## Strategy

- Switch over first, and copy only what has to be copied. Retention does most of the work: error retention is between 5 and 45 days and event retention is shorter, so most of the source ages out on its own within weeks. That is why archived and resolved messages are optional rather than required. Retention would have deleted them anyway.
- The required set is small because unresolved failures are the only category a customer can act on, and the strategy assumes customers keep that number low by resolving and archiving. **A neglected instance breaks that assumption**: unresolved failures can legitimately be months old, and a large backlog of them makes the closed window long rather than short. The dry run is what tells a customer which case they are in.
- Anything not selected simply ages out of RavenDB, and the customer deletes the old database when they are ready.

## Goals

- **Minimal downtime**. Only the required data copies with ServiceControl closed. Optional data copies in the background while it serves traffic.
- **All three RavenDB sources are supported**. Embedded, a container, or RavenDB Cloud, on one code path rather than three.
- **No writes through the client**. The copier never changes the source, but RavenDB's own expiration does: the primary database already has it configured, and the sweep keeps deleting failed messages and event log items throughout the migration and for as long afterwards as the instance is left running. The old database is a fallback that degrades from the moment you start.
- **Abandonable up to a known point, and only up to that point**. While ServiceControl is closed the copy can be thrown away at no cost, because nothing but the copier has written to SQL and RavenDB is untouched: see [the one point you can go back](#the-one-point-you-can-go-back). Once the host opens there is no way back at all.
- **No duplicates and no gaps**. Rows and the resume cursor commit in one transaction, so a crash needs no reconciliation.
- **Every identifier anything depends on is carried across**. The event log and historic retry operations are renumbered, because nothing references their keys.
- **Refuse rather than half-migrate**. Every check runs before the first row moves, and a failure is a host that will not start.
- **No silent loss**. A migration cannot end with a selected category incomplete, and skipped rows are counted and reported.
- **Bounded impact on a live instance**. Throttled behind normal ingestion and streamed, so memory does not track the size of the database.
- **Known before it starts, visible while it runs**. A dry run reports what will move and how long ServiceControl is closed, and every category transition is reported as it happens.
- **Use existing functionality where possible**. Progress goes through custom checks and the activity feed, so no new client or screen is needed.

## Deliberately not built, and not currently planned

*Each of these was considered and left out. None of them is scheduled: if one becomes a requirement it is new design work, not a later increment of this one.*

- **Zero downtime.** The required data is copied with ServiceControl closed, so there is a real, if short, outage.
- **Reversible once ServiceControl opens.** Nothing copies SQL rows back to RavenDB, so once the host has served traffic there is no rollback of any kind.
- **Steerable while running.** No pause, resume, or abort. Changing anything means editing configuration and restarting.
- **A general-purpose migration tool.** The source is always RavenDB and the target is always a ServiceControl EF Core persister, both at versions this build can read.
- **Custom migration UI via ServicePulse.** Custom checks and the event log will be used for progress reporting, but migration configuration and migration engine control will not be available via the UI.

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
- Both RavenDB databases, primary and throughput, on one server or cluster
- A SQL Server target must have Full-Text Search installed. `--setup` checks `SERVERPROPERTY('IsFullTextInstalled')` and fails if it is absent, because message search is not optional. A stock SQL Server container image does not include it. PostgreSQL needs nothing extra, since its index is a GIN over `to_tsvector`
- A managed target's transient failures are already survivable: retry on failure is on by default and there is no setting to turn it off

**Not supported:**

- Any server-to-server copy: no backup and restore, no RavenDB ETL or replication into SQL, no external data pipeline
- A host that can reach only one of the two databases at a time, so no staged move by way of an offline copy
- **An embedded RavenDB source when ServiceControl runs in a container.** Reading an embedded database means starting a RavenDB server process, and the container image does not carry one: `ServiceControl.Persistence.RavenDB.csproj:36` excludes the `RavenDBServer` directory from the artifact, and the copy that would restore it at `:44` is conditional on `CI` not being set, which the Dockerfile sets. A containerised instance migrating away from embedded RavenDB has to point at an external RavenDB server rather than at a data directory. Windows installations are unaffected: the installer unzips the server unconditionally
- Primary and throughput RavenDB databases in different locations
- Anything but RavenDB as the source, or anything but a ServiceControl EF Core persister as the target

## Migration workflow

1. Upgrade ServiceControl as normal, still on RavenDB.
2. Set four things in configuration: the new `PersistenceType`, its connection string, `MigrationMode=true`, and which [optional data](#data-to-be-migrated) they want copied.
3. Run `--setup` to create the SQL schema. It fails against a SQL Server instance without Full-Text Search installed.
4. Run the [dry run](#dry-run). It reports what it resolved as a source, what each category holds, and an estimate of how long ServiceControl will be closed. Read [what the estimate is worth](#what-the-estimate-is-worth) before booking an outage around it.
5. Start ServiceControl (`MigrationMode=true`).
6. Every check runs before a single row moves. If one fails the host does not start and names which, having copied nothing, so a wrong database name or unconfigured body storage costs a restart rather than a half-finished migration.
7. The copying of [required data](#required) starts, with ServiceControl still closed. This is assumed to be a small amount of data.
8. ServiceControl opens, and whatever [optional data](#optional) they asked for is copied in the background while the instance runs normally. They can watch it from ServicePulse custom checks and events, but not steer it.
9. They run the verification pass once the background job has completed, which reports row counts on both sides category by category, accounting for deliberate skips so a difference is explained rather than reported as a fault, then set `MigrationMode=false` and restart.
10. RavenDB data can be removed.

- If `MigrationMode=false` is set while a selected category is still incomplete, the host refuses to start and names exactly what is outstanding.
- A category that ended *complete with errors* counts as complete and does not block, though its skipped count is printed so the loss is stated rather than silent.
- An explicit override exists for a customer who has changed their mind and accepts leaving data behind. It marks the outstanding categories as abandoned, which is a deliberate end state rather than a failure, so the progress check settles and the guard stays armed for any later migration.
- **Steps 5 to 7 are the abort window**, which is not the override above: see [the one point you can go back](#the-one-point-you-can-go-back).
- A category that stops because too many rows failed is *halted*, and restarting will halt it again. Fix the cause and restart to resume it, or abandon it deliberately if you accept the loss.

## Architecture

```mermaid
flowchart TB
    cfg["Configuration + restart<br/>the only way to change anything"]
    checks["Custom checks + activity feed<br/>progress, with no new client needed"]

    subgraph host["One ServiceControl host process, started with MigrationMode = true"]
        direction LR
        raven["RavenDB persister<br/>own AssemblyLoadContext<br/>new read-only lifecycle"]
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

- **No provider-specific code in the engine or the source.** The source is always RavenDB and the target is always an `IPersistence`.
- **Both persisters load into the same process**, each into its own `AssemblyLoadContext`.
- **The engine references neither assembly.** It knows only `IMigrationSource` and `IMigrationTarget`, and treats the resume cursor as an opaque value it passes from one to the other, so it can be tested against fakes on either side.

## Startup sequence

```mermaid
flowchart TB
    A["Restart with MigrationMode = true"] --> B["Open the SQL target, exactly as today"]
    B --> C["Open the old RavenDB, read only"]
    C --> D{"All checks pass?"}
    D -->|"No"| E["Host does not start.<br/>Says which check failed.<br/>Nothing has been copied."]
    D -->|"Yes"| F["Copy what cannot be recreated.<br/>Minutes. ServiceControl still closed."]
    F --> G["ServiceControl opens.<br/>New failed messages go straight to SQL."]
    G --> H["Copy the selected history in the background,<br/>throttled behind normal ingestion"]
    H --> I["Verify row counts on both sides,<br/>category by category"]
    I --> J{"MigrationMode = false,<br/>everything complete?"}
    J -->|"No"| K["Host does not start.<br/>Names what is outstanding.<br/>An override exists."]
    J -->|"Yes"| L["RavenDB is never opened again"]
```

**Checked before a single row moves:**

- The SQL schema is current
- Message body storage is writable
- Both RavenDB databases are reachable
- The client certificate is valid, where the source is an external server
- The source is at a version this build can read
- The selected categories are valid
- `RetryHistoryDepth` is greater than zero. At zero or less, the first completed retry after the migration deletes the entire copied retry history, and no row count would ever show it

## Data to be migrated (Categories)

### Required

- Unresolved **and retry-issued** failed messages, with their bodies. Attempt history collapses to the newest attempt, because the SQL model has no attempts table. Retry-issued messages are required for the same reason unresolved ones are: issuing a retry deletes the expiry, so they never age out. Leaving one behind means the retry confirmation arrives with no row to mark resolved, and the message stays missing from the customer's list while the retry actually succeeded
- Message redirects
- Endpoint settings
- Known endpoints, including the monitored flag. One category, because the flag is a property of the endpoint row and cannot be copied without it
- Notification settings
- The licence trial end date
- Throughput history
- Retry operations, unacknowledged and historic. One category, because RavenDB holds both lists in a single document
- Licensing report masks
- The uploaded licensed endpoint details file, which nothing recomputes: skipping it means the customer re-downloads it from the licence portal and uploads it again
- Subscriptions

### Optional

- Archived and resolved failed messages: the biggest category by far, and most of the copying time
- The event log, without which the ServicePulse activity feed starts empty
- Custom checks, which cost almost nothing to skip because every check re-reports on its next interval
- Failed error imports, the record of errors that could not be ingested
- Group comments, **copied last of everything**, after archived and resolved messages. A comment survives only once the failed messages its group is built from have arrived, so on a large archive the comments are the last thing to appear. An empty comment field partway through a migration is the copy still running, not data loss
- Failed message edits

### Not migrated

- The fifteen RavenDB index definitions, which map to a much smaller set of ordinary SQL indexes, and two of which are dead already
- The transient in-flight collections, which are empty when nothing is running
- `ArchiveBatches`, which exists only because of how RavenDB works
- Integration events still waiting to be sent when you switch over are never sent
- Broker and audit service version details, which refill on the throughput collector's next run
- The last computed licensed-endpoint count, which is recomputed from the throughput data that is being copied

## What does not come across

**Whole categories are never copied.** Which ones, and why nothing needs them, is the [not migrated](#not-migrated) list above. Anything in an optional category you did not select is also never copied, and nothing later goes back for it.

**Rows skipped one at a time, and counted.** Each of these shows up in the skipped count for its category, broken out by reason, so you can see how much went and why:

- A failed message whose `UniqueMessageId` is not a GUID. The target column is a `uniqueidentifier` and the value is never regenerated, because it is simultaneously the primary key, the ServicePulse URL, the retry correlation key and the body lookup key.
- A failed message with no processing attempts recorded against it. The SQL model keeps the newest attempt and derives the failure time, the failing endpoint and the exception from it, all of which are required columns, so a message with nothing to derive them from cannot be written at all rather than being written blank.
- A failed message whose body cannot be read after three attempts. **The whole message is skipped, not just its body**, because a message with no body is worse than no message.
- A row already past the target's retention cutoff. The sweeper would delete it within the hour, so copying it would write a body to blob storage for nothing.
- A group comment whose failure group has no messages left in the target. RavenDB never expires comments, but the SQL sweeper reclaims orphans.

**Things that change shape, and are not counted as skips at all.** The dry run counts these before anything moves, so they are a number you see in advance rather than a discovery afterwards. They are also the ones to read twice:

- **Processing attempt history collapses to the newest attempt.** The SQL model has no attempts table. This affects every failed message that failed more than once, in the one category every customer copies. A message that failed five times arrives showing one attempt, and the other four are gone.
- **Subscriptions that differ only in message-type version merge onto one row**, because the target key carries the type name without the version.
- **A subscription whose message type or transport address exceeds 200 characters cannot be stored at all.**
- **Throughput endpoint names that differ only in case merge onto one row**, because the target key is lower-cased.
- **Event log items and historic retry operations are renumbered.** Their keys are database identities and nothing references them, so this is safe, but the old numbers do not survive.

**A category can finish with a small amount of loss and still count as complete.** A few skipped rows in a large table leave the category in a *complete with errors* state, which blocks nothing. Its skipped count is printed and the ids of the skipped rows are written to the log, so while the RavenDB database still exists you can go and look at exactly what did not make it.

## The one point you can go back

While ServiceControl is closed and the required copy is running, nothing except the copier has written to SQL and RavenDB is untouched and still authoritative. If you need your instance back, set `MigrationMode=false`, point `PersistenceType` back at RavenDB, and start. You lose the copy, not your data, and you can start again later.

That window closes the moment ServiceControl opens. From then on new failed messages are ingesting into SQL, RavenDB is no longer current, and there is no rollback: nothing copies SQL rows back. The choice at that point is to finish the migration or to accept losing whatever has not been copied.

## Reading from RavenDB

- A third RavenDB lifecycle opens the source: connect, check the version, stop. It never calls `DatabaseSetup.Execute`.
- Both source databases must be on the same server or cluster (`LicensingDataStore.cs:35`).
- The source has to be at a ServiceControl version this build can read, and nothing in RavenDB records one today. The only version check that exists compares the RavenDB server version to the RavenDB client version, and runs only for an external source. So a marker is stamped into the database on upgrade, and a source without one, or one from a newer major version, is refused by name rather than misread.
- Duration scales with distance to the source. The copier already holds the document from the stream, so each body costs **one** round trip rather than two, but it is one per message and they are not batched. Egress out of RavenDB Cloud is billed to the customer. See [how long the background copy actually takes](#how-long-the-background-copy-actually-takes).

## Writing to SQL

- A whole `FailedMessage` is written with its stored status intact. No existing caller does that, though the dialect upsert already accepts a status, so the gap is smaller than it looks.
- `UniqueMessageId` keeps its value, but converts type: the source holds a string and the target column is a `uniqueidentifier`. It is the primary key, the ServicePulse URL, the retry correlation key and the body lookup key at once.
- `StatusChangedAt` is reconstructed from `@expires` for resolved and archived messages, which is the only place RavenDB sets it. Unresolved and retry-issued messages have no `@expires`, so the copier uses the newest processing attempt's timestamp. The column is `NOT NULL`, so it cannot be left empty, but the value is harmless for those two: the retention sweep only considers resolved and archived rows, so an unresolved message never ages out whatever is written here.
- Message bodies go through `IBodyStoragePersistence`, which owns the compression threshold and the choice of filesystem, Azure Blob or S3. The separate 102,400-byte inline threshold is not there: it lives on the ingestion path, so the copier has to apply it rather than inherit it.
- Throughput rows are written directly rather than through the collector, and the write sets each day's count rather than adding to it.
- Identifiers narrow on the way across, and the dry run counts every kind. What narrows, merges or cannot be stored at all is in [what does not come across](#what-does-not-come-across).

## Batching and throttling

- Batch size comes from the provider: SQL Server divides its own parameter budget by the column count, PostgreSQL uses a flat 50 rows.
- The throttle is a configurable pause between batches, defaulting to 100 ms. Lowering it, or turning `MigrationMode` off, is the only remedy for a copy competing with production.

## Checkpointing and resume

- The checkpoint table is a new table on the target, created by `--setup` along with the rest of the schema.
- It holds one row per category: the selection that row ran with, the state, the resume cursor, copied and skipped counts, timings and the last error.
- Only the copier writes to it. The status command reads it.
- Rows and the resume cursor commit in one transaction.
- If a message is in both databases the SQL row wins and the copier skips it, so every category is safe to run twice.
- Each category has its own cursor, so a half-copied category picks up where it stopped.

## Error handling

- Which rows are skipped, and why, is in [what does not come across](#what-does-not-come-across). What follows is the mechanics around those rules.
- A body is read up to three times before the message is skipped whole, and the exhausted attempts count toward the halt threshold.
- Deciding whether a row is past the target's retention cutoff needs two retention periods: the source's reverses `@expires` back into the status-change instant, and the target's current one decides whether that instant is past the cutoff.
- A bad row does not stop the copy. Its category finishes in a separate complete-with-errors state.
- The halt threshold is proportional with an absolute floor, and a category halts only when both are exceeded. Proportional alone halts a three-row category on one bad row; absolute alone lets ten thousand failures pass on a five-million-row table as "only 0.2%".
- Verification therefore cannot treat any count difference as a fault. It accounts for all five skip rules, or it reports every successful migration as broken.

## Dry run

Runnable before anything starts, and again later against whatever is still outstanding. It never writes to RavenDB.

What it resolves and reports:

- Whether the source is embedded or external, and which server
- Which two RavenDB databases, and the setting each name came from
- What it found in each of them
- Rows per category, and message-body volume per category
- A duration for the window while ServiceControl is closed, as a range

It runs the same seven checks that gate startup, so a missing setting surfaces before a customer books an outage.

It counts, before anything moves, the rows that cannot cross as they stand:

- Documents whose `UniqueMessageId` will not parse as a GUID
- Throughput endpoint names that differ only in case, and so merge onto one row
- Subscriptions that differ only in message-type version, and so merge onto one row
- Subscriptions whose message type or transport address exceeds the 200-character key limit
- Integration event dispatches still queued, which are not copied and will never be sent

It reports no duration for the optional categories, and nothing about load on the source.

### When you can run the read-only commands

`--migration-source-report`, `--migration-verify` and `--migration-dry-run` all open the RavenDB source. **On an embedded source that means stopping the ServiceControl service first**, because a second RavenDB process cannot attach to a data directory the first one holds. Plan the dry run as part of the outage rather than as something you run the day before while the instance keeps serving traffic. On an external source, a container or RavenDB Cloud, all three run against a live instance with no interruption.

One deployment shape they cannot help at all: all three need shell access to the host, so a containerised instance has no easy way to run them, and a containerised instance cannot use an embedded source either.

`--migration-status` is the exception and is deliberately so: it reads only the checkpoint table in SQL and never opens the source, so it works on every source shape at any time, including during the background copy. It is the command to use for watching progress.

## Configuration and control

- A customer sets `MigrationMode` and the list of categories next to it. A status command and a custom check report back.
- Categories are read fresh at every startup. Adding one copies it on the next restart, removing one deletes nothing.
- There is no HTTP API, no pause, no resume, no abort, and no way to add a category to a running instance. All of those mean editing configuration and restarting.
- The checkpoint table is a record of what happened, not a control channel.
- Stopping a copy takes a restart, so it cannot be stopped in ten seconds.

## Out of scope

- The audit instance, which has no EF Core persister at all, so a customer who finishes this migration is still running RavenDB for audit. This is stated up front under [Problem](#problem), because it changes whether the migration is worth doing at all
- The monitoring instance, which keeps its data in memory, so there is nothing to move
