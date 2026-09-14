# EF Audit Persistence for the Primary Instance

## Summary

Implement audit storage and querying in the existing primary EF persisters, so `SupportsAuditIngestion`
can be flipped to `true` and the audit runtime already hosted in the primary starts writing and
reading real data.

This is the implementation half of [Host Audit Ingestion in the Primary Instance](audit-ingestion-in-primary-plan.md).
That plan delivered the contracts, the copied runtime, the settings, the fail-fast command and the
composition. Nothing in it stores an audit message. This plan does.

Revised 14 September 2026 to add a second topology: a dedicated audit database, served by the same
executable in a new audit-only mode and reached by the primary through the existing scatter-gather.
See "Topologies". The hosting plan's decision that there is no separate SQL Server or PostgreSQL
audit HTTP service is superseded by that section.

The audit-only spike ([#5318](https://github.com/Particular/ServiceControl/pull/5318)) remains the
evidence base for partitioning, retention and full-text search. It is not the design: it targeted a
standalone audit instance with its own database, its own `DbContext` and its own persistence
contracts. Here the audit tables join the primary's model.

## The governing trade

Ingestion is the hot path and reads are not. Audit volume is orders of magnitude above error volume,
every message is written once and read rarely, and a slow write path backs up onto the broker where a
slow query only makes one person wait.

So: nothing goes on the write path that can be moved off it, and where the two conflict the query pays.
That is what settles the shape of most of this plan. No upsert, no conflict probe, no rollup table
maintained at ingestion, no extra index that only a query wants, and a database generated id rather
than one derived per message. The consequences are read side and accepted: duplicate rows on
redelivery, and counts and unions computed at query time.

## Goals

- Store audit messages, saga snapshots and failed audit imports in the primary database, with the
  cheapest write path that will do.
- Serve the five `IMessagesViewDataStore` queries from a union of failed and audited messages.
- Serve audit counts, saga history, and audit body resolution.
- Keep the spike's PostgreSQL hourly range partitioning and its retention economics.
- Keep ingestion safe under competing consumers.
- Flip `SupportsAuditIngestion` to `true` on both EF manifests once the shared database works.
- Let an operator move audit storage to a dedicated database, on the same server or another, without
  a second audit persister and without changing how RavenDB deployments work.

## Non-goals

- Any change to RavenDB, on either instance.
- Migrating existing RavenDB audit data.
- Changing the audit runtime. Host composition and settings change only where the dedicated audit
  database needs them, and those changes are listed under "Topologies".
- SCMU and PowerShell support for any of this. It stays with the EF storage type workstream, which
  now has one more host mode and two more settings to surface.
- Making `EnableFullTextSearchOnBodies` real. See "Full-text search".

## Decisions

Settled by interview on 22 Aug 2026, extended by interview on 14 Sep 2026.

| Decision | Choice |
| --- | --- |
| Model boundary | Audit tables join `ServiceControlDbContext` and the existing per-provider migration stream. Partitioning is applied by raw SQL inside a migration, the way `AddFullTextSearch` already applies the GIN index. |
| Partition creation | The retention sweeper alone creates partitions ahead. Ingestion-only workers never issue DDL. |
| Audit body keys | `audit/{yyyy-MM-dd-HH}/{uniqueMessageId}`, with a new delete-by-prefix operation on `IBodyStoragePersistence`. |
| Full-text search | Mirror the error side: index the audit table's own columns through the existing `IFullTextSearchDialect` seam. No `SearchableContent` column. |
| Audit message identity | `created_on` is the ingestion hour. Primary key is `(created_on, id)` where `id` is a `bigint` identity. Rows are plain inserts and are not deduplicated. |
| Saga snapshot identity | A `bigint` database identity, no dedupe. A redelivered saga audit message produces a second snapshot. |
| Audit counts | Aggregate query over the audit table, served by an index on `(receiving_endpoint_name, created_on)`. No rollup table. |
| `AuditRetentionPeriod` null | 7 days, matching SCMU and the Dockerfile. |
| Retention ownership | Single owner via `RunRetentionSweep`, plus a session scoped advisory lock ported from the spike so two misconfigured primaries cannot sweep at once. |
| Dedicated audit database | Supported. The primary executable gains `--audit-instance`, a host that ingests audit, serves the audit routes and sweeps audit retention against its own connection string. The primary lists it under `RemoteInstances` exactly as it lists a RavenDB audit instance. Neither `ServiceControl.Audit.exe` nor a second `DbContext` in the primary is involved. |
| Schema of a dedicated audit database | The same `ServiceControlDbContext` and the same migration stream. The audit database carries the error tables, empty, and a primary whose audit is remote carries the audit tables, empty. |
| How the primary knows audit is remote | An explicit setting, `ServiceControl/AuditDataLocation`, `Local` or `Remote`. `Local` is the default where the persister supports audit. Not derived from `IngestAuditMessages` and `RemoteInstances`, because the shared topology where only workers ingest looks the same. |
| Delivery order | Shared database first, through step 6. The dedicated database is steps 7 and 8, composed from parts that already work by then. |
| Message view composition | One SQL statement per view: `UNION ALL` of the failed and audit branches, precedence by anti-join on `unique_message_id`, sort, paging and count in the database. Decided 14 Sep 2026, replacing the in-memory merge of two pages, which paged wrongly past page one and could not count. |

### Why the partition key is the ingestion hour, and why rows are not deduplicated

PostgreSQL requires every unique constraint on a partitioned table to include the partition key, so
the row's identity and its partition are one decision. `created_on` is `UtcNow` truncated to the hour,
as in the spike.

The ingestion path is a plain multi-row `INSERT` with a database generated id. There is no upsert,
which means a redelivered audit message produces a second row.

This is a deliberate regression against the standalone RavenDB audit instance, which does deduplicate:
`RavenAuditIngestionUnitOfWork` bulk inserts with the deterministic document id
`ProcessedMessages-{ticks}-{ProcessingId()}`, so storing the same message twice overwrites. Keeping
that would have meant an index probe per row on the hot path, on every message, to correct a case that
only arises when a receive is not acknowledged. The spike also inserted plainly. The cost is that a
redelivered message appears twice in ServicePulse.

Because there is no conflict clause, the insert is identical on both providers apart from identifier
quoting, so audit ingestion needs no provider specific dialect at all.

### Partition creation is a single point of failure, knowingly

Only the retention sweeper creates partitions, so an ingestion-only worker cannot insert into an hour
the sweeper never provisioned. If the sweeper's host is down longer than the lookahead, every worker
starts failing inserts.

Mitigations, not fixes:

- Provision a long lookahead (48 hours, against the spike's 6), so the outage has to be sustained.
- A custom check that fails when the newest provisioned partition is less than 12 hours ahead, so the
  condition is visible before it bites.
- The setup command provisions the initial window, so a fresh instance ingests before the first sweep.

Revisiting this means letting ingesting hosts issue `CREATE TABLE IF NOT EXISTS` themselves.

## Topologies

Audit volume is what stresses a database. An operator whose business SQL Server copes with the error
instance but not with audit needs to move only audit to a dedicated server, and nothing else about
the deployment should have to change when they do. Clustering and replicas are the server's answer
to load; this is the product's.

Every process is the same executable and the same persister. What differs is the connection string
each process is given and the mode it is started in.

### Shared database

The default, and the only topology steps 1 to 6 deliver.

| Process | Started as | Database | Runs |
| --- | --- | --- | --- |
| Primary | `ServiceControl.exe` | primary | Everything a primary runs, plus the audit receiver unless `IngestAuditMessages` is false, the local audit queries, and the audit retention pass. |
| Audit worker | `--audit-ingestion-only` | primary | The audit receiver and nothing else, as shipped. |
| Error worker | `--error-ingestion-only` | primary | As shipped. |

### Dedicated audit database

| Process | Started as | Database | Runs |
| --- | --- | --- | --- |
| Primary | `ServiceControl.exe` with `AuditDataLocation=Remote` and `RemoteInstances` naming the audit host | primary | Everything a primary runs, minus the audit receiver, the local audit queries and the audit retention pass. Audit data reaches it through the scatter-gather, as it does from a RavenDB audit instance. |
| Audit host | `--audit-instance` | audit | The audit receiver, the audit routes, audit retention and partition provisioning, saga audit, failed audit import tooling, platform connection details. No error side. |
| Audit worker | `--audit-ingestion-only` with `ServiceControlQueueAddress` set | audit | The audit receiver, as shipped. It does not know which database it feeds: that is the connection string it was given. |
| Error worker | `--error-ingestion-only` | primary | As shipped. Error ingestion only ever shares the primary's database. |

The RavenDB topology is unchanged: `ServiceControl.Audit.exe` with its own database, listed as a remote.

### One owner per database

Every database has exactly one owner, and only owners run setup. The owner of the primary database
is the primary; the owner of a dedicated audit database is the audit host. The owner is the process
that runs `--setup`, and so migrations, queue creation and body storage provisioning, and the process
that runs retention, partition provisioning and the API. Every other process on that database is a
worker: `--error-ingestion-only` and `--audit-ingestion-only` run none of those, exactly as they do
today. A worker on a dedicated audit database is not listed under `RemoteInstances`, because it has
no API to list; only the audit host is.

Two `--audit-instance` processes on one database are a misconfiguration, the same way two primaries
on one database are. The advisory retention lock is the safety net for both, not a supported shape.

Two consequences follow from owners being upgraded independently:

- The primary database and the audit database run the same migration stream, but their owners are
  upgraded on their own schedules, so one is routinely a migration behind the other. The primary
  only ever reaches the audit host over HTTP, which already has to tolerate a RavenDB audit remote
  on an older version, so the upgrade order does not matter. The acceptance suite covers an audit
  host one migration behind the primary.
- Workers have no schema check. A worker started before its database was migrated fails on the first
  insert rather than at startup, which is today's behaviour for error workers as well. Workers gain a
  startup probe that reads the migrations history table and refuses to start, with a message naming
  `--setup` on the owner, when the migration the binary was built against is not applied.

### The audit host

`--audit-instance` is the primary executable composed from the audit side only. Against
`--audit-ingestion-only` it adds the API, retention, failed audit reimport, platform connection
details and licensing metadata. Against the normal primary it drops error ingestion, recoverability,
heartbeat monitoring as a feature, the event log, external integrations, notifications and licensing
ownership.

Components: `AuditComponent`, `HeartbeatMonitoringComponent` for `IsNewInstance` as in the worker,
and `CustomChecksComponent` in reporting mode, see below. The full persister is registered with
`RunRetentionSweep` true. `--setup` in this mode provisions the audit queue, migrates the audit
database and provisions body storage, and does not touch the primary's queues.

The API surface is only the routes the primary calls on a remote, plus health. Taken from the code
that calls them: `/api` (`CheckRemotes`), `/api/configuration` (`ConfigurationApi` and licensing),
`/api/connection` (`RemotePlatformConnectionDetailsProvider`), the five message views,
`/api/messages/{id}/body` (forwarded by instance id from `GetMessagesController`), `/api/sagas/{id}`
and `/api/endpoints/{name}/audit-count`. They are registered through an
`IApplicationFeatureProvider<ControllerFeature>` allow list, so a browser or ServicePulse pointed at
the audit host by mistake gets 404 rather than an empty error instance.

Authorization: the primary forwards the caller's `Authorization` header to remotes, so the audit host
runs the same authorization configuration and the same `error:*` policies as the primary. That is how
a RavenDB audit remote works today with its `audit:*` policies, and the documentation has to say the
two processes must be configured alike.

Startup guards: the persister must support audit; `ServiceControlQueueAddress` must be set;
`RemoteInstances` must be empty, because an audit host is a leaf; and the mode cannot be combined
with either ingestion-only flag.

### Reporting back to the primary

The primary is the only process ServicePulse talks to, and two things it shows come from wherever
audit is ingested: custom checks (audit ingestion health, failed audit imports) and endpoints
detected from audit messages.

In the shared topology both land in the primary's tables directly, through the shared unit of work.
In the dedicated topology the audit host and its workers write to the audit database, which the
primary never reads, so they need the RavenDB audit instance's mechanism: `ReportCustomCheckResult`
and `RegisterNewEndpoint` sent to the primary's queue, which `ReportCustomCheckResultHandler` and
`RegisterNewEndpointHandler` already handle. `ServiceControl/ServiceControlQueueAddress`, the audit
instance's own key name, names that queue.

The copied audit runtime has no NServiceBus endpoint, only `IMessageDispatcher`. Hosts on the audit
database get a send-only NServiceBus endpoint for these two messages. Send-only claims no queue, so
the hosting plan's rule that ingestion-only hosts own no queue holds. The presence of
`ServiceControlQueueAddress` is what switches a host from writing custom checks and endpoint
registrations locally to sending them: required on `--audit-instance`, set on an
`--audit-ingestion-only` worker only when it feeds a dedicated audit database, and never set on a
shared topology process. Known endpoints are still recorded in the audit database as well, because
`IsNewInstance` warms from there and the audit host's own queries need them.

The custom check ids need a decision. `FailedAuditImportCustomCheck` in the primary is named
`Audit Message Ingestion (local)` so that it cannot collide with the RavenDB audit instance's check in
the same category. That name reads wrongly when reported from a dedicated audit host. Step 7 decides
whether the id is the same in both topologies or the host reports under the audit instance's id, and
`InternalCustomCheckClassification` has to know the answer either way.

### The primary in Remote mode

`AuditDataLocation=Remote` turns off, on the primary:

- The audit receiver, regardless of `IngestAuditMessages`.
- The local audit queries. The existing `Empty*` audit stand-ins are registered instead of the EF
  stores, so the scatter-gather treats the local instance as a non-participant and a timed-out audit
  host is reported as a timeout rather than hidden behind an empty local answer. The message views
  drop their audit branch the same way, and the audit host drops the failed branch, so each host
  queries only the tables it owns.
- The audit retention pass and partition provisioning, through a persister setting carried the way
  `AuditRetentionPeriod` is.
- `--import-failed-audits`, which fails with a message naming the audit host as the place to run it.
- The current warning about remotes plus local audit, which becomes a validation error in the
  opposite direction: `Local` with the receiver on and remotes configured.

Everything else, including `/api/connection` composition and licensing throughput, keeps working the
way it does with a RavenDB remote today.

### Settings

| Setting | Process | Notes |
| --- | --- | --- |
| `ServiceControl/AuditDataLocation` | primary | `Local` (default where the persister supports audit) or `Remote`. Ignored where the persister does not support audit. |
| `ServiceControl/RemoteInstances` | primary | Already exists. Lists the audit host's API URL. |
| `ServiceControl/ServiceControlQueueAddress` | audit host, and audit workers on a dedicated database | The primary's input queue. Same key the RavenDB audit instance reads. |
| `ServiceControl/Database/ConnectionString` | every process | Which database a process feeds. Unchanged. |
| `ServiceControl/MessageBody/...` | every process | Hosts on one database share one body store: the audit host and its workers share the audit store, the primary and its workers share the primary's. The `audit/` key prefix keeps the two apart if an operator points both at one store. |

`--audit-ingestion-only` needs no new flag. A worker on a different database is the same command with
a different connection string and `ServiceControlQueueAddress` set.

## What already exists and is reused

The largest risk in this work is rebuilding something the primary already has. It has more than the
spike did.

| Capability | Where | Consequence |
| --- | --- | --- |
| Known endpoint recording | `FailedMessageBatchWriter.BuildEndpointRows` dedupes in memory per batch with `DistinctBy`, then `IFailedMessageIngestionSqlDialect.InsertMissingKnownEndpoints` issues one insert-if-missing per distinct endpoint | Nothing to do. The audit path already calls `Monitoring.RecordKnownEndpoint` on the same unit of work. The spike's insert-only table and its reconciler are not ported. |
| Provider-specific upserts | `IFailedMessageIngestionSqlDialect`, with `ON CONFLICT` and `MERGE WITH (HOLDLOCK)` implementations | Audit inserts extend this seam rather than inventing one. |
| Full-text search | `IFullTextSearchDialect`, `FullTextSearchSql`, the `AddFullTextSearch` migration, `FullTextSearchIndexTests` pinning query and index together | The audit index follows the identical pattern, including the test that fails when the two drift. |
| Body storage | `IBodyStoragePersistence` with FileSystem, AzureBlob and S3 implementations, plus installers | Audit bodies use the same store, and gain one new operation. |
| Body classification | `MessageBodyClassifier.Classify` decides inline text against external, honouring `MaxBodySizeToStore` and binary detection | Audit bodies reuse it unchanged. |
| Retention | `RetentionSweeper`, a `BackgroundService` gated by `RunRetentionSweep`, already deleting external bodies before rows | Audit retention extends it. |
| Transactional batch | `EFIngestionUnitOfWork.Complete` runs one execution strategy and one transaction | Audit rows join that transaction. |
| Body arbitration order | Documented on `IBodyStorage.TryFetch`, exercised by the audit-capable test persister | The EF implementation has to satisfy it, and the order is already stated. |
| Remote audit instances | `RemoteInstanceSetting`, `ScatterGatherApi`, `CheckRemotes`, `RemotePlatformConnectionDetailsProvider`, body forwarding in `GetMessagesController` | The audit host is one more remote. Nothing on the primary's read side is new for the dedicated topology. |
| Reporting from an audit instance | `ReportCustomCheckResultHandler`, `RegisterNewEndpointHandler` | The audit host reports the way the RavenDB audit instance does. |

## Schema

Three new tables in the existing schema. Column naming follows each provider's existing convention
(`snake_case` on PostgreSQL, `PascalCase` on SQL Server), as the current entity configurations do.

### AuditMessages

Partitioned by range on `created_on` (PostgreSQL only).

| Column | Notes |
| --- | --- |
| `created_on` | Ingestion time truncated to the hour. Partition key. |
| `id` | A `bigint` identity. Part of the key only because PostgreSQL requires the partition key in every unique constraint, and `created_on` alone is not unique. Nothing reads it back. |
| `unique_message_id` | `headers.UniqueId()`. Indexed. Joins an audit row to its failed counterpart and to its body. |
| `message_id` | |
| `message_type` | |
| `time_sent` | |
| `processed_at` | From `Headers.ProcessingEnded`, best-guess `UtcNow` when absent. This is what `MessagesView.ProcessedAt` shows, not `created_on`. |
| `conversation_id` | |
| `is_system_message` | |
| `status` | `Successful` or `ResolvedSuccessfully`, from the `IsRetried` metadata. |
| `sending_endpoint_name`, `sending_endpoint_host_id`, `sending_endpoint_host` | |
| `receiving_endpoint_name`, `receiving_endpoint_host_id`, `receiving_endpoint_host` | |
| `critical_time_ticks`, `processing_time_ticks`, `delivery_time_ticks` | |
| `headers_json` | |
| `body_text` | Inline body, or null. Produced by `MessageBodyClassifier`. |
| `body_stored_externally` | |
| `body_size`, `body_content_type` | |


Primary key `(created_on, id)`. Indexes on `(unique_message_id)`, `(receiving_endpoint_name, created_on)`,
`(conversation_id)`, `(processed_at)`, and the full-text index.

A column exists only where the read path filters, sorts, indexes or full-text searches on it.
Everything else is projected from `headers_json`, the way `MessagesViewMapper` already derives
`MessageIntent` and `BodyUrl` for failed messages. In particular `InvokedSagas` and
`OriginatesFromSaga` get no columns: `InvokedSagasParser.Parse` is a pure function of headers and
nothing queries them.

The spike's column list is good evidence for the write path it load tested, and is followed closely
here. It cannot answer read path questions, because its `EFAuditDataStore` returns an empty result
for every query.

### SagaSnapshots

Partitioned identically. Maps `ServiceControl.SagaAudit.SagaSnapshot`, which does not move.

`SagaSnapshotFactory` never assigns an id and RavenDB supplies a document id, so there is no natural
key to carry over. The id is a database generated identity and snapshots are not deduplicated.
Identity columns work on a partitioned table in PostgreSQL 16, verified directly, including inserts
that span partitions and the `LIKE INCLUDING ALL` clone the migration uses. The write path never reads
the value back, so letting the database generate it costs nothing.

The consequence, accepted knowingly: a redelivered saga audit message adds a second snapshot, which
appears as a duplicate step in the ServicePulse saga diagram. Deriving the id from the carrying
message would have deduplicated it; deriving it from saga content would have risked collapsing two
distinct state changes that share a `FinishTime` tick. A duplicate step is the more visible defect but
the recoverable one, and audit messages are not commonly redelivered.

| Column | Notes |
| --- | --- |
| `created_on` | Partition key. |
| `id` | Sequential. Part of the primary key only because PostgreSQL requires the partition key in it. |
| `saga_id` | Indexed with `created_on` to serve saga history. |
| `saga_type`, `status`, `start_time`, `finish_time`, `endpoint` | |
| `state_after_change` | |
| `initiating_message_json`, `outgoing_messages_json` | |

### FailedAuditImports

Not partitioned. Mirrors `FailedErrorImportEntity`, including the inline-or-external body split and
the `ExternalBodyId` prefix convention.

| Column | Notes |
| --- | --- |
| `unique_message_id` | Primary key. `FailedAuditImport.DeriveKey`, already shipped. |
| `failed_at`, `message_id`, `headers_json`, `body`, `body_stored_externally`, `exception_info` | |

No retention, matching failed error imports.

## Partitioning and retention

`IAuditPartitionManager`, implemented per provider, with the operations the spike settled on:
ensure partitions exist, list expired partitions, drop a partition.

### PostgreSQL

Native `PARTITION BY RANGE (created_on)`, hourly partitions named `{table}_{yyyyMMddHH}`. Dropping an
expired hour is `DETACH` then `DROP TABLE`, a metadata operation.

### SQL Server

No partitioning. A full-text index cannot be aligned to a partition scheme, which makes partition
truncation impossible, and the spike removed the partitioning infrastructure it had first built
(commits 4448035a16 then 6dd611302d) because composite keys, aligned indexes and pausing ingestion
during cleanup were not worth it. `DropPartition` is a batched `DELETE` over the hour's range,
paced the way `SweepFailedMessages` already paces.

This asymmetry is deliberate and load-tested. Do not re-add SQL Server partitioning without redoing
that work.

### Sweeper integration

`RetentionSweeper.Sweep` gains an audit pass, running after the existing error and event log passes:

1. Ensure partitions exist for the next 48 hours.
2. For each expired hour: delete the body prefix, then drop the partition or delete the rows.

Bodies before rows, matching the existing order and its reasoning: a crash in between leaves rows the
next sweep re-handles, where the reverse leaks bodies.

`AuditRetentionPeriod` is read from `Settings` into `EFPersisterSettings`, defaulting to 7 days when
null.

The audit pass and partition provisioning run only where audit data is local: on a primary with
`AuditDataLocation=Local`, and on the audit host. A persister setting carries that, the way
`AuditRetentionPeriod` does, so a primary whose audit is remote never provisions partitions for
tables nothing writes to.

### Locking

`RunRetentionSweep` is false on every ingestion-only worker, so in a correct deployment there is one
sweeper by construction. The lock exists for the incorrect deployment: two primaries both configured
to sweep. Concurrent batched deletes are merely wasteful, but two concurrent `DETACH`/`DROP TABLE`
against the same partition means the loser errors and abandons the rest of its pass, and an operator
running the additional primary processes this work enables is exactly who is likely to misconfigure
it.

Port `TryAcquireLock`/`ReleaseLock` from the spike's `RetentionCleaner`, which already has both
implementations:

- PostgreSQL: `SELECT pg_try_advisory_lock(hashtext('retention_cleaner'))`, released with
  `pg_advisory_unlock`.
- SQL Server: `sp_getapplock` with `@LockMode = 'Exclusive'`, `@LockOwner = 'Session'` and
  `@LockTimeout = 0`, released with `sp_releaseapplock`.

The lifecycle is the spike's, unchanged:

- A dedicated `DbConnection` is opened for the sweep and closed after it. Both locks are session
  scoped, so a host that crashes mid-sweep releases the lock when its connection drops rather than
  wedging retention until someone intervenes.
- Acquisition has a zero timeout. A sweeper that cannot take the lock logs and skips that pass
  instead of queueing behind the holder, because the work is idempotent and hourly.
- Release is in a `finally`.

`IRetentionLock` covers every sweep, not just the audit pass. Failed messages, event log items,
orphaned group comments and audit partitions are all taken under one acquisition. They already run
sequentially in a single `Sweep` method, so per pass locks would buy nothing, and the guarantee worth
stating is the simple one: at most one host is sweeping anything at any moment.

This makes error and event log retention lock protected for the first time. That is a behaviour change
to shipped code, in the safe direction, and it is intended rather than incidental.

`IRetentionLock` is a new per provider seam alongside `IAuditPartitionManager`.

## Body storage

Audit bodies are keyed `audit/{yyyy-MM-dd-HH}/{uniqueMessageId}`, where the hour is the row's
`created_on`. The hour in the key is what lets retention delete a partition's bodies in one operation
per provider rather than one per message.

`IBodyStoragePersistence` gains:

```csharp
Task DeleteBodiesWithPrefix(string prefix, CancellationToken cancellationToken = default);
```

- FileSystem: delete the directory.
- AzureBlob: delete by blob prefix.
- S3: list and bulk delete by key prefix.

`BodyStorage.TryFetch` implements the arbitration order `IBodyStorage` already documents. Step three
resolves the audit row by `unique_message_id`, returning the inline `body_text` where present and the
external body otherwise. Because the audit body key contains the hour, resolving it needs the row
first, which the query already fetches.

In the dedicated topology the primary never reaches step three: Remote mode removes the local audit
source, and an audit body is fetched from the audit host by instance id through the forwarding
`GetMessagesController` already does for a RavenDB remote.

## Full-text search

The audit index mirrors `FullTextSearchSql` for failed messages: a GIN index over
`to_tsvector('simple', headers_json || body_text || message_type)` on PostgreSQL, a full-text catalog
on SQL Server, applied by migration because EF cannot model either. `IFullTextSearchDialect` gains an
audit overload, and the audit equivalent of `FullTextSearchIndexTests` pins the query expression to
the indexed expression.

`EnableFullTextSearchOnBodies` stays unread, as it is on the error side today. Making it real is a
separate change that has to answer what happens to an existing index, and it should change both
sides at once or neither.

## Query implementation

### The five message view queries

Each view is one SQL statement over both tables. The database applies the filters, the precedence
rule, the sort, the page and the count; nothing is merged in memory.

```sql
SELECT <common projection> FROM failed_messages f WHERE <filters>
UNION ALL
SELECT <common projection> FROM audit_messages a
WHERE <filters>
  AND NOT EXISTS (SELECT 1 FROM failed_messages f WHERE f.unique_message_id = a.unique_message_id)
ORDER BY <sort> LIMIT @take OFFSET @skip
```

The three rules `IMessagesViewDataStore` states are each one clause of that statement:

1. **Precedence.** A message that both failed and was audited shows as failed. The anti-join on the
   audit branch drops the audit row. `unique_message_id` is the same deterministic value on both
   tables, message id plus processing endpoint, and it is the primary key of `failed_messages` and
   indexed on `audit_messages`, so the anti-join is one key probe per surviving audit row. It is not
   a `ROW_NUMBER() OVER (PARTITION BY ...)`, which would force both tables to be materialised before
   the first row could be returned.
2. **Paging.** `OFFSET` and `LIMIT` sit on the union, so page N is exact. Both providers stream a
   top-N over two ordered index scans (Merge Append on PostgreSQL, Merge Concatenation on SQL
   Server) and stop at the page boundary rather than reading either table through.
3. **Counting.** The total is a second statement with the same two predicates, `COUNT(*)` on each
   branch with the anti-join on the audit side, so a message in both tables counts once.

Each branch keeps its own indexes, including its own full-text index, because the planner plans
each branch on its own. The full-text predicate reaches each branch through the existing
`IFullTextSearchDialect` seam, and the audit copy of `FullTextSearchIndexTests` pins the audit
branch's expression to its index the way the failed one is pinned today. Everything else is LINQ:
`Concat` over two projections to a shared row type and `Any` for the anti-join, which EF Core
translates to the statement above on both providers.

Two constraints follow. Both branches must project the same column set, which is why the audit
columns in "Schema" were chosen from what `MessagesView` shows. And every sortable column must be
indexed on both tables, or a sort becomes a sort of the whole filtered set: `time_sent` and
`processed_at` are, and the remaining sort keys the API accepts (`critical_time`, `delivery_time`,
`processing_time`, `message_type`, `status`) get a decision in step 4, either an index on both
tables or documented as unindexed sorts.

`LocalMessagesView.Merge` stays only behind the in-memory test persister, which holds both kinds of
message in memory and has no database to hand the work to. Once step 4 lands it serves no shipped
code path, and step 6 deletes it with the test persister.

On a Remote-mode primary and on the audit host one branch has no rows by construction, and the
composition registers an empty source for it rather than querying an empty table. See "Topologies".

`GetAllMessagesByConversation` has no page bound in practice and needs a cap.

### Audit counts

`GROUP BY` over `created_on::date` filtered by `receiving_endpoint_name`, bounded by the retention
window, served by the `(receiving_endpoint_name, created_on)` index. Runs once a day per endpoint
from the licensing throughput collector, which is its only caller.

### Saga history

Select the snapshots for a saga id, ordered by `finish_time` descending, projected into
`SagaHistory.Changes`. Bounded by a cap: a long-running saga can have thousands of snapshots.

## Ingestion write path

`EFIngestionUnitOfWork.Audit` stops returning null and returns an `EFAuditIngestionUnitOfWork` that
buffers into thread-safe collections, exactly as the recoverability child does. `Complete` writes
audit rows inside the existing transaction, after the failed message upsert and before the commit,
so a batch's audit rows and its known endpoints commit together.

Inserts are a plain parameterised multi-row `INSERT`, reusing the existing `ParameterRows` helper. No
conflict clause, no provider specific dialect: with nothing to deduplicate the statement is the same
on both providers apart from identifier quoting.

External body writes are queued on the existing `RecordBodyWrite` path, so they complete before the
rows that point at them.

## Scale-out and idempotency

- Audit inserts are plain inserts with a database generated id, so competing consumers never collide.
- Known endpoint upserts are unchanged and already safe.
- Failed audit imports key on `FailedAuditImport.DeriveKey`, already shipped, so a poison message
  produces one row rather than one per attempt per worker.
- Body writes are idempotent: same key, immutable content.
- Only the retention owner issues DDL or deletes, and the advisory lock holds that to one host even
  when two are configured to sweep.

One known gap, stated rather than hidden: a redelivered audit message or saga audit message always
produces a second row. Neither table deduplicates. RavenDB does, so this is a behaviour difference
between the two audit persisters and not merely a scale-out caveat.

The dedicated topology adds no new kind of writer. The audit host and its workers are the same
competing consumers against one database, and the primary is the only writer to its own.

## PR sequence

1. **Schema and migrations.** Entities, configurations, `DbContext` registration, and per-provider
   migrations including the PostgreSQL partitioning DDL. No behaviour: nothing writes or reads yet,
   and the manifests still say false. The full-text index is not here, see step 4.
2. **Ingestion write path.** `IAuditIngestionSqlDialect`, `EFAuditIngestionUnitOfWork`, wiring
   `EFIngestionUnitOfWork.Audit`. Tested through the persistence test base.
3. **Retention, partitions and locking.** `IAuditPartitionManager` and `IRetentionLock` per provider,
   the sweeper's audit pass, the body prefix delete on all three body stores, the lookahead custom
   check.
4. **Queries.** The five message view unions, audit counts, saga history, and the third step of body
   arbitration. The full-text index lands here rather than with the schema, because the indexed
   expression and the query expression have to be written together or PostgreSQL silently downgrades
   to a sequential scan, which is what the pinning test exists to catch.
5. **Failed audit imports.** The store, and the `--import-failed-audits` round trip.
6. **Turn it on.** Flip `SupportsAuditIngestion` in both manifests, update the approval test that
   asserts it is false, and delete `ServiceControl.Persistence.Tests.AuditCapable`. The `Empty*`
   audit stand-ins stay: step 7 registers them on a primary whose audit is remote. Full acceptance
   runs on both providers.
7. **Dedicated audit database.** `--audit-instance` and its guards, the controller allow list,
   `AuditDataLocation` and the primary's Remote behaviour, `ServiceControlQueueAddress` on the
   primary executable with the send-only endpoint and the reporting switch, and the persister
   setting that gates the audit retention pass, and the workers' migration probe. Acceptance test:
   a primary and an audit host against two databases (two schemas in the test harness), audit
   messages ingested by the host and read through the primary, a custom check and a detected
   endpoint raised on the host and visible on the primary.
8. **Documentation.** `docs/audit-ingestion-in-the-primary.md` gains the topology tables, the new
   mode and the two settings, and the hosting plan's statement that there is no separate audit HTTP
   service is marked superseded.

Each pull request leaves both EF acceptance suites and the RavenDB suites passing, and steps 1 to 5
leave `SupportsAuditIngestion` false so nothing activates early. Step 1 is on this branch, rebased
onto master on 14 September 2026.

## Testing

- `ServiceControl.Persistence.Tests.SqlServer` and `.PostgreSql` cover the write path, retention,
  partition lifecycle and the queries against real databases.
- The full-text index tests are duplicated for audit, pinning query expression to index expression on
  both providers.
- Partition lifecycle: partitions created ahead, expired partitions dropped, bodies for the dropped
  hour gone, and rows in live partitions untouched.
- Retention locking: a second sweeper against the same database skips its pass rather than failing,
  and a dropped lock connection frees the lock for the next sweep.
- A redelivered audit message and a redelivered saga audit message each produce two rows. Asserted
  rather than left to chance, because it differs from RavenDB.
- The precedence, paging and counting rules from `IMessagesViewDataStore`, now against real SQL rather
  than the in-memory test persister: a message in both tables shows as failed and counts once, page
  two is exact when page one held duplicates, and the total matches `failed + audited - overlap`.
- Query plans, both providers: the union with a sort and a page does not read either table through,
  and the search view uses both full-text indexes. Asserted from `EXPLAIN` output the way
  `FullTextSearchIndexTests` already does.
- The acceptance tests from the hosting plan keep running, and step 6 makes them run against a real
  audit-capable persister for the first time.
- Dedicated topology, both providers: ingestion on the audit host and on a worker, queries through
  the primary's scatter-gather, precedence between a failed message on the primary and its audit row
  on the host, and a body fetched by instance id.
- Remote mode on the primary: no audit partitions are provisioned, the audit tables stay empty,
  `--import-failed-audits` refuses, and a timed-out audit host surfaces as a timeout rather than an
  empty result.
- Reporting: a custom check and a detected endpoint raised on the audit host appear on the primary,
  and a shared topology worker with `ServiceControlQueueAddress` unset still writes both locally.
- Guards: the audit host refuses to start without `ServiceControlQueueAddress`, with remotes
  configured, or on a persister without audit support.
- Ownership: an audit host one migration behind the primary still answers the scatter-gather, and a
  worker started against an unmigrated database refuses to start and names the owner's `--setup`.

## Open items

1. What supplies the SQL Server full-text `KEY INDEX`. It must be a single column unique index, and
   the audit primary key is the composite `(created_on, id)`. Options are a SQL Server only surrogate
   identity column, or letting SQL Server key on `id` alone, which would dedupe across hour boundaries
   and so behave better than PostgreSQL rather than the same. Needed for step 4, not step 1.
2. What caps `GetAllMessagesByConversation` and saga history, and what the API returns when a cap is hit.
   Also whether the total count on an unfiltered message view stays exact, which is a count over
   the whole audit table on every request, or becomes estimated or capped. This is the one query
   cost the union does not remove, because no design can count without touching the table.
3. Whether the audit path should raise the `EndpointDetected` domain event. Carried over from the
   hosting plan, still unanswered, and now cheap to settle because the write path is real.
4. Whether `SagaUpdatedHandler` should hand the snapshot straight to the audit unit of work instead of
   forwarding it to the audit queue. Also carried over.
5. Whether the 48 hour partition lookahead and the 12 hour custom check threshold are the right
   numbers, which is a question for whoever runs the load tests.
6. Whether the audit host reports failed audit imports under `Audit Message Ingestion (local)` or
   under the RavenDB audit instance's id. See "Reporting back to the primary".
7. Whether `/api/endpoints/known` on the primary should also merge the audit host's known endpoints
   through the scatter-gather, which a RavenDB audit instance also serves, rather than relying on
   `RegisterNewEndpoint` alone.
8. What the audit host answers on `/api/configuration` for the fields licensing reads, and whether
   `CheckRemotes` should tell an EF audit host apart from a RavenDB audit instance in its message.
