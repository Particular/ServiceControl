# Error ingestion design (relational persisters)

## Overview

The error instance receives failed messages on its input queue and stores them so they can be
queried, grouped, retried, and eventually archived. This document describes how ingestion works
for the relational persisters (PostgreSQL and SQL Server) and explains the design decisions that
are not obvious from the code, in particular why the write path is hand-written SQL rather than
ordinary change-tracked entity saves.

The unit of ingestion is a **batch**: the transport hands the ingester up to `MaximumConcurrency`
messages at a time, and the whole batch is written in a single database transaction. The relevant
types are `EFIngestionUnitOfWork` (accumulation), `FailedMessageBatchWriter` (the write), and the
per-provider `IIngestionSqlDialect` implementations (the statements that differ by provider).

## Data model

The schema stores **one row per failed message**, keyed by `UniqueMessageId`, in the
`FailedMessages` table. There is deliberately **no attempts history table**. A message that fails
repeatedly keeps a single row that records:

- the **last** processing attempt (every denormalized and payload column comes from it),
- the **number of distinct attempts** (`NumberOfProcessingAttempts`),
- the **failure window** (`FirstTimeOfFailure`, `LastTimeOfFailure`).

This is a deliberate difference from the document-database persister, which retained an array of
attempts. The read side only ever consumed the last attempt plus the count, so storing the full
history earned nothing and cost write amplification. It is also an improvement: because the count
is a column rather than the length of a capped array, `NumberOfProcessingAttempts` always reports
the true number of attempts, where Raven's implementation silently stopped counting once the
retained array hit its cap of ten.

### Stored source data vs derived columns

Only two pieces of data are **stored as source of truth**: the full headers dictionary
(`HeadersJson`) and the message body. Every other column (message type, endpoints, exception
details, timestamps, and so on) is a **derived extraction** written purely so it can be indexed
and queried. On read, the `FailureDetails` object and the metadata dictionary the rest of the
system expects are **reconstructed** from the headers and these columns. Nothing downstream of
ingestion reads a column expecting it to carry information the headers do not already contain.

`BodyUrl`, `ContentType`, and `ContentLength` are examples worth calling out: the document store
persisted them into a metadata dictionary, but they are all derivable (`BodyUrl` from the
`UniqueMessageId`, the other two from the `BodyContentType` and `BodySize` columns), so they are
not stored again.

### Body placement

Bodies are **always** stored. The `MaxBodySizeToStore` setting (default 100 KB) only decides
*where*:

- text at or under the cap: stored inline in `BodyText`, nothing external.
- text over the cap: the full body goes to external storage, and a **search prefix** of at most
  the cap, cut on a valid UTF-8 boundary, is kept inline in `BodyText` so search still works.
- binary, or not strictly UTF-8 decodable, or containing a NUL byte: external storage only,
  `BodyText` is null, regardless of size.

When `BodyStoredExternally` is true the external copy is authoritative and `BodyText` is a
search aid only; it must never be served as the body. `BodySize` is always the true original
size. External writes happen before the row that points at them is committed.

### Groups, endpoints, retention

- **Failure groups** live in `FailedMessageGroups`, an association table. A message's group rows
  are **replaced wholesale** on every attempt, because grouping is recomputed from the latest
  attempt. Group views are query-time aggregates and are not part of ingestion.
- **Known endpoints** are written in the same batch, **insert-if-absent**. An existing endpoint
  row is never updated, which preserves the user-controlled `Monitored` flag.
- **Retention** is driven by `StatusChangedAt`. A background sweeper deletes `Resolved` and
  `Archived` rows older than `now - ErrorRetentionPeriod`, with the cutoff recomputed on every
  run so a changed retention setting takes effect without rewriting rows. A filtered index on
  `StatusChangedAt` restricted to those two statuses keeps the sweep cheap.

### Full-text search

Search over headers and body is provider-native and set up with raw SQL in the migrations: a
stored generated `tsvector` column with a GIN index on PostgreSQL, and a full-text catalog and
index on SQL Server. This is orthogonal to the write path but is another place where the
relational features we want have no portable expression.

## The write path

`RecordFailedProcessingAttempt` is called **concurrently** for the messages in a batch (the
ingester fans the batch out). `RecordKnownEndpoint` and `RecordSuccessfulRetry` are called
afterwards. An EF `DbContext` is not thread-safe and must not be touched from multiple threads,
so the `Record*` methods do no database work at all: they only enqueue into thread-safe
collections. **All** database access happens later, on one thread, in `Complete`.

`Complete` first waits for any external body writes, then hands the accumulated work to
`FailedMessageBatchWriter.Write`, which:

1. **Folds** the accumulated attempts in memory into one row per message plus its group rows. The
   fold sorts a message's attempts by time, takes the last as the winner, counts distinct attempt
   timestamps, and computes the failure window. The result is sorted by `UniqueMessageId` so that
   concurrent writers tend to take row locks in the same order.
2. Opens **one transaction** and runs the statements in a fixed order:
   1. upsert the failed-message rows,
   2. delete then re-insert the affected messages' group rows,
   3. insert-if-absent the known endpoints,
   4. resolve confirmed retries (set them `Resolved`, delete their retry rows),
   5. commit.

The order matters: a message that both fails and is retry-confirmed in the same batch must end
`Resolved`, so the retry resolution runs last.

## Why the write is raw SQL

Most of ServiceControl prefers standard abstractions, and the portable parts of this write path do
use them: the group delete and the retry resolution are ordinary set-based EF operations
(`ExecuteDelete`/`ExecuteUpdate`). The **upserts** are hand-written SQL, per provider, behind the
`IIngestionSqlDialect` seam. Three requirements together force that, and no ORM-level API satisfies
all three at once.

### 1. The upsert is a conditional merge, not a save

Writing a failed message is not "insert this row" or "update this row". For a message that already
exists the statement must, in one shot:

- flip the status back to `Unresolved`, and reset the retention clock **only** if the row was
  previously resolved or archived,
- add the batch's attempt count, but **not** if the batch merely redelivered the attempt already
  stored (equal timestamps),
- widen the failure window (min of the first, max of the last),
- replace every payload column with the incoming values **only if** the incoming attempt is at
  least as new as the stored one, so that an out-of-order older attempt still counts but does not
  overwrite newer data.

Those are per-column conditional expressions comparing the incoming row against the pre-update
stored row. A change-tracked save cannot express them: it would have to read every row first,
decide in memory, and write back, which is both slower and a race (see below). The logic has to
execute **inside a single set-based statement** where every guard reads the same consistent row
state.

### 2. It must be correct under concurrent writers

The instance runs a single ingestion loop today, but the write path is built so that multiple
instances could ingest against the same database (for example to scale out under load). That means
two transactions can try to write the **same** `UniqueMessageId` at the same time. Two hazards
follow:

- **Insert races.** Both writers see the row as absent and both insert, colliding on the primary
  key.
- **Read-modify-write cost.** EF's optimistic concurrency (a rowversion/xmin token) would catch a
  conflicting write instead of silently losing it, but only via a read before every write and a
  retry loop per message, the per-row round trip reason 3 rules out, and it still can't express the
  conditional merge from reason 1.

Closing the insert race requires the database's own concurrency-safe upsert primitive, and those
are **provider-specific**:

- PostgreSQL: `INSERT ... ON CONFLICT (unique_message_id) DO UPDATE`. The conflict clause makes a
  concurrent insert fall through to the update instead of failing, and the whole statement is
  atomic so the count arithmetic cannot lose an increment.
- SQL Server: `MERGE ... WITH (HOLDLOCK)`. The lock hint serializes concurrent merges on the same
  key so the second one sees the row and updates instead of colliding.

These have no common surface. `ON CONFLICT` and `MERGE` are different grammars with different
concurrency semantics (a plain `MERGE` on PostgreSQL is **not** insert-race safe, which is exactly
why the PostgreSQL side uses `ON CONFLICT` instead). Expressing "the concurrency-safe conditional
upsert for this database" therefore means writing the statement each database actually needs.

### 3. It is set-based over a whole batch

A batch can hold many messages. Saving them as tracked entities would be a statement per row and
would, on SQL Server, run into the 2100-parameter limit for larger batches. The dialects instead
send **chunked multi-row statements** (up to 50 rows each): a handful of fixed statement shapes
that the database can cache a plan for, with no per-row round trips and no temporary tables.

### What stays portable

Only the genuinely divergent statements are raw. The retry resolution and the group delete are set
based and identical across providers, so they remain EF operations in the shared writer. The raw
SQL is confined to the two dialect classes, one per provider, each responsible only for the
upserts. The guard semantics are kept identical between the two dialects; the shared test suite
runs every ingestion test against both providers to keep them from drifting.

## Transactions and retries

Everything in a batch runs in one transaction opened by the writer. The raw dialect commands are
explicitly enlisted onto that transaction, and the EF `ExecuteUpdate`/`ExecuteDelete` operations
participate in it as well, so a failure anywhere rolls the whole batch back. Nothing is committed
piecemeal.

The transaction is wrapped in the provider's execution strategy so that a transient failure (a
dropped connection, or a deadlock between concurrent writers) retries the **entire** batch. This is
safe because the batch is **idempotent**: re-running it folds to the same rows, the upsert is a
merge, the group rows are deleted and re-inserted, endpoints are insert-if-absent, and retry
resolution is a set update plus delete. Replaying a batch after an ambiguous commit changes
nothing. Stable lock ordering (the fold sorts by `UniqueMessageId`) keeps deadlocks rare in the
first place.
