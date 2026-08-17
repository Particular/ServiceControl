# Event log design

## What it is

The event log is the primary instance's activity feed: the chronological "what has this instance noticed" list that ServicePulse shows. Message failures, retries, redirects, heartbeats, custom checks and integration failures all surface here.

It is a **projection of domain events, not a log file**. Nothing writes to it directly. Components raise domain events for their own reasons, and the event log turns a chosen subset of those into feed items. An event only appears if someone has declared how it should read, which makes the feed an editorial selection rather than a dump.

Only the primary instance has an event log. Audit and monitoring instances have none.

Four contracts define the whole part: `EventLogItem` (what is written), `EventLogItemView` (what is read), `EventLogMappingDefinition<TEvent>` (how a component declares an event belongs in the feed), and `IEventLogDataStore` (the storage seam). Everything else is machinery behind them.

## What gets recorded

`EventLogItem` is the write contract, and it carries **no identity**. The identity is minted in each persistence seam.

`EventLogItemView` is the same shape plus `Id`, which is assigned by storage rather than by the application.

`RelatedTo` entries are built by helpers with fixed prefixes: `/message/{id}`, `/endpoint/{name}`, `/machine/{name}`, `/host/{guid}`, `/customcheck/{id}`, `/recoverability/groups/{id}`. The helpers only prefix a string, so passing the wrong property yields a link that resolves to nothing.

## Declaring an event

A component makes one of its events visible with two things, and nothing in the event log changes:

1. A class deriving **directly** from `EventLogMappingDefinition<TEvent>`. Derive through an intermediate non-generic base and the definition is silently skipped at registration.
2. A matching `services.AddEventLogMapping<TDefinition>()` in that component's configuration. Two definitions for one event type is an error.

A definition is a **declarative builder, not a handler**: its constructor calls `Description(…)`, and optionally `RaisedAt(…)`, `Severity(…)` / `TreatAsError()` and the `RelatesTo*` helpers, to specify how one row reads. Definitions live with the component that raises the event: **this part owns the machinery, the components own the content**. An event with no declaration is ignored, deliberately and silently.

## Reading the feed

`GET /api/eventlogitems` is the entire HTTP surface, gated on `Permissions.ErrorEventLogView`. It takes `page` and `pageSize`, returns one page of `EventLogItemView` newest first, and sets `Total-Count`, `ETag` and `Link`. There is no write, no delete, no per-item lookup, and no filtering or search: `Category`, `Severity` and `EventType` are returned but cannot be queried on.

Clients discover new items by **polling**; nothing is pushed, and recording an item has no outward effect at all. Because polling is the only path, a caller that echoes its `ETag` back as `If-None-Match` gets `304` with no body, and the request costs a header exchange instead of a page of JSON.

Timestamps are when the thing happened, so an item can land in the middle of the feed rather than at its head.

## The storage seam

`IEventLogDataStore` has two methods, and its XML docs are the binding contract:

- `Add(EventLogItem)` persists one item. **Identity is the store's to assign** and surfaces on `EventLogItemView.Id`. That makes `Id` opaque: a stable key within one store, not something to parse.
- `GetEventLogItems(PagingInfo, knownVersion)` returns a `QueryResult` carrying the page, the total count independent of paging, and an `ETag`. Two obligations: the `ETag` is surfaced **verbatim**, so whatever the client echoes back arrives here unchanged and can be compared, and it **must change when retention removes items**, not only when one is added, since nothing else tells a polling client its cached page has gone stale.

## Retention

Items age out on their own after `EventsRetentionPeriod`, 14 days by default. Nothing a user does removes one and there is no API to try. Enforcement is left to each storage backend and is invisible through the seam, so a change to the setting applies retrospectively on some backends and to new items only on others.

## Failure behaviour

Recording is **in-band, not best-effort**. Domain event dispatch rethrows, so if storage is unreachable the failure propagates to whatever raised the event and that operation fails. There is no retry, no queue and no buffer in front of the write.

## Known limits

- **The feed is per error instance.** A federated deployment gets no aggregation and nothing in the response says which instance answered.
