# Audit ingestion in the primary instance

## Overview

Storage that advertises `SupportsAuditIngestion` in its `persistence.manifest` can hold audit data
alongside the primary's own data, which lets the primary ServiceControl process ingest the audit
queue itself instead of relying on a separate ServiceControl.Audit instance.

The standalone RavenDB audit instance is unaffected. RavenDB does not advertise audit support, does
not gain combined hosting, and keeps its own executable, settings, API and installers.

No shipped persister advertises audit support yet, so on every existing deployment the audit
component registers nothing and behavior is unchanged.

## Deployment modes

| Mode | How | What it runs |
| --- | --- | --- |
| Normal primary, audit ingestion on | Default where the persister advertises audit support | The audit receiver, the audit capabilities, the primary API and everything a normal primary runs |
| Normal primary, audit ingestion off | `ServiceControl/IngestAuditMessages=false` | Everything above except the audit receiver. Local audit queries, failed audit tooling and `/api/connection` stay active, because other processes may still be ingesting |
| Audit ingestion only | `ServiceControl.exe --audit-ingestion-only` | The audit receiver, the endpoint monitor it depends on, this node's custom checks, and the health endpoints. No NServiceBus endpoint, no API, no retention, no licensing |

`--audit-ingestion-only` and `--error-ingestion-only` cannot be combined. Each queue gets its own
worker pool so the two can be scaled independently, so run one process per mode.

## Settings

The primary reads the audit settings under the same key names the audit instance uses, so an audit
capable primary is configured exactly the way an audit instance is configured today.

| Setting | Default | Notes |
| --- | --- | --- |
| `ServiceControl/IngestAuditMessages` | `true` | Applies to the normal primary only. Always on under `--audit-ingestion-only`, and has no effect where the persister does not support audit |
| `ServiceBus/AuditQueue` | `audit` | The queue this instance drains |
| `ServiceBus/AuditLogQueue` | the subscoped audit queue name | Only used when forwarding is on |
| `ServiceControl/ForwardAuditMessages` | `false` | |
| `ServiceControl/AuditRetentionPeriod` | unset | Already existed. Validated between 1 hour and 365 days |
| `ServiceControl/MaximumAuditIngestionConcurrencyLevel` | `32` | Independent of the primary endpoint's concurrency, which is what `MaximumConcurrencyLevel` sets |
| `ServiceControl/TimeToRestartAuditIngestionAfterFailure` | 60 seconds | Mirrors the error equivalent |
| `ServiceControl/OtlpEndpointUrl` | unset | Enables the OpenTelemetry metrics exporter |
| `ServiceControl/MessageBody/FileSystem/PathIsShared` | `false` | Required by both ingestion only modes when body storage is the file system |

### Setting collisions

`ServiceControl` and `ServiceControl.Audit` settings can both be set by bare environment variable
name, and `ServiceBus/AuditQueue` is literally the same key for both processes. A combined primary
and a standalone audit instance sharing one environment file therefore collide on
`INGESTAUDITMESSAGES`, `AUDITRETENTIONPERIOD`, `FORWARDAUDITMESSAGES` and `SERVICEBUS_AUDITQUEUE`.

That combination is unsupported. The primary logs a warning at startup when it has audit ingestion
enabled and audit remotes configured at the same time, because that is the shape most likely to hit
the collision.

## Queue ownership

The setup path creates the audit queue, and the audit forwarding queue when forwarding is enabled.
Ingestion only workers run no installers: they never create queues, never apply database migrations
and never provision body storage. Run setup from a normal instance before starting any worker.

Transport operations remain in the audit ingestion path for two reasons only:

- **Forwarding**, when `ForwardAuditMessages` is on.
- **Retry acknowledgements**. `ServiceControl.Retry.AcknowledgementQueue` is stamped by whichever
  instance issued the retry, so the acknowledgement cannot be short-circuited into the local
  database. In a combined host it is dispatched to the local error queue and comes straight back in
  through local error ingestion, which is exactly what happens today.

Endpoints detected from audit headers are written straight to the shared `KnownEndpoints` table
through the ingestion unit of work, rather than sent to the primary's input queue.

## Body storage

Audit and failed message bodies share one store, and each owns a prefixed keyspace, so an edited
message's failed body and its audited body do not collide. `IBodyStorage.TryFetch` resolves in a
fixed order: failed message by `UniqueMessageId`, then failed message by `MessageId`, then audit
message by `UniqueMessageId`.

Every ingesting process must write bodies somewhere every host can read. Blob and S3 storage
qualify. File system storage qualifies only if the path is a shared mount, which nothing in the
settings can detect, so both ingestion only modes refuse to start unless
`ServiceControl/MessageBody/FileSystem/PathIsShared` asserts it.

## Health endpoints

Both ingestion only hosts map the same two routes, anonymously, returning JSON:

- `/health` is liveness. It answers "is this process still serving" and is what a container health
  check should restart on.
- `/health/ready` additionally reports whether the ingestion this host exists to do is happening.
  An audit ingestion only host answers for `audit-ingestion` and not for `error-ingestion`.

## Querying

Local audit data is served through the existing primary routes under their existing policies:
`/api/messages` and its variants on `error:messages:view`, `/api/sagas/{id}` on
`error:sagas:view`, and `endpoints/{endpoint}/audit-count` on `error:messages:view`. A primary
configured with an audit remote already serves that remote's audit data under those policies today,
so nothing about the `my/routes` manifest or ServicePulse navigation changes.

Additional audit remotes keep working. The scatter gather runs the local query first and merges the
remotes after, so a primary can hold audit data locally, query remotes, or both.

Where one local result set contains both failed and audited messages, three rules apply, and
`LocalMessagesView.Merge` implements them for any persister:

1. **Precedence.** A message that both failed and was audited shows as failed.
2. **Paging.** The local result is already at most one page, after deduplication.
3. **Counting.** A message that both failed and was audited is counted once.

## Packaging

The audit runtime ships inside the existing primary artifact. There is no new assembly and no new
deployment unit. The primary gains three OpenTelemetry package references, which the copied
ingestion metrics use, exported only when `OtlpEndpointUrl` is set.
