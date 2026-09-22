# Audit ingestion in the primary instance

## Overview

Storage that advertises `SupportsAuditIngestion` in its `persistence.manifest` can hold audit data alongside the primary's own data, which lets the primary ServiceControl process ingest the audit queue itself instead of relying on a separate ServiceControl.Audit instance.

The standalone RavenDB audit instance is unaffected. RavenDB does not advertise audit support, does not gain combined hosting, and keeps its own executable, settings, API and installers.

The SQL Server and PostgreSQL persisters advertise audit support, so a primary on either ingests the audit queue by default. On RavenDB the audit component registers nothing and behavior is unchanged.

## Deployment options

Every process is the same executable and the same persister. What differs is the database each process is given and the mode it is started in. Each database has exactly one owner, and only owners run setup, retention and the API; every other process on that database is a worker.

### Shared database

The default. Audit data lives in the primary's database.

| Process | Started as | What it runs |
| --- | --- | --- |
| Primary | `ServiceControl.exe` | The audit receiver (unless `ServiceControl/IngestAuditMessages=false`), the audit capabilities, the local audit queries, audit retention, and everything a normal primary runs |
| Audit worker | `ServiceControl.exe --audit-ingestion-only` | The audit receiver, the endpoint monitor it depends on, this node's custom checks, and the health endpoints. No NServiceBus endpoint, no API, no retention, no licensing |
| Error worker | `ServiceControl.exe --error-ingestion-only` | As before |

Turning the primary's receiver off stops only the receiver. Local audit queries, failed audit tooling and `/api/connection` stay active, because workers may still be ingesting.

### Dedicated audit database

For a deployment whose database copes with the error instance but not with audit volume. Audit moves to its own database, on the same server or another, and nothing else about the deployment changes.

| Process | Started as | What it runs |
| --- | --- | --- |
| Primary | `ServiceControl.exe` with `ServiceControl/AuditDataLocation=Remote` and the audit host listed under `ServiceControl/RemoteInstances` | Everything a normal primary runs, minus the audit receiver, the local audit queries and audit retention. Audit data reaches it through the scatter gather, exactly as from a RavenDB audit instance |
| Audit host | `ServiceControl.exe --audit-instance`, with the audit database's connection string and `ServiceControl/ServiceControlQueueAddress` | The audit receiver, the primary API, audit retention, saga audit, failed audit tooling and `/api/connection`. No error side. `--setup --audit-instance` provisions the audit database, the audit queue and body storage |
| Audit worker | `ServiceControl.exe --audit-ingestion-only`, with the audit database's connection string and `ServiceControl/ServiceControlQueueAddress` | As in the shared database option. Which database it feeds is the connection string it is given |
| Error worker | `ServiceControl.exe --error-ingestion-only` | As before. Error ingestion only ever shares the primary's database |

The audit host and its workers report their custom checks and the endpoints they detect to the primary's input queue, the way the standalone RavenDB audit instance does, because the primary is the only process ServicePulse asks. `ServiceControl/ServiceControlQueueAddress` is what switches a process into that reporting mode; leave it unset in the shared database option.

The audit host serves the whole primary API, of which the primary's scatter gather calls the message, saga, audit count, body, configuration and connection routes. The rest answers with the host's own empty error data. It runs the same authorization configuration as the primary, because the primary forwards the caller's credentials to it.

`--audit-instance` cannot be combined with either ingestion only flag, and `--audit-ingestion-only` cannot be combined with `--error-ingestion-only`: run one process per mode.

A single process is never given two databases. One primary holding audit in database A and error in database B would need two sets of migrations, two retention locks, two body stores and two ownership stories inside one host, and it would still not give audit its own memory, thread pool or failure boundary, which is the reason a deployment reaches for a second database in the first place. A deployment that wants audit data somewhere else runs the audit host; a deployment that only wants audit data on different storage does that below the connection string, with a filegroup, a tablespace or a different storage tier.

### The RavenDB deployment

Unchanged. `ServiceControl.Audit.exe` with its own database, listed as a remote.

RavenDB gains no combined hosting, and that is on the merits rather than for want of scope. Voron admits one writer per database, so putting audit and error ingestion in one RavenDB database would put both streams behind a single write transaction, which is the contention the separate audit instance exists to avoid. SQL Server and PostgreSQL admit many concurrent writers, so the two streams contend only at the row level.

## Settings

The primary reads the audit settings under the same key names the audit instance uses, so an audit capable primary is configured exactly the way an audit instance is configured today.

| Setting | Default | Notes |
| --- | --- | --- |
| `ServiceControl/IngestAuditMessages` | by precedence, see below | Applies to the normal primary only. Always on under `--audit-ingestion-only`, and has no effect where the persister does not support audit |
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

`ServiceControl` and `ServiceControl.Audit` settings can both be set by bare environment variable name, and `ServiceBus/AuditQueue` is literally the same key for both processes. A combined primary and a standalone audit instance sharing one environment file therefore collide on `INGESTAUDITMESSAGES`, `AUDITRETENTIONPERIOD`, `FORWARDAUDITMESSAGES` and `SERVICEBUS_AUDITQUEUE`.

That combination is unsupported. Point the two processes at separate configuration rather than sharing one environment file.

### Whether the primary ingests audit

`ServiceControl/IngestAuditMessages` resolves by precedence rather than to a fixed default, so that an upgrade never changes which process drains the audit queue:

| Configuration | Audit ingestion |
| --- | --- |
| Set explicitly | Whatever it says |
| Unset, audit remotes configured | Off |
| Unset, no audit remotes, persister supports audit | On |
| Persister does not support audit | Nothing registered |

A fresh installation configures no remotes, so it ingests audit. An installation upgraded from a primary plus a standalone audit instance already lists that instance as a remote, so it does not, and nothing changes for it until an operator sets the value.

The remotes list is what separates the two cases, because it is already present and already means "audit data lives elsewhere". Nothing has to be written at install time, which matters because container deployments have no installer to write it.

A primary holds audit data locally, queries remote audit instances, or does both. The scatter gather merges local results with remote ones, so a primary with its own audit data and a remote has two audit sources and message search spans them. Both together is what the migration path below relies on, and the default above is about not changing which process drains a queue during an upgrade, not about the combination being unusual.

The audit sources a primary resolved are logged at startup and reported by the `Audit data location` custom check.

### Retiring an audit instance

There is no audit data migration. An audit instance is retired by letting its data expire:

1. Set `ServiceControl/IngestAuditMessages=false` on the existing audit instance. It stops draining the audit queue and becomes read only.
2. Set `ServiceControl/IngestAuditMessages=true` on the primary. It starts draining the audit queue into its own database.
3. Leave the audit instance running and still listed as a remote.

New audit data lands in the primary's database. Older audit data is still served from the retiring instance through the scatter gather, which runs the local query first and merges the remotes after, so message search spans both for as long as both exist. Once the old data has aged past its retention period, stop the instance and remove the remote.

The same sequence moves audit the other way, from the primary's database to a dedicated audit database, so changing deployment option is always a configuration change and a waiting period rather than a data migration.

## Queue ownership

The setup path of a database's owner creates the audit queue, and the audit forwarding queue when forwarding is enabled. Ingestion only workers run no installers: they never create queues, never apply database migrations and never provision body storage.

Both databases carry the same schema. The audit database carries the error tables, empty, and a primary whose audit is remote carries the audit tables, empty.

Transport operations remain in the audit ingestion path for two reasons only:

- **Forwarding**, when `ForwardAuditMessages` is on.
- **Retry acknowledgements**. `ServiceControl.Retry.AcknowledgementQueue` is stamped by whichever instance issued the retry, so the acknowledgement cannot be short-circuited into the local database. In a combined host it is dispatched to the local error queue and comes straight back in through local error ingestion, which is exactly what happens today.

Endpoints detected from audit headers are written straight to the `KnownEndpoints` table of the database being ingested into, through the ingestion unit of work. On a dedicated audit database they are additionally reported to the primary's input queue, since that table is not the primary's.

## Schema upgrades

Schema changes are applied by `--setup`, never by a process as it starts. A database is migrated by its owner and by nothing else: the primary for the shared database, the `--audit-instance` host for a dedicated audit database.

### Every host checks the schema before it serves

On startup each host compares the migrations its assembly carries against the migrations the database has applied, and refuses to start on either mismatch:

| Mismatch | Meaning | Message |
| --- | --- | --- |
| The database is missing migrations this host carries | The host is newer than the database. Setup has not been run since this binary was deployed. | Run setup on the instance that owns this database |
| The database has applied migrations this host does not carry | The host is older than the database. It was left running, or started, across an upgrade. | Upgrade this host to match the database |

The second direction matters most. Without it an old worker sees nothing pending, starts normally and keeps writing against a schema it was not built for, failing later at an arbitrary point when it touches a column that changed. A host that detects the database has moved ahead of it while running stops itself rather than continuing.

The probe runs in every host, not only in ingestion only workers. An owner that has been upgraded but not set up fails at startup with an actionable message instead of on its first write.

### Upgrading a database and everything on it

Processes sharing a database are upgraded together, as a unit:

1. Stop the workers on that database.
2. Stop the owner.
3. Deploy the new binaries.
4. Run `--setup` from the owner, which applies the migrations.
5. Start the owner, then the workers.

Ingestion pauses between steps 1 and 5. Nothing is lost: messages accumulate in the audit and error queues and are drained once ingestion resumes, so the cost is ingestion lag rather than data loss. Size the pause against the queue's retention and the broker's capacity, not against a data loss risk.

A rolling upgrade, where workers are replaced one at a time without a pause, is not supported. It would require every migration to be readable by the previous version, which is not a constraint the schema is designed under.

### Two owners of two different databases are independent

The primary and a dedicated audit host own separate databases and migrate them separately, so the two can be upgraded in either order and neither waits on the other. Each one is upgraded with its own workers, as the unit described above.

The exception is the scatter gather between them. A primary and an audit host on different versions communicate over the HTTP API, so they are bound by the usual compatibility rules for that API, not by the database schema.

## Body storage

Audit and failed message bodies share one store, and each owns a prefixed keyspace, so an edited message's failed body and its audited body do not collide. Audit bodies are keyed `audit/{ingestion hour}/{unique message id}`, which is what lets retention drop an hour's bodies in one operation. `IBodyStorage.TryFetch` resolves in a fixed order: failed message by `UniqueMessageId`, then failed message by `MessageId`, then audit message by `UniqueMessageId`.

Hosts on one database share one body store: the audit host and its workers share the audit store, the primary and its workers share the primary's.

Every ingesting process must write bodies somewhere every host can read. Blob and S3 storage qualify. File system storage qualifies only if the path is a shared mount, which nothing in the settings can detect, so both ingestion only modes refuse to start unless `ServiceControl/MessageBody/FileSystem/PathIsShared` asserts it.

## Health endpoints

Both ingestion only hosts map the same two routes, anonymously, returning JSON:

- `/health` is liveness. It answers "is this process still serving" and is what a container health check should restart on.
- `/health/ready` additionally reports whether the ingestion this host exists to do is happening. An audit ingestion only host answers for `audit-ingestion` and not for `error-ingestion`.

## How a worker identifies itself

A worker reports its own custom checks, and a check row is keyed on the reporting endpoint name, a host id and the check id. A host that does not run the primary endpoint derives its host id from the machine name and the instance name, so two workers on different machines are distinct without any configuration. Every container deployment gets this for free, since each container has its own host name.

**Two workers on the same machine sharing an instance name collide**, and the later report overwrites the earlier one. Give co-located workers distinct instance names.

Telemetry identity is separate and already distinguishes such a pool: scaled out workers share a `service.name` because they drain the same queue, so every process also reports `host.name` and `process.pid`, and `OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES` override both.

## Retention

Audit rows are stored by the hour they were ingested in and expire an hour at a time, once the whole hour is behind `AuditRetentionPeriod`. On PostgreSQL the audit tables are range partitioned by that hour, so dropping an expired hour is a metadata operation, and the retention sweep keeps partitions provisioned 48 hours ahead; a custom check, `Audit partition provisioning`, fails when the newest provisioned partition ends less than 12 hours ahead, which means the sweeper has stopped. On SQL Server, which does not partition, an expired hour is deleted in batches.

The retention sweep, for audit and error data alike, runs under a session scoped database lock, so at most one host sweeps a database at any moment. A host that cannot take the lock skips the pass.

## Querying

Local audit data is served through the existing primary routes under their existing policies: `/api/messages` and its variants on `error:messages:view`, `/api/sagas/{id}` on `error:sagas:view`, and `endpoints/{endpoint}/audit-count` on `error:messages:view`. A primary configured with an audit remote already serves that remote's audit data under those policies today, so nothing about the `my/routes` manifest or ServicePulse navigation changes.

Additional audit remotes keep working. The scatter gather runs the local query first and merges the remotes after, so a primary can hold audit data locally, query remotes, or both.

Where a database holds both failed and audited messages, each message view is one SQL statement over both tables, with each branch carrying its own sort and limit so the database merges two index-ordered scans and stops at the page boundary. Three rules apply:

1. **Precedence.** A message that both failed and was audited shows as failed, whatever the failed row's status. Archived failures show as archived, as they always have.
2. **Paging.** Pages are exact across both tables.
3. **Counting.** A message that both failed and was audited is counted once. The total is capped, because an exact count is linear in the audit table; `Total-Count` and the paging links report the cap when it is reached.

Combining failed and audited messages at read time is not new. A primary with an audit remote already does it today, in memory, after an HTTP round trip, with approximate paging. Doing it as one statement inside one database lets the sort and the limit push down to the storage engine, makes paging exact, and removes the hop.

Message bodies are not combined this way. Audit and failed bodies share one store, each owning a prefixed keyspace so an edited message's failed body cannot collide with its audited body, and `IBodyStorage.TryFetch` resolves in a fixed order. That is one store with a lookup order, not a merge across two.

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

The audit runtime ships inside the existing primary artifact. There is no new assembly and no new deployment unit. The copied ingestion metrics share the primary's OpenTelemetry exporter, enabled by the standard `OTEL_EXPORTER_OTLP_ENDPOINT` variable.
