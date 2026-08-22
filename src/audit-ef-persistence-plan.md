# EF Audit Persistence for the Primary Instance

## Summary

Implement audit storage and querying in the existing primary EF persisters, so `SupportsAuditIngestion`
can be flipped to `true` and the audit runtime already hosted in the primary starts writing and
reading real data.

This is the implementation half of [Host Audit Ingestion in the Primary Instance](audit-ingestion-in-primary-plan.md).
That plan delivered the contracts, the copied runtime, the settings, the fail-fast command and the
composition. Nothing in it stores an audit message. This plan does.

The audit-only spike ([#5318](https://github.com/Particular/ServiceControl/pull/5318)) remains the
evidence base for partitioning, retention and full-text search. It is not the design: it targeted a
standalone audit instance with its own database, its own `DbContext` and its own persistence
contracts. Here the audit tables join the primary's model.

## Goals

- Store audit messages, saga snapshots and failed audit imports in the primary database.
- Serve the five `IMessagesViewDataStore` queries from a union of failed and audited messages.
- Serve audit counts, saga history, and audit body resolution.
- Keep the spike's PostgreSQL hourly range partitioning and its retention economics.
- Keep ingestion safe under competing consumers.
- Flip `SupportsAuditIngestion` to `true` on both EF manifests as the last step.

## Non-goals

- Any change to RavenDB, on either instance.
- Migrating existing RavenDB audit data.
- Changing the audit runtime, the host composition, or the settings surface. Those shipped already.
- Making `EnableFullTextSearchOnBodies` real. See "Full-text search".

## Decisions

Settled by interview on 22 Aug 2026.

| Decision | Choice |
| --- | --- |
| Model boundary | Audit tables join `ServiceControlDbContext` and the existing per-provider migration stream. Partitioning is applied by raw SQL inside a migration, the way `AddFullTextSearch` already applies the GIN index. |
| Partition creation | The retention sweeper alone creates partitions ahead. Ingestion-only workers never issue DDL. |
| Audit body keys | `audit/{yyyy-MM-dd-HH}/{uniqueMessageId}`, with a new delete-by-prefix operation on `IBodyStoragePersistence`. |
| Full-text search | Mirror the error side: index the audit table's own columns through the existing `IFullTextSearchDialect` seam. No `SearchableContent` column. |
| Audit message identity | `created_on` is the ingestion hour. Primary key is `(created_on, id)` where `id` is the deterministic processing id, so a redelivery within the hour collapses. |
| Saga snapshot identity | Sequential `id`, no dedupe. A redelivered saga audit message produces a second snapshot. |
| Audit counts | Aggregate query over the audit table, served by an index on `(receiving_endpoint_name, created_on)`. No rollup table. |
| `AuditRetentionPeriod` null | 7 days, matching SCMU and the Dockerfile. |
| Retention ownership | Single owner via `RunRetentionSweep`, plus a session scoped advisory lock ported from the spike so two misconfigured primaries cannot sweep at once. |

### Why the partition key is the ingestion hour

PostgreSQL requires every unique constraint on a partitioned table to include the partition key, so
the row's identity and its partition are one decision.

`created_on` is `UtcNow` truncated to the hour, as in the spike. A redelivery of the same audit
message within the hour collapses onto the same primary key. A redelivery that straddles an hour
boundary produces a second row.

The alternative, partitioning on the message's own processing time, dedupes reliably but lets a late
or replayed message target a partition retention has already dropped. `--import-failed-audits`
replays old messages by design, so that failure mode is reachable in normal operation. The bounded
duplicate is the cheaper defect.

State this in the acceptance criteria rather than claiming exact-once.

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

## Schema

Three new tables in the existing schema. Column naming follows each provider's existing convention
(`snake_case` on PostgreSQL, `PascalCase` on SQL Server), as the current entity configurations do.

### AuditMessages

Partitioned by range on `created_on` (PostgreSQL only).

| Column | Notes |
| --- | --- |
| `created_on` | Ingestion time truncated to the hour. Partition key. |
| `id` | Deterministic processing id: `DeterministicGuid.MakeId(messageId, processingEndpoint, processingStarted)`, or a fresh Guid when any of those headers is absent. |
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
| `invoked_sagas_json`, `originates_from_saga_json` | `MessagesView` carries these and the enrichers already produce them. The spike dropped them, which would have regressed the message view. |

Primary key `(created_on, id)`. Indexes on `(unique_message_id)`, `(receiving_endpoint_name, created_on)`,
`(conversation_id)`, `(processed_at)`, and the full-text index.

### SagaSnapshots

Partitioned identically. Maps `ServiceControl.SagaAudit.SagaSnapshot`, which does not move.

`SagaSnapshotFactory` never assigns an id and RavenDB supplies a document id, so there is no natural
key to carry over. The id is sequential and snapshots are not deduplicated.

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

Each becomes a union of failed and audited rows, merged through `LocalMessagesView.Merge`, which
already implements the precedence, paging and counting rules.

The merge is in memory over at most two pages of rows, not in SQL. Two ordered `Take(PageSize)`
queries, one per source, then merge. A SQL `UNION ALL` with a window function would push the work
into the database, but it defeats the full-text index on both providers and cannot express
"failed wins" without a second pass anyway.

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

Inserts go through a new `IAuditIngestionSqlDialect`:

- PostgreSQL: `INSERT ... ON CONFLICT (created_on, id) DO NOTHING`.
- SQL Server: `MERGE ... WITH (HOLDLOCK) ... WHEN NOT MATCHED THEN INSERT`.

`DO NOTHING` rather than an update: an audit row is immutable once written, and a redelivery carries
identical content.

External body writes are queued on the existing `RecordBodyWrite` path, so they complete before the
rows that point at them.

## Scale-out and idempotency

- Audit inserts are insert-if-missing on a deterministic key, so competing consumers converge.
- Known endpoint upserts are unchanged and already safe.
- Failed audit imports key on `FailedAuditImport.DeriveKey`, already shipped, so a poison message
  produces one row rather than one per attempt per worker.
- Body writes are idempotent: same key, immutable content.
- Only the retention owner issues DDL or deletes, and the advisory lock holds that to one host even
  when two are configured to sweep.

Two known gaps, stated rather than hidden:

- A redelivery that crosses an hour boundary produces a second audit row.
- A redelivered saga audit message always produces a second saga snapshot, because snapshots carry no
  deduplication key.

## PR sequence

1. **Schema and migrations.** Entities, configurations, `DbContext` registration, per-provider
   migrations including the partitioning and full-text SQL. No behaviour: nothing writes or reads yet,
   and the manifests still say false.
2. **Ingestion write path.** `IAuditIngestionSqlDialect`, `EFAuditIngestionUnitOfWork`, wiring
   `EFIngestionUnitOfWork.Audit`. Tested through the persistence test base.
3. **Retention, partitions and locking.** `IAuditPartitionManager` and `IRetentionLock` per provider,
   the sweeper's audit pass, the body prefix delete on all three body stores, the lookahead custom
   check.
4. **Queries.** The five message view unions, audit counts, saga history, and the third step of body
   arbitration.
5. **Failed audit imports.** The store, and the `--import-failed-audits` round trip.
6. **Turn it on.** Flip `SupportsAuditIngestion` in both manifests, update the approval test that
   asserts it is false, and delete `ServiceControl.Persistence.Tests.AuditCapable` along with the
   `Empty*` audit data stores it stood in for. Full acceptance runs on both providers.

Each pull request leaves both EF acceptance suites and the RavenDB suites passing, and steps 1 to 5
leave `SupportsAuditIngestion` false so nothing activates early.

## Testing

- `ServiceControl.Persistence.Tests.SqlServer` and `.PostgreSql` cover the write path, retention,
  partition lifecycle and the queries against real databases.
- The full-text index tests are duplicated for audit, pinning query expression to index expression on
  both providers.
- Partition lifecycle: partitions created ahead, expired partitions dropped, bodies for the dropped
  hour gone, and rows in live partitions untouched.
- Retention locking: a second sweeper against the same database skips its pass rather than failing,
  and a dropped lock connection frees the lock for the next sweep.
- Redelivery of an audit message within an hour produces one row. Redelivery across an hour boundary
  produces two, and a redelivered saga audit message always produces two. All three are asserted
  rather than left to chance.
- The precedence, paging and counting rules from `IMessagesViewDataStore`, now against real SQL rather
  than the in-memory test persister.
- The acceptance tests from the hosting plan keep running, and step 6 makes them run against a real
  audit-capable persister for the first time.

## Open items

1. What caps `GetAllMessagesByConversation` and saga history, and what the API returns when a cap is hit.
2. Whether the audit path should raise the `EndpointDetected` domain event. Carried over from the
   hosting plan, still unanswered, and now cheap to settle because the write path is real.
3. Whether `SagaUpdatedHandler` should hand the snapshot straight to the audit unit of work instead of
   forwarding it to the audit queue. Also carried over.
4. Whether the 48 hour partition lookahead and the 12 hour custom check threshold are the right
   numbers, which is a question for whoever runs the load tests.
