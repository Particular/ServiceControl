# Saga timeout management design

Status: proposal. The endpoint side exists on the NServiceBus `saga-timeout-full-messages` branch. ServiceControl and ServicePulse do not implement any of this yet.

## What it is

Saga timeout management lets an operator see every pending saga timeout in the system, see how each timeout changed over time, and reschedule, trigger, or cancel a timeout from ServicePulse.

The design rests on one rule: the endpoint owns the timeout, and ServiceControl keeps a projection of it. The saga persister stores the timeouts with the saga data. Every committed change is reported to the ServiceControl primary instance as a message. ServiceControl never changes a timeout directly. An operator action is a request sent to the endpoint, which applies it to the saga and reports the result through the same messages as any other change.

This document covers:

- the endpoint side: message contracts and the management satellite
- how the primary instance ingests and orders the messages
- the persistence model for RavenDB, SQL Server, and PostgreSQL
- the HTTP API and operator actions
- the ServicePulse pages, including the saga diagram additions
- rollout, compatibility, and open questions

## Endpoint side

### Timeout storage and dispatch

A saga persister opts in by implementing `ISupportTimeoutManagement`. The saga API is `RequestTimeout`, `FindTimeouts<T>`, `RescheduleTimeout`, and `CancelTimeout`. The persister stores a list of `StoredSagaTimeout` with the saga data:

| Field | Meaning |
|---|---|
| `TimeoutId` | Created when the timeout is requested. It never changes. |
| `TriggerId` | Identifies the current scheduled instance. It is regenerated on every reschedule. |
| `Version` | Starts at 1. It increments on every reschedule. Cancel does not increment it. |
| `DueTime` | The last requested due time. |
| `MessageType` | Full name of the timeout message type. |
| `ContentType`, `Payload` | The serialized timeout message. |

After the saga is saved, the endpoint sends each new or rescheduled timeout as a normal delayed message to its own input queue. The `NServiceBus.SagaTimeoutId` header carries `{TimeoutId}-{TriggerId}`. When the message arrives, the endpoint fires the timeout only if a stored timeout matches that identity. Otherwise the message is stale and is dropped without invoking the handler. Firing removes the timeout from storage in the same save as the handler's saga changes.

### Reporting

Reporting is opt-in: `endpointConfiguration.ReportSagaTimeoutsToServiceControl()`. It needs a persister that implements `ISupportTimeoutManagement`. The reports go to the primary instance queue. The default queue is `Particular.ServiceControl`; `ServiceControlErrorInstanceQueue(...)` overrides it.

| Message | Sent when | Extra fields |
|---|---|---|
| `SagaTimeoutRequested` | The saga called `RequestTimeout` | `TimeoutId`, `TimeoutType`, `Version`, `TriggerId`, `DueTime`, `PayloadContentType`, `Payload` |
| `SagaTimeoutRescheduled` | The saga or an operator rescheduled a stored timeout | Same as `SagaTimeoutRequested`, plus `OperationId` |
| `SagaTimeoutCanceled` | The saga or an operator canceled a stored timeout | `TimeoutId`, `TimeoutType`, `Version`, `OperationId` |
| `SagaTimeoutTriggered` | A stored timeout fired and the handler succeeded | `TimeoutId`, `TimeoutType`, `Version`, `TriggerId` |
| `SagaTimeoutChangeRejected` | A reschedule or cancel could not be applied | `TimeoutId`, `RequestedChange`, `RequestedDueTime`, `Reason`, `OperationId` |
| `SagaCompleted` | A saga that had stored timeouts when it was loaded completed | none |

Every message also carries:

- `EndpointName`, `SagaType`, `SagaId`, and `CorrelationPropertyValue`
- `ChangedAt`, from the clock of the machine that made the change
- `Host` and `HostId`, the same values that heartbeats and custom checks report
- `ManagementAddress`, the transport address of the endpoint's management satellite

`OperationId` is set only for changes an operator requested. `Reason` is `TimeoutNotFound`, `VersionMismatch`, or `SagaNotFound`.

A completing saga reports `SagaTimeoutTriggered`, if a timeout fired, and `SagaCompleted`. It does not report the individual changes the completing handler made, because completion discards all of the saga's timeouts. A new saga that requests timeouts and completes in the same message reports nothing.

Reports from a saga handler are pending transport operations of the incoming message. A rolled-back handler reports nothing, and with the outbox the reports are dispatched exactly when the saga changes are. The transport headers are `NServiceBus.EnclosedMessageTypes` (the full type name, for example `NServiceBus.SagaTimeoutRequested`), `NServiceBus.ContentType: application/json`, and `NServiceBus.MessageIntent: Send`. The body is System.Text.Json with PascalCase property names, and `Payload` is base64.

### Management satellite

When reporting is enabled, the endpoint also runs a satellite named "Saga timeout management" on the queue `{endpoint queue}.SagaTimeouts`. It applies changes that an operator requests through ServiceControl. Its address is the `ManagementAddress` in every report.

A satellite bypasses the message pipeline, so the request is not deserialized. Every field travels in a header:

| Header | Value |
|---|---|
| `NServiceBus.SagaTimeoutManagement.OperationId` | GUID that ServiceControl assigns to the request |
| `NServiceBus.SagaTimeoutManagement.SagaType` | Saga type full name, as reported |
| `NServiceBus.SagaTimeoutManagement.SagaId` | Saga id |
| `NServiceBus.SagaTimeoutManagement.TimeoutId` | Timeout id |
| `NServiceBus.SagaTimeoutManagement.ExpectedVersion` | The `Version` the operator saw |
| `NServiceBus.SagaTimeoutManagement.Change` | `Reschedule` or `Cancel` |
| `NServiceBus.SagaTimeoutManagement.DueTime` | New due time in the NServiceBus wire format; required for `Reschedule` |
| `NServiceBus.ContentType` | Content type of the body; required when the body is not empty |

A `Reschedule` body, when present, replaces the timeout payload as is. An empty body keeps the stored payload. A `Cancel` must have an empty body.

The satellite uses the outbox the same way the receive pipeline does (`TransportReceiveToPhysicalMessageConnector`). For each request, it:

1. looks up the request's message id in the outbox; if a record exists, it dispatches the stored operations and skips to step 7
2. begins an outbox transaction and opens a synchronized storage session on it, or on the receive transaction when the outbox is disabled
3. finds the saga metadata by type name and loads the saga by id
4. compares `ExpectedVersion` with the stored timeout
5. applies the change, saves the saga, and completes the session
6. stores the rescheduled timeout message and the reports in the outbox record, commits, and dispatches them
7. marks the outbox record as dispatched

A missing saga, a missing timeout, or a different version produces `SagaTimeoutChangeRejected` and saves nothing. The rejection is still stored in the outbox record, so a duplicate request repeats the same rejection. A rescheduled timeout whose due time has passed is dispatched without a delay. "Trigger now" is a reschedule to the current time, so the saga handles it exactly like a timeout that came due.

A malformed request moves to the error queue without retries. Other failures, such as a persister concurrency conflict, use the default recoverability policy.

Without the outbox, the satellite has a gap. Suppose the saga save commits and the dispatch then fails. The retried request carries the old `ExpectedVersion`, so it is rejected with `VersionMismatch`. The change is saved but not reported, and the rescheduled timeout message is lost. ServiceControl shows the timeout as overdue, and the operator can reschedule it again. With the outbox, the retry finds the outbox record and dispatches the original operations.

### Delivery guarantees ServiceControl must handle

- Duplicates happen. Outbox replays, transport redelivery, and a retry after a commit whose dispatch failed all resend a report. No message carries a deduplication id.
- Reorder happens. The primary processes its input queue concurrently, and transports do not guarantee order.
- Loss happens without the outbox. Suppose the saga save commits and the dispatch fails. The retry then sees the timeout message as stale, so `SagaTimeoutTriggered` is never sent.
- A payload change without a reschedule is neither stored nor reported. A payload-only change is a reschedule with the same due time. It produces `SagaTimeoutRescheduled` with a new `Version` and `TriggerId`.

## Ingestion in the primary instance

### Message types and handlers

The primary declares local copies of the six contracts in `namespace NServiceBus` so that `NServiceBus.EnclosedMessageTypes` resolves by full name. The `EndpointUsageReport` contract in `Particular.LicensingComponent` sets the precedent. A new `SagaTimeoutMessagesSerializationContext` is added to the `TypeInfoResolverChain` in `NServiceBusFactory`.

A new `SagaTimeoutsComponent` is added to `ServiceControlMainInstance`. It registers one handler per message type, the data store, the event log mappings, and the API. Each handler converts its message into a `SagaTimeoutChange` record and calls one data-store method. The receive time comes from the injected `TimeProvider` and is stored as `ReceivedAt` beside the endpoint's `ChangedAt`.

The handlers do not run when the primary runs with `--error-ingestion-only`, because that mode does not host the NServiceBus endpoint. Another host of the same instance must process the input queue.

### Applying a change

Messages arrive in any order and more than once. The store therefore treats a timeout as a state that changes only forward. The order is defined by `(Version, Rank)`:

| Change | Status after | Rank |
|---|---|---|
| `Requested` | `Pending` | 0 |
| `Rescheduled` | `Pending` | 0 |
| `Triggered` | `Triggered` | 1 |
| `Canceled` | `Canceled` | 1 |

A change replaces the current state only if its `(Version, Rank)` is greater than the stored one. Otherwise only its history entry is recorded. Some consequences:

- A duplicate has the same key and changes nothing.
- `Canceled` at version 2 before `Rescheduled` at version 2 leaves the timeout `Canceled`. The reschedule is still recorded in the history.
- `Requested` at version 1 after `Rescheduled` at version 3 leaves version 3 current.
- The first change seen for a `TimeoutId` creates the timeout, whatever its kind. A `Canceled` without a prior `Requested` creates a `Canceled` timeout with no payload.

`Canceled` does not increment `Version`, so `Rescheduled` and `Canceled` can share a version. Rank breaks the tie, and terminal states win.

`SagaTimeoutChangeRejected` never changes state. It adds a history entry to the timeout named by `TimeoutId`. If the store has no such timeout, it creates one with status `Unknown`. Lists exclude `Unknown` timeouts. They exist so that the change history and operator feedback have a record to attach to.

### Saga completion

`SagaCompleted` removes everything ServiceControl knows about the saga's timeouts: current state, versions, history, and failed triggers. It then writes a completed-saga marker keyed by `SagaId`.

Any report for that saga that arrives after the marker is dropped. This covers a request reported in an earlier message that the primary processes after the completion. The marker expires with the retention period, so a report delayed for longer than that would create a stale `Pending` timeout. An operator can remove such a timeout with Forget.

### History entries and deduplication

Every message produces at most one history entry per timeout. Its deduplication key is:

- `{Kind}:{Version}` for `Requested`, `Rescheduled`, `Canceled`, and `Triggered`
- `Rejected:{RequestedChange}:{ChangedAt ticks}` for `SagaTimeoutChangeRejected`

Both persisters enforce the key, so applying the same message twice records one entry.

### Failed triggers

A timeout message that fails processing in the endpoint goes to the error queue. Error ingestion already stores it as a failed message with all headers. When a failed message carries `NServiceBus.SagaTimeoutId`, the ingestion unit of work also records a failed trigger: `{TimeoutId, TriggerId, FailedMessageId, FailedAt}`. This is one extra write per failed timeout message, and none for other messages. It is not a `MessageFailed` domain event handler, because that event does not carry the headers.

A timeout has a failed trigger when its current `TriggerId` matches a recorded failed trigger whose failed message is still unresolved. A successful retry produces `SagaTimeoutTriggered`, and the timeout leaves `Pending`. An archived failed message leaves the timeout `Pending` with a failed trigger. The operator then decides whether to reschedule, trigger, or cancel it.

### Overdue

Overdue is computed when a query runs. It is not stored. A timeout is overdue when it is `Pending`, has no failed trigger, and `DueTime < now - SagaTimeouts/OverdueGracePeriod`. The default grace period is 5 minutes, which absorbs delayed-delivery latency and clock drift. An overdue timeout usually means one of the following:

- the endpoint is down or its queue is backed up
- the delayed message was lost
- `SagaTimeoutTriggered` was lost, which can happen without the outbox

## Persistence

Both persisters implement `ISagaTimeoutDataStore`. The shared tests in `ServiceControl.Persistence.Tests` cover the apply rules above, including duplicates, reorder, and reports after completion. Each method owns its session or `DbContext`, commits before returning, and returns detached snapshots. This follows the coding guidelines.

```csharp
interface ISagaTimeoutDataStore
{
    Task<SagaTimeoutApplyResult> Apply(SagaTimeoutChange change, CancellationToken cancellationToken);
    Task<SagaTimeoutApplyResult> ApplySagaCompleted(SagaCompletedChange change, CancellationToken cancellationToken);

    Task<QueryResult<IList<SagaTimeoutView>>> Query(SagaTimeoutQuery query, PagingInfo paging, SortInfo sort, CancellationToken cancellationToken);
    Task<SagaTimeoutCounts> GetCounts(SagaTimeoutQuery query, CancellationToken cancellationToken);
    Task<IList<SagaTimeoutGroup>> GetGroups(SagaTimeoutQuery query, SagaTimeoutGrouping grouping, CancellationToken cancellationToken);
    Task<SagaTimeoutDetails?> Get(string timeoutId, CancellationToken cancellationToken);
    Task<IList<SagaTimeoutView>> GetBySaga(Guid sagaId, CancellationToken cancellationToken);
    Task<QueryResult<IList<SagaTimeoutHistoryEntryView>>> QueryHistory(SagaTimeoutHistoryQuery query, PagingInfo paging, CancellationToken cancellationToken);

    Task<OperatorRequestResult> RecordOperatorRequest(SagaTimeoutOperatorRequest request, CancellationToken cancellationToken);
    Task Forget(string timeoutId, CancellationToken cancellationToken);
}
```

`Apply` returns `Ignored` for a report that follows the saga's completion marker.

`SagaTimeoutQuery` carries:

- the tab: `Pending`, `Overdue`, or `FailedTrigger`
- the endpoint, saga type, and saga id
- the due-time window
- the search text

`SortInfo.AllowedSortOptions` for timeouts is `due_time`, `last_changed_at`, `endpoint`, `saga_type`, and `timeout_type`.

`RecordOperatorRequest` checks the expected version and stores the pending operation on the timeout. It returns `Accepted`, `VersionMismatch`, `NotPending`, or `NotFound`. These are explicit outcomes, not exceptions, as the guidelines require for expected concurrency conflicts.

### Logical model

| Entity | Key | Holds |
|---|---|---|
| Saga timeout | `TimeoutId` | Current state: endpoint, saga type and id, correlation value, timeout type, version, trigger id, due time, status, requested and last-changed times, last change kind and source, management address, host, pending operator operation |
| Timeout version | `TimeoutId`, `Version` | Payload content type and payload of that version |
| History entry | `TimeoutId`, dedup key | Kind, version, trigger id, due time, `ChangedAt`, `ReceivedAt`, source, operation id, rejection details |
| Completed saga | `SagaId` | Endpoint, saga type, `ChangedAt`, `ReceivedAt` |
| Failed trigger | `TimeoutId`, `TriggerId` | Failed message id, failed at |

The source of a change is `Operator` when the report carries an `OperationId`, and `Saga` otherwise. The management address is taken from the latest report. Payloads are stored per version, so the detail page can show "show change" between two versions. Lists never read payloads.

The detail page shows "Payload updated" when a `Rescheduled` entry keeps the due time of the previous version. That label is computed when the history is read; there is no separate change kind.

### RavenDB

One document per timeout holds the current state and its history. The `Changes` list is bounded at 100 entries. When the bound is reached, the oldest entries except `Requested` are dropped and `HistoryTruncated` is set. Payloads are stored as document attachments named `payload-v{Version}`, so the document stays small and list queries do not load payloads.

| Collection | Document id | Notes |
|---|---|---|
| `SagaTimeouts` | `SagaTimeouts/{TimeoutId}` | Current state, `Changes`, `PendingOperation`, `FailedTriggers` |
| `CompletedSagas` | `CompletedSagas/{SagaId}` | Completed-saga marker |

Failed triggers are embedded in the timeout document and are rarely more than a few. Error ingestion patches the document with a `PatchRequest` that creates it as `Unknown` when it does not exist yet. That case happens when the failure is ingested before the `Requested` report.

`Apply` loads the completed-saga marker and the timeout document in one session. If the marker exists, it returns `Ignored`. Otherwise it applies the rules and saves with `UseOptimisticConcurrency = true`. A `ConcurrencyException` is retried inside the method up to three times. Two concurrent reports for the same timeout are rare, so the retry almost never runs.

`ApplySagaCompleted` stores the marker and deletes the saga's timeout documents in one session. Deleting a document also deletes its attachments. The documents to delete come from a query on `SagaTimeoutsIndex` by `SagaId`, which waits for non-stale results. A saga has few timeouts, so the session stays small.

Indexes:

- `SagaTimeoutsIndex` maps `Status`, `DueTime`, `EndpointName`, `SagaType`, `SagaId`, `TimeoutType`, `CorrelationValue`, `LastChangedAt`, and `HasUnresolvedFailedTrigger`. It also maps a `Query` field for full-text search over correlation value, saga type, and timeout type. The `Overdue` and `Pending` tabs are range queries on `DueTime` against a cutoff the API computes.
- `SagaTimeoutHistoryIndex` is a fan-out index: `from t in docs.SagaTimeouts from c in t.Changes select ...`. It stores the fields the Change History tab shows, so that tab reads the index without loading documents.

`HasUnresolvedFailedTrigger` needs the failed message status, which lives in another collection. The index uses `LoadDocument` on `FailedMessages/{FailedMessageId}` for the current trigger only. RavenDB reindexes the timeout when that failed message changes.

Retention uses `@expires`:

- A timeout gets `@expires = terminal time + ErrorRetentionPeriod` when it becomes `Triggered` or `Canceled`.
- An `Unknown` timeout gets `@expires = ReceivedAt + ErrorRetentionPeriod`. A later report that makes it `Pending` removes the expiry with `ExpirationManager.CancelExpiration`.
- A completed-saga marker gets `@expires = ReceivedAt + ErrorRetentionPeriod`.
- `Pending` timeouts do not expire.

A changed retention setting applies to new expiries only, as for other RavenDB data.

### SQL Server and PostgreSQL

The entities live in `ServiceControl.Persistence.EFCore` with explicit `IEntityTypeConfiguration` classes. Each provider gets one migration, `AddSagaTimeouts`, generated with `dotnet ef migrations add`. PostgreSQL names come from the snake-case convention. The names below are the PostgreSQL ones; SQL Server uses the PascalCase equivalents.

```text
saga_timeouts
  timeout_id            varchar(450)  PK
  endpoint_name         varchar(450)
  saga_type             varchar(450)
  saga_id               uuid
  correlation_value     varchar(450)  null      -- FitToIndex for long values
  timeout_type          varchar(450)
  version               int
  trigger_id            varchar(450)  null
  due_time              timestamptz   null
  status                smallint                -- Unknown, Pending, Triggered, Canceled
  rank                  smallint
  requested_at          timestamptz   null
  last_change_kind      smallint
  last_change_source    smallint                -- Saga, Operator
  last_changed_at       timestamptz
  last_received_at      timestamptz
  status_changed_at     timestamptz             -- retention sweep key
  management_address    varchar(450)  null
  host                  varchar(450)  null
  host_id               varchar(450)  null
  pending_operation_id  uuid          null
  pending_operation     jsonb / nvarchar(max) null
  history_truncated     bool

saga_timeout_versions
  timeout_id, version   PK, FK -> saga_timeouts on delete cascade
  payload_content_type  varchar(450)
  payload               bytea / varbinary(max)

saga_timeout_changes
  id                    bigint identity PK
  timeout_id            varchar(450)  FK -> saga_timeouts on delete cascade
  dedup_key             varchar(450)
  kind, version, trigger_id, due_time, changed_at, received_at, source, operation_id,
  requested_change, requested_due_time, reason
  unique (timeout_id, dedup_key)

saga_timeout_failed_triggers
  timeout_id, trigger_id  PK
  failed_message_id     varchar(450)
  failed_at             timestamptz

completed_sagas
  saga_id               uuid PK
  endpoint_name, saga_type, changed_at, received_at
```

`saga_timeout_failed_triggers` has no foreign key, because error ingestion can record a failure before the first report arrives.

Indexes on `saga_timeouts`:

- `(status, due_time)` for the Pending and Overdue tabs
- `(endpoint_name, status, due_time)` and `(saga_type, status, due_time)` for the filters and grouping
- `(saga_id)` for completion and the saga diagram
- `(correlation_value)` for search
- `(status, status_changed_at)` for the retention sweep

`saga_timeout_changes` has an index on `(changed_at)` for the Change History tab.

`Apply` runs in one transaction:

1. Return `Ignored` if `completed_sagas` has the saga.
2. Insert the timeout row if it is absent, with status `Unknown`, version 0, and the saga fields from the change.
3. Insert the history entry and, for `Requested` and `Rescheduled`, the version row, each only if it is absent.
4. Update the current state with a guarded statement: `UPDATE saga_timeouts SET ... WHERE timeout_id = @id AND (version < @version OR (version = @version AND rank < @rank))`.

Every insert uses the provider's insert-if-absent statement: `ON CONFLICT DO NOTHING` on PostgreSQL, and `INSERT ... WHERE NOT EXISTS` with `UPDLOCK, HOLDLOCK` on SQL Server. Two concurrent first reports therefore both succeed, and the guarded update decides which state wins. The guarded update replaces optimistic concurrency, because the version rule is the concurrency rule. The statements follow the `EndpointThroughput` upsert precedent (#5895), where duplicate keys are routine. A rejection stops after step 3, so a timeout known only from a rejection stays `Unknown`.

Step 1 and `ApplySagaCompleted` can race. A report that passes step 1 just before the marker commits would leave a timeout behind. To prevent that, both take an application lock on the saga id for the transaction: `pg_advisory_xact_lock` on PostgreSQL and `sp_getapplock` on SQL Server.

`ApplySagaCompleted` runs in one transaction. It inserts the marker if it is absent and deletes `saga_timeouts` rows for the saga. Versions and history go by cascade. It also deletes failed triggers for the deleted timeout ids.

Search uses `LIKE` on `correlation_value`, `saga_type`, and `timeout_type`. Payload search is not in the first version. See [Open questions](#open-questions).

`HasUnresolvedFailedTrigger` is a join from `saga_timeouts` to `saga_timeout_failed_triggers` on `(timeout_id, trigger_id)`, and on to `failed_messages` filtered to the unresolved status. Error ingestion inserts the failed trigger with the same insert-if-absent statement, through a new method on the ingestion unit of work and a statement in `IFailedMessageIngestionSqlDialect`.

`RetentionSweeper` gets three passes:

- Delete `saga_timeouts` whose status is `Triggered`, `Canceled`, or `Unknown` and whose `status_changed_at < now - ErrorRetentionPeriod`. Child rows go by cascade.
- Delete `completed_sagas` older than the same cutoff.
- Delete `saga_timeout_failed_triggers` with no matching timeout and older than the cutoff.

The cutoff is recomputed on every run, so a changed setting applies to existing rows, as for failed messages.

### Retention period

Finished timeouts use `ErrorRetentionPeriod`. A timeout is error-instance operational data and lives about as long as the failed messages it can relate to. A separate `SagaTimeoutRetentionPeriod` setting is not proposed, but it can be added without a schema change.

## HTTP API

All routes are on the primary instance under `api/`. Paging, sorting, `Total-Count`, `Link`, and `ETag` work as in other list APIs.

| Method and route | Permission | Purpose |
|---|---|---|
| `GET timeouts?tab=pending\|overdue\|failed&endpoint=&saga_type=&saga_id=&due_before=&due_after=&q=` | `error:timeouts:view` | Timeout list |
| `GET timeouts/counts` | `error:timeouts:view` | Counts per tab, for the tab headers and the menu badge |
| `GET timeouts/groups?by=endpoint\|saga_type&tab=...` | `error:timeouts:view` | Group rows with counts and earliest due time |
| `GET timeouts/history?endpoint=&saga_type=&since=` | `error:timeouts:view` | Change History tab |
| `GET timeouts/{timeoutId}` | `error:timeouts:view` | Details, history, and pending operation |
| `GET timeouts/{timeoutId}/payload?version=` | `error:timeouts:view` | Raw payload with its content type |
| `GET sagas/{sagaId}/timeouts` | `error:timeouts:view` | Timeouts of one saga instance, for the saga diagram |
| `POST timeouts/{timeoutId}/reschedule` | `error:timeouts:manage` | Body `{ "due_time": ..., "expected_version": n, "payload": ..., "payload_content_type": ... }`; the payload fields are optional |
| `POST timeouts/{timeoutId}/trigger` | `error:timeouts:manage` | Body `{ "expected_version": n }` |
| `POST timeouts/{timeoutId}/cancel` | `error:timeouts:manage` | Body `{ "expected_version": n }` |
| `DELETE timeouts/{timeoutId}` | `error:timeouts:manage` | Forget the record in ServiceControl only |

The `GET sagas/{sagaId}/timeouts` route lives on the primary. `GET sagas/{sagaId}` is already a scatter-gather route on the primary. The new route reads only local data, so it does not need scatter-gather.

The action routes return:

- `202 Accepted` with `{ "operation_id": ... }` when the request is recorded and sent
- `409 Conflict` when `expected_version` does not match the stored version, or another operation is pending
- `422` when the timeout is not `Pending`, or has no known management address

The check in ServiceControl gives fast feedback. It is not authoritative: the satellite repeats the version check against the saga data.

`DELETE` exists for timeouts that will never be reported again, for example because the endpoint was decommissioned. It removes the record and its history from ServiceControl. It does not touch the saga.

Every action writes an `IMessageActionAuditLog` entry, raises a domain event, and so appears in the event log. The events are `SagaTimeoutRescheduleRequested`, `SagaTimeoutTriggerRequested`, `SagaTimeoutCancelRequested`, `SagaTimeoutForgotten`, and `SagaTimeoutOperatorRequestRejected`. They relate to `/endpoint/{name}`. Integration events are not proposed for the first version.

## Operator actions

1. ServicePulse calls `POST timeouts/{id}/reschedule` with the version it displays.
2. The controller calls `RecordOperatorRequest`. On `Accepted`, it sends a local `RequestSagaTimeoutChange` command with `IMessageSession.SendLocal`, as `CustomCheckController` does for deletes, and returns `202`.
3. The handler for `RequestSagaTimeoutChange` builds the satellite request: the headers listed under [Management satellite](#management-satellite) and, for a payload change, the new payload as the body. It dispatches the request with `IMessageDispatcher` to the stored management address, inside the handler's transport transaction. `EditHandler` dispatches the same way.
4. The satellite applies the change and reports it with the `OperationId`.
5. The report clears the pending operation and records the history entry with source `Operator`. A rejection with the same `OperationId` clears it as well, records the reason, and raises `SagaTimeoutOperatorRequestRejected`.

The trigger action sends a reschedule with the current time as the due time and an empty body.

A pending operation that receives no report within `SagaTimeouts/OperatorRequestTimeout` (default 10 minutes) is shown as "no response from endpoint". It no longer blocks new requests. The record does not prove the request was lost: the endpoint can be down, or the request can sit in the satellite queue.

The satellite queue is shared by all instances of a scaled-out endpoint, because its name derives from the endpoint's shared queue name. Any instance can apply the change, because the saga is in shared storage.

## ServicePulse

The new area follows existing patterns. It is gated on a minimum ServiceControl version with an `isSagaTimeoutsSupported.ts` file and `ConditionalRender`. `apiRoutes.ts` gets `viewTimeouts` and `manageTimeouts` capabilities. `PermissionGate` disables the action buttons for users without `error:timeouts:manage`.

### Timeouts list

The route is `/timeouts`, with child routes `pending`, `overdue`, `failed`, and `history`. A `TimeoutsMenuItem.vue` in `PageHeader.vue` shows the count of overdue timeouts plus failed triggers as a badge, read from `GET timeouts/counts`. There is no "NEW" badge component today. If product wants one, it is a small addition to the menu item styles.

- The tabs use the `FailedMessagesView` pattern, with counts from `GET timeouts/counts`.
- The filter bar reuses `FilterInput` (debounced search), `ListFilterSelector` (endpoint, saga type), and a due-window selector based on `SuperDatePicker` and `timeRange.ts`, which already handle relative ranges.
- The table follows the mockup columns: Due, Timeout, Saga, Correlation value, Last change, Status. Due shows the absolute UTC time and a relative time. `TimeSince` already renders future times ("in 12 minutes"). `AdaptiveTimestamp` is past-only and needs a future-aware variant.
- Group by endpoint or saga type calls `GET timeouts/groups`. Selecting a group sets the filter. Grouping over the current page only would mislead, because it hides most of the data.
- Paging uses `PaginationStrip` and sorting uses `ColumnHeader`.
- A `TimeoutsStore` (Pinia) polls through `useStoreAutoRefresh`. After an action it switches to a 1-second interval until the pending operation clears, as `FailedMessages.vue` does after a retry.

The Change History tab lists `GET timeouts/history` entries:

- kind, timeout, saga, and version
- source: saga or operator
- the endpoint's `ChangedAt`, and ServiceControl's `ReceivedAt` when they differ by more than the grace period

### Timeout details

The route is `/timeouts/{timeoutId}`, with breadcrumbs Timeouts / endpoint / saga type / correlation value.

- The details panel shows the stored fields, the host, and the current trigger id. The version shown is the integer `Version`, not a fragment of the trigger id as in the mockup. It is the value sent as `expected_version`.
- The payload panel renders JSON payloads with the existing message body viewer. Other content types show as text or as a download.
- The history panel is a timeline of history entries. "Show change" on a `Rescheduled` entry shows a due-time change and a payload diff against the previous version.
- The actions panel has Reschedule, Trigger now, and Cancel. Reschedule has a UTC date-time input and an optional payload editor for JSON payloads. Cancel uses `ConfirmDialog`. All three send the displayed version.
- A `409` shows a toast saying the timeout changed and reloads the page.
- While an operation is pending, the panel shows "Change requested, waiting for the endpoint", with the time of the request, and disables the actions.
- The related panel lists the other timeouts of the saga instance and links to the saga diagram. The diagram link needs a message id, which the page takes from the saga history (`GET sagas/{sagaId}`). Without saga audit, the link is not shown.
- Forget is a secondary action, shown only for `Unknown` timeouts and timeouts whose operation got no response.

### Saga diagram additions

`SagaDiagram.vue` adds a "Timeouts (n)" toggle to its toolbar. `SagaDiagramStore` loads `GET sagas/{sagaId}/timeouts` next to `GET sagas/{sagaId}` when the feature is supported. A failure of the new call does not break the diagram. A completed saga has no timeouts left in ServiceControl, so the panel is empty for it.

- The "Timeouts of this saga" panel lists each timeout with status, requested, rescheduled, and triggered times, and links to Open in Timeouts and Show history.
- `SagaDiagramParser` treats an outgoing message with a delivery delay as a timeout request. That message is matched to a managed timeout by saga id, timeout type, and due time (`deliver_at` within one second of `DueTime`). The match adds a "Managed" badge and a "View timeout" link.
- The match is a heuristic, because saga audit does not record the `NServiceBus.SagaTimeoutId` header. Two timeouts of the same type with the same due time cannot be told apart. Adding the header to the saga audit `ResultingMessage` would make the match exact; that is a change to NServiceBus.SagaAudit and the audit instance.
- Operator changes are not saga invocations, so saga audit never records them. The diagram inserts them from the timeout history as separate timeline entries, ordered by `ChangedAt` ("Timeout Rescheduled = 1m, by an operator in ServicePulse").

### Testing

- Component tests use the `SagaDiagram.spec.ts` style and a timeout history fixture.
- Application tests use MSW handlers through `mockEndpoint` and a `hasSagaTimeouts` precondition. The precondition sets the minimum ServiceControl version.
- A `timeouts` mock scenario in `test/mocks/scenarios` supports manual work with `npm run dev:mocks`.

## Rollout and compatibility

Reporting is opt-in because a ServiceControl version without this feature does not know the six message types. Reports sent to it would fail on the primary's input queue and end up in the error queue as failed messages of ServiceControl itself. The documentation of `ReportSagaTimeoutsToServiceControl()` states the minimum ServiceControl version.

Enabling reporting adds the `{endpoint queue}.SagaTimeouts` queue. Installers create it like the endpoint's other queues. Deployments that create queues with scripts must add it.

Endpoints with persisters that do not implement `ISupportTimeoutManagement` keep the legacy timeout behavior and report nothing. Their timeouts do not appear in ServicePulse. The satellite still runs on such an endpoint and rejects every request with `TimeoutNotFound`.

Every timeout change is one message to the primary instance. A saga that requests a timeout per message doubles the primary's plugin-message load for that endpoint. The load test tooling in this repository should get a saga timeout scenario before release. It should measure handler throughput on both persisters with concurrent reports for the same saga.

The feature is not available on the audit or monitoring instances, and it is not available when the primary runs only error ingestion.

## Open questions

| Question | Owner |
|---|---|
| The minimum ServiceControl version to state in the `ReportSagaTimeoutsToServiceControl()` documentation | NServiceBus, ServiceControl |
| Record `NServiceBus.SagaTimeoutId` in saga audit outgoing messages, so the saga diagram match is exact | NServiceBus.SagaAudit, ServiceControl audit |
| Payload search: a full-text column on the version table, or the RavenDB `Query` field over JSON payloads, and its cost | ServiceControl |
| Whether finished timeouts need their own retention setting | ServiceControl |
| Whether the losses possible without the outbox, a lost `Triggered` report and a lost dispatch after a satellite commit, need endpoint-side detection, or whether showing them as overdue is enough | NServiceBus, ServiceControl |
