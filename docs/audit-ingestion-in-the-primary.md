# Audit ingestion in the primary instance

## Overview

Storage that advertises `SupportsAuditIngestion` in its `persistence.manifest` can hold audit data
alongside the primary's own data, which lets the primary ServiceControl process ingest the audit
queue itself instead of relying on a separate ServiceControl.Audit instance.

The standalone RavenDB audit instance is unaffected. RavenDB does not advertise audit support, does
not gain combined hosting, and keeps its own executable, settings, API and installers.

The SQL Server and PostgreSQL persisters advertise audit support, so a primary on either ingests
the audit queue by default. On RavenDB the audit component registers nothing and behavior is
unchanged.

## Topologies

Every process is the same executable and the same persister. What differs is the database each
process is given and the mode it is started in. Each database has exactly one owner, and only owners
run setup, retention and the API; every other process on that database is a worker.

### Shared database

The default. Audit data lives in the primary's database.

| Process | Started as | What it runs |
| --- | --- | --- |
| Primary | `ServiceControl.exe` | The audit receiver (unless `ServiceControl/IngestAuditMessages=false`), the audit capabilities, the local audit queries, audit retention, and everything a normal primary runs |
| Audit worker | `ServiceControl.exe --audit-ingestion-only` | The audit receiver, the endpoint monitor it depends on, this node's custom checks, and the health endpoints. No NServiceBus endpoint, no API, no retention, no licensing |
| Error worker | `ServiceControl.exe --error-ingestion-only` | As before |

Turning the primary's receiver off stops only the receiver. Local audit queries, failed audit
tooling and `/api/connection` stay active, because workers may still be ingesting.

### Dedicated audit database

For a deployment whose database copes with the error instance but not with audit volume. Audit moves
to its own database, on the same server or another, and nothing else about the deployment changes.

| Process | Started as | What it runs |
| --- | --- | --- |
| Primary | `ServiceControl.exe` with `ServiceControl/AuditDataLocation=Remote` and the audit host listed under `ServiceControl/RemoteInstances` | Everything a normal primary runs, minus the audit receiver, the local audit queries and audit retention. Audit data reaches it through the scatter gather, exactly as from a RavenDB audit instance |
| Audit host | `ServiceControl.exe --audit-instance`, with the audit database's connection string and `ServiceControl/ServiceControlQueueAddress` | The audit receiver, the primary API, audit retention, saga audit, failed audit tooling and `/api/connection`. No error side. `--setup --audit-instance` provisions the audit database, the audit queue and body storage |
| Audit worker | `ServiceControl.exe --audit-ingestion-only`, with the audit database's connection string and `ServiceControl/ServiceControlQueueAddress` | As in the shared topology. Which database it feeds is the connection string it is given |
| Error worker | `ServiceControl.exe --error-ingestion-only` | As before. Error ingestion only ever shares the primary's database |

The audit host and its workers report their custom checks and the endpoints they detect to the
primary's input queue, the way the standalone RavenDB audit instance does, because the primary is the
only process ServicePulse asks. `ServiceControl/ServiceControlQueueAddress` is what switches a
process into that reporting mode; leave it unset in the shared topology.

The audit host serves the whole primary API, of which the primary's scatter gather calls the
message, saga, audit count, body, configuration and connection routes. The rest answers with the
host's own empty error data. It runs the same authorization configuration as the primary, because the
primary forwards the caller's credentials to it.

`--audit-instance` cannot be combined with either ingestion only flag, and `--audit-ingestion-only`
cannot be combined with `--error-ingestion-only`: run one process per mode.

### The RavenDB topology

Unchanged. `ServiceControl.Audit.exe` with its own database, listed as a remote.

All three modes keep audit data in the primary's own database. A customer whose audit load would
swamp that database can instead move audit to a dedicated one, served by the same executable in
`--audit-instance` mode. That topology was decided on 14 September 2026 and arrives with the EF
audit persistence work; until then this document describes the shared-database modes only.

## Settings

The primary reads the audit settings under the same key names the audit instance uses, so an audit
capable primary is configured exactly the way an audit instance is configured today.

| Setting | Default | Notes |
| --- | --- | --- |
| `ServiceControl/IngestAuditMessages` | `true` | Applies to the normal primary only. Always on under `--audit-ingestion-only`, and has no effect where the persister does not support audit |
| `ServiceBus/AuditQueue` | `audit` | The queue this instance drains |
| `ServiceBus/AuditLogQueue` | the subscoped audit queue name | Only used when forwarding is on |
| `ServiceControl/ForwardAuditMessages` | `false` | |
| `ServiceControl/AuditRetentionPeriod` | 7 days | Already existed. Validated between 1 hour and 365 days. The SQL Server and PostgreSQL persisters default it to 7 days when unset, matching the management utility and the container image rather than the audit instance's 30 |
| `ServiceControl/AuditDataLocation` | `Local` | `Remote` tells a primary its audit data is on a dedicated audit host. Ignored where the persister does not support audit |
| `ServiceControl/ServiceControlQueueAddress` | unset | The primary's input queue. Set on the audit host and on workers that feed a dedicated audit database, which then report custom checks and detected endpoints there. The same key the standalone audit instance reads |
| `ServiceControl/MaximumAuditIngestionConcurrencyLevel` | `32` | Independent of the primary endpoint's concurrency, which is what `MaximumConcurrencyLevel` sets |
| `ServiceControl/TimeToRestartAuditIngestionAfterFailure` | 60 seconds | Mirrors the error equivalent |
| `ServiceControl/MessageBody/FileSystem/PathIsShared` | `false` | Required by both ingestion only modes when body storage is the file system |

### Setting collisions

`ServiceControl` and `ServiceControl.Audit` settings can both be set by bare environment variable
name, and `ServiceBus/AuditQueue` is literally the same key for both processes. A combined primary
and a standalone audit instance sharing one environment file therefore collide on
`INGESTAUDITMESSAGES`, `AUDITRETENTIONPERIOD`, `FORWARDAUDITMESSAGES` and `SERVICEBUS_AUDITQUEUE`.

That combination is unsupported. A primary that ingests audit into its own database and also lists
remote instances refuses to start, because it is one of two mistakes: remotes left over from before
audit moved into the database, or a primary that was meant to have `AuditDataLocation=Remote`.

## Queue ownership

The setup path of a database's owner creates the audit queue, and the audit forwarding queue when
forwarding is enabled. Ingestion only workers run no installers: they never create queues, never
apply database migrations and never provision body storage. Run setup from the owner before starting
any worker; a worker started against a database the owner has not migrated refuses to start and says
so, rather than failing on its first write.

The primary and a dedicated audit database run the same migrations, so the owners are upgraded
independently and in either order. The audit database carries the error tables, empty, and a primary
whose audit is remote carries the audit tables, empty.

Transport operations remain in the audit ingestion path for two reasons only:

- **Forwarding**, when `ForwardAuditMessages` is on.
- **Retry acknowledgements**. `ServiceControl.Retry.AcknowledgementQueue` is stamped by whichever
  instance issued the retry, so the acknowledgement cannot be short-circuited into the local
  database. In a combined host it is dispatched to the local error queue and comes straight back in
  through local error ingestion, which is exactly what happens today.

Endpoints detected from audit headers are written straight to the `KnownEndpoints` table of the
database being ingested into, through the ingestion unit of work. On a dedicated audit database they
are additionally reported to the primary's input queue, since that table is not the primary's.

## Body storage

Audit and failed message bodies share one store, and each owns a prefixed keyspace, so an edited
message's failed body and its audited body do not collide. Audit bodies are keyed
`audit/{ingestion hour}/{unique message id}`, which is what lets retention drop an hour's bodies in
one operation. `IBodyStorage.TryFetch` resolves in a fixed order: failed message by
`UniqueMessageId`, then failed message by `MessageId`, then audit message by `UniqueMessageId`.

Hosts on one database share one body store: the audit host and its workers share the audit store,
the primary and its workers share the primary's.

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

## Retention

Audit rows are stored by the hour they were ingested in and expire an hour at a time, once the whole
hour is behind `AuditRetentionPeriod`. On PostgreSQL the audit tables are range partitioned by that
hour, so dropping an expired hour is a metadata operation, and the retention sweep keeps partitions
provisioned 48 hours ahead; a custom check, `Audit partition provisioning`, fails when the newest
provisioned partition ends less than 12 hours ahead, which means the sweeper has stopped. On SQL
Server, which does not partition, an expired hour is deleted in batches.

The retention sweep, for audit and error data alike, runs under a session scoped database lock, so at
most one host sweeps a database at any moment. A host that cannot take the lock skips the pass.

## Querying

Local audit data is served through the existing primary routes under their existing policies:
`/api/messages` and its variants on `error:messages:view`, `/api/sagas/{id}` on
`error:sagas:view`, and `endpoints/{endpoint}/audit-count` on `error:messages:view`. A primary
configured with an audit remote already serves that remote's audit data under those policies today,
so nothing about the `my/routes` manifest or ServicePulse navigation changes.

Additional audit remotes keep working. The scatter gather runs the local query first and merges the
remotes after, so a primary can hold audit data locally, query remotes, or both.

Where a database holds both failed and audited messages, each message view is one SQL statement
over both tables, with each branch carrying its own sort and limit so the database merges two
index-ordered scans and stops at the page boundary. Three rules apply:

1. **Precedence.** A message that both failed and was audited shows as failed, whatever the failed
   row's status. Archived failures show as archived, as they always have.
2. **Paging.** Pages are exact across both tables.
3. **Counting.** A message that both failed and was audited is counted once. The total is capped,
   because an exact count is linear in the audit table; `Total-Count` and the paging links report
   the cap when it is reached.

A primary whose audit data is remote queries only its failed messages locally.

## Telemetry

Both ingestions publish on the primary instance's meter, `Particular.ServiceControl`; a standalone audit instance publishes on `Particular.ServiceControl.Audit`. The meter names the process, not the subject. What a measurement is about is carried by the instrument prefix instead: `sc.error.ingestion.*` for error ingestion and `sc.audit.ingestion.*` for audit ingestion, unchanged whichever instance produced them.

Scaled out workers drain the same queue and so share an instance name, which makes `service.name` identical across the pool. Every process therefore also reports `host.name` and `process.pid`, so a pool can be told apart on a dashboard with nothing configured. `service.instance.id` is generated where none is given: unique per process, but new on every restart, so a dashboard grouped on it alone loses its series each time a worker is recycled.

Naming a worker explicitly is the standard OpenTelemetry environment variables, honored for both metrics and exported logs:

```
OTEL_SERVICE_NAME=sc-audit-ingestion
OTEL_RESOURCE_ATTRIBUTES=service.instance.id=worker-1
```

Anything set there wins, including `host.name` and `process.pid`, so a containerized deployment can report the identity it wants rather than the one the process detects.

## Packaging

The audit runtime ships inside the existing primary artifact. There is no new assembly and no new
deployment unit. The copied ingestion metrics share the primary's OpenTelemetry exporter, enabled by
the standard `OTEL_EXPORTER_OTLP_ENDPOINT` variable.
