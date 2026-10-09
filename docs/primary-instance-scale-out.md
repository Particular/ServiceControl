# Primary instance scale-out

The primary instance ingests failed messages, runs retries and archiving, monitors heartbeats and custom checks, dispatches integration events, collects usage data and serves the HTTP API that ServicePulse polls. This page says how many primary processes may run against one database and one set of queues, what each supported arrangement runs, and which parts of the full instance assume that they are the only process. The reasoning is in [the scale-out decision](decisions/2026-10-09-primary-instance-scale-out.md). How ingestion batches are written in parallel is covered in [ingestion-pipeline.md](ingestion-pipeline.md), and the retry state machine in [bulk-retries-design.md](bulk-retries-design.md).

## Supported arrangements

### One full instance

The default. Every component runs in one process, and the in-memory state listed below is complete because that process sees every message and every request.

### One full instance plus error ingestion workers

`ServiceControl.exe --error-ingestion-only` runs a host that drains the error queue into the shared database and nothing else. Any number of workers may run beside exactly one full instance. The mode requires SQL Server or PostgreSQL storage. It refuses to start on RavenDB, because the RavenDB persister merges failed messages with patch scripts that are not ordered against each other (`RavenIngestionUnitOfWorkFactory`). A worker hosts the event log, external integration writes, recoverability (ingestion only), heartbeat monitoring (warm-up only, no checking) and custom checks components, and no NServiceBus endpoint. The acceptance test `When_hosting_error_ingestion_only` asserts the exact set of hosted services a worker runs, so adding a hosted service anywhere forces a decision about whether it may run on every node.

Workers need:

- A database already provisioned by the full instance's `--setup`. A worker runs no setup.
- Message bodies every host can read. File system body storage only works on a shared mount. Azure Blob and S3 storage work as they are.

### Active-passive failover of the full instance

A standby copy of the full instance may take over after the active one has stopped. The standby must start only after the active process has exited. The two must never run at the same time, because the components listed below behave as if they are alone. In practice that means a cluster resource or an orchestrator with a single replica and a recreate strategy, never a rolling update.

Both copies need identical configuration: the instance name, which names the input queue; the hostname and port, which derive `Settings.InstanceId` for body URLs and the single-message retry route; the license, which is read from each machine; and the body storage location, which must be a shared mount, Azure Blob or S3. The transport must be a broker or a database. MSMQ and the Learning transport are machine-local, so the standby would own a different set of queues. Embedded RavenDB cannot fail over, because its data directory is local to the machine. Failover needs SQL Server, PostgreSQL or an external RavenDB server.

A standby start replays what a single-node restart does today:

- Retry batches the active node was still claiming (`MarkingDocuments`) are adopted and staged. A batch it was forwarding resumes in timeout mode and completes 45 seconds after the staging queue goes quiet (`RetryProcessor`, `RetryDocumentManager`).
- Endpoint monitoring is warmed from `KnownEndpoints` with no heartbeat readings, so every monitored endpoint counts as failing until its next heartbeat arrives, and the first heartbeat re-enables monitoring for endpoints a user had disabled (`EndpointInstanceMonitor`).
- Integration events the active node published but had not yet deleted are published again. Dispatch is at-least-once, and subscribers must tolerate duplicates (`ExternalIntegrationRequestsDataStore`).
- Internal custom check rows keyed on the old host id stay in the database with their last status.

### Not supported: more than one full instance

Two or more full instances against one database and one set of queues, whether deliberately behind a load balancer or through an overlapping failover, is not supported. Nothing refuses the second start. The sections below list what goes wrong and what each component would need.

## What is safe to share today

These paths already tolerate several processes, which is what the ingestion workers rely on:

- Error ingestion. Competing consumers on the error queue, and a batch writer whose upserts are guarded by attempt times, whose inserts tolerate a competing writer's identical row, and whose lock order is stable (`FailedMessageBatchWriter` and the two `IFailedMessageIngestionSqlDialect` implementations). `ErrorIngestionConcurrencyTests` covers it.
- Retry acknowledgements and retry claims. A successful retry resolves the message only when no newer attempt is stored, and claims are insert-if-absent, so two batches never stage the same message (`RetryBatchStore`, `IRetryBatchSqlDialect`).
- Every controller that reads from and writes to the database directly. The ETag is a digest of the returned rows and totals with no per-process input (`DataVersion`), so a conditional GET answered by another node works.
- Authentication. JWT bearer tokens are validated against the OpenID Connect authority on every request. There is no cookie, session or data protection key ring.
- Scatter-gather to audit instances, the health endpoints, CORS, forwarded headers and pagination hold no server state between requests. Readiness reports the node's own ingestion watchdog, which is what a load balancer probe needs.
- Subscription rows on SQL Server and PostgreSQL, settings rows, custom check rows and known endpoint rows are written through upserts that handle the insert race.
- Azure Blob and S3 body storage, and file system body storage on a shared mount. Bodies are immutable and keyed by message id.

## What assumes one process

Each table names the component, the state or job that belongs to one process, what a second full instance does to it, and what it would need to become deployment-global. File names are given so a row can be checked against the code. Line numbers are not, because they change.

### Retries

| Component | Per-process state or unguarded job | With a second full instance | What it would need |
| --- | --- | --- | --- |
| Staging pass (`RetryProcessor`, `RetryStagingStore`) | `GetStagingBatch` is a plain select of the oldest `Staging` batch, and nothing marks it taken until `MarkBatchAsForwarding` | Both nodes stage the same batch. Every message is sent to the staging queue twice under two staging ids, the event log and audit entries are duplicated, and the last `MarkBatchAsForwarding` wins | Claim the batch with a conditional status update before staging, or run the processor under a lease |
| Forwarding pass (`RetryProcessor`, `ReturnToSenderDequeuer`) | Every node holds a receiver on the one `{InstanceName}.staging` queue and follows the same forwarding pointer. In counting mode the forwarder exits only when its own counter reaches the batch size | Both nodes run the forwarder for the same batch, the transport splits the messages between them, neither reaches its count, and both retry processors block until a restart. Messages carrying the other staging id are consumed and dropped | One receiver per batch, and an inactivity timer in counting mode so a short count cannot block forever |
| Forwarding pointer (`RetryBatchNowForwardingEntity`, `RetryStagingStore.PointForwarderAt`) | A single row written update-else-insert with no concurrency token, deleted after every batch | Two nodes staging different batches either collide on the insert, an unhandled key violation, or the second overwrites the first and leaves a `Forwarding` batch nothing will ever forward | A guarded upsert, and a recovery pass for `Forwarding` batches without a pointer |
| Orphan adoption (`RetryDocumentManager`, `AdoptOrphanBatchesFromPreviousSessionHostedService`) | Orphans are `MarkingDocuments` batches whose `RetrySessionId` differs from this process's static GUID. The sweep runs at startup, repeats while it finds something, and stops for good when it finds nothing | A starting node adopts batches a live node is still claiming and stages them half-built. A node that crashes beside a running peer leaves batches no running node will ever adopt | A liveness record per session, or adoption under the same lease as the processor |
| Recovery flag (`RetryProcessor.isRecoveringFromPrematureShutdown`) | A per-process boolean, true until the node's first staging pass | A node restarted while its peer forwards treats the live batch as abandoned, forwards it in timeout mode and deletes it. The peer's counting forwarder never completes and consumes a share of every later batch | Record ownership on the pointer instead of inferring it from process state |
| Retry progress (`RetryingManager`, `InMemoryRetry`) | Operation state and progress in a per-process dictionary that is never persisted and never expires. Startup seeds a `Preparing` entry for every open batch group | Progress exists only on the node that processes the batches. Other nodes show the operation as preparing forever, silently drop `RetryAllInGroup` and bulk requests for it, and refuse to archive the group | Persist operation state and read it back in the API and the guards |
| Groups API (`GroupFetcher`, `FailureGroupsController`) | Merges database rows with `RetryingManager` and `OperationsManager` progress | Status, progress and acknowledgement flags differ per node. While a group batch forwards, nodes that never saw the operation throw in `MapOpenForForwardingOperation` and answer 500 | Same as above |
| Bulk requests (`RetriesGateway`, `BulkRetryBatchCreationHostedService`) | Requests queue in process on the node that handled the command, deduplicated against that node's operations | Two nodes run the same bulk retry. Claims keep each message to one batch, but the second operation completes with zero messages and writes its own history and acknowledgement rows | Deduplicate requests in the database |
| Archive progress and guards (`OperationsManager`, `EFCoreArchivingManager`, `ArchiveAllInGroupHandler`, `RetryAllInGroupHandler`, `UnacknowledgedGroupsController`) | Archive progress in per-process dictionaries. The archive-while-retrying and retry-while-archiving guards read them, and acknowledging a completed archive removes a local entry | The guards are bypassed across nodes, so an archive and a retry of one group interleave and archived messages are forwarded. The completed-archive card appears on one node, and the acknowledge call answers 404 elsewhere | Persist progress (the `ArchiveOperations` row already carries counts) and base both guards on database state |
| Error queue address (`ErrorQueueNameCache`, `ReturnToSenderDequeuer`, `EditHandler`) | Set by the dequeuer's `StartAsync`, which runs after the NServiceBus endpoint has started receiving | A node that handles `EditAndSend` in that window has already marked the original resolved and then throws. The command ends in the instance's own error queue | Resolve the address at composition time |

### Heartbeats and endpoint monitoring

| Component | Per-process state or unguarded job | With a second full instance | What it would need |
| --- | --- | --- | --- |
| Heartbeat recording and the dead check (`HeartbeatHandler`, `EndpointInstanceMonitoring`, `HeartbeatMonitoringHostedService`) | Heartbeats are commands on the shared input queue. Each node records the share it received in a per-process dictionary and runs the check every 5 seconds against the 40 second grace period. No last-seen time is persisted | Each node sees a fraction of every endpoint's heartbeats, declares healthy endpoints dead and restored, and writes the event log rows, integration events and emails for each transition | Persist a last-seen time per endpoint instance and run the check on one node |
| Monitoring API (`EndpointsMonitoringController`) | `GET /api/endpoints`, `/api/endpoints/known` and `/api/heartbeats/stats` answer from the dictionaries, which are loaded from `KnownEndpoints` once at startup | Lists, counts, last report times and ETags differ per node. Endpoints detected on one node are absent on the others until they restart | Serve the endpoints from the database |
| Monitoring toggles and deletes (`EndpointsMonitoringController`, `MonitoringDataPersister`) | `PATCH` and `DELETE /api/endpoints/{id}` check existence in memory and mutate the serving node's monitor plus the database row | Other nodes keep the old flag and the deleted endpoint, keep raising heartbeat events for it, and answer 404 for an endpoint they never saw | Check existence and read the flag from the database at decision time |
| First heartbeat (`EndpointInstanceMonitor`, `MonitoringDataStore.CreateOrUpdate`) | The first heartbeat a node sees for an instance sets `Monitored` in memory and writes that value to the row | Every node start re-enables monitoring for endpoints a user disabled, and writes a "confirmed to have heartbeats" event log row per node | Insert-if-missing, and never touch `Monitored` on an existing row |
| Instance purge (`HeartbeatEndpointSettingsSyncHostedService`) | Every 6 hours, for endpoints with instance tracking off, deletes `KnownEndpoints` rows for instances that this node's memory says are not sending heartbeats, keeping the last one in its own dictionary order. Then deletes settings rows for endpoint names with no known endpoint row | Live instances are deleted. Two nodes keep different survivors, so every row of an endpoint can go, and the following sweep removes the user's tracking setting and recreates it with the default | Run on one node from persisted liveness |
| MassTransit connector heartbeat (`MassTransitConnectorHeartbeatStatus`) | The last connector heartbeat in a per-process property, read by `/api/configuration` and the license page | Presence, queue list and the license extension link differ per node | Persist the latest heartbeat |

### Integration events and subscriptions

| Component | Per-process state or unguarded job | With a second full instance | What it would need |
| --- | --- | --- | --- |
| Dispatch on both persisters (`ExternalIntegrationRequestsDataStore`, `EventDispatcherHostedService`) | Select, publish, then delete, with a per-process semaphore as the only exclusion | Both nodes publish the same batch, so every contract event reaches subscribers up to once per node | Claim rows before publishing, with a delete that returns the rows or a select for update that skips locked rows, or one dispatcher under a lease |
| Subscription cache (`SubscriptionStorage`, SQL Server and PostgreSQL) | Subscriber lookups are cached per process for `SubscriptionCacheDuration`, 60 seconds by default, and a subscribe or unsubscribe invalidates only the handling node's cache | Other nodes publish to a stale subscriber list for up to the cache duration. Message-driven transports only | Set the duration to zero on multi-node deployments, or version the table |
| Subscription document (`RavenSubscriptionStorage`) | All subscriptions in one document, mirrored into memory at startup and stored back whole with no change vector | A node never learns about subscriptions made through another, and its next save overwrites the document with its stale copy | Reload per lookup and store with optimistic concurrency |

### Throughput collection and licensing

| Component | Per-process state or unguarded job | With a second full instance | What it would need |
| --- | --- | --- | --- |
| Audit and cloud broker collectors (`AuditThroughputCollectorHostedService`, `BrokerThroughputCollectorHostedService`) | Read `LastCollectedDate`, fetch absolute daily totals, then add them through the additive upsert (`IEndpointThroughputDialect`). Both start 40 seconds after host start, then daily | Overlapping gathers add the same days twice, so audit, Azure Service Bus and Amazon SQS throughput is multiplied for every day in the window | One collector under a lease, or an idempotent set for absolute totals |
| Sampling broker queries (`RabbitMQQuery`, `SqlServerQuery`, `PostgreSqlQuery`) | Sample a shared broker counter from a per-process baseline and record each delta | Every node adds its own deltas, so broker throughput is multiplied for as long as the nodes run | One collector under a lease |
| License state (`ActiveLicense`, `LicenseCheckHostedService`, `LicenseController`) | Read from each machine's license sources into a per-process singleton, refreshed every 8 hours or on `?refresh=true` for that node only | Nodes with different license files answer differently, and a refresh fixes one node | Install the same license on every machine and refresh each |

### Retention

| Component | Per-process state or unguarded job | With a second full instance | What it would need |
| --- | --- | --- | --- |
| Sweep (`RetentionSweeper`) | Registered on every full instance (`PersistenceFactory` sets `RunRetentionSweep` from the ingestion-only flag). The single-flight guard is a per-process semaphore | Every node sweeps hourly. The end state is correct because the deletes re-assert the cutoff, but the work is duplicated and the sweeps overlap on a backlog | A database lease, or a per-node setting |
| Manual purge (`SystemMaintenanceController`, `RetentionApi`) | Start, the 409 "already running" answer and the status come from the serving node's sweeper | A second node accepts a second purge, and a status poll routed to another node reports it finished | Persist the purge run |

### Custom checks and notifications

| Component | Per-process state or unguarded job | With a second full instance | What it would need |
| --- | --- | --- | --- |
| Internal checks (`InternalCustomChecksHostedService`, `CustomCheckDetail`) | Rows are keyed by instance name, host id and check id. The host id is NServiceBus's default, derived from the machine name and executable path, and nothing removes a dead host's rows | One row per node for every internal check, so a shared condition such as a failed import raises N failures, N emails and N integration events. Two nodes with the same host id flip one row. A replaced node's rows stay | Key deployment-global checks on the instance name only, and expire stale rows |
| Email throttling (`EmailThrottlingState`, `SendEmailNotificationHandler`) | The throttle after an SMTP failure and the send semaphore are per process | During an SMTP outage every node attempts and fails on its own | Acceptable, or a shared throttle row |
| Saga audit misconfiguration (`SagaAuditMisconfigurationCustomCheck`) | A static flag set by the handler on the node that consumed the misrouted message | Only that node's row can fail | Persist the detail |
| Remote availability (`CheckRemotes`, `RemoteInstanceSetting.TemporarilyUnavailable`, `ScatterGatherApi`) | A per-process flag that skips an audit instance for up to 30 seconds after a failure | Consecutive requests alternate between complete and incomplete results while an audit instance is unreachable from one node | Acceptable as transient |

### Storage, transport and hosting

| Component | Per-process state or unguarded job | With a second full instance | What it would need |
| --- | --- | --- | --- |
| File system body storage (`FileSystemBodyStoragePersistence`, `BodyStorage`) | Bodies are files under the configured path on the ingesting node | On other nodes the body is unavailable. The API answers 204, a retry forwarded by another node throws, and the sweep deletes the row while the file stays | A shared mount, Azure Blob or S3, and a startup warning when the path is local |
| MSMQ and Learning transports (`MsmqTransportCustomization`, `LearningTransportCustomization`) | Queues are local to the machine | Nodes on different machines own disjoint queues and never compete, and a failover strands the old machine's queues | Broker or database transports only |
| Embedded RavenDB (`RavenEmbeddedPersistenceLifecycle`) | An in-process server with a local data directory | Each node holds a disjoint database | External RavenDB, SQL Server or PostgreSQL |
| Import failed errors (`ImportFailedErrorsCommand`) | Hosts a full NServiceBus endpoint on the instance input queue with every handler registered but only the recoverability component configured | Heartbeats, custom check reports and endpoint registrations it dequeues fail activation and land in the error queue. A running instance must be stopped first, which a highly available deployment cannot do | A send-only host, or an in-process import |
| Instance id (`Settings.InstanceId`) | Derived from each node's hostname, port, virtual directory and TLS scheme | Body URLs and the single-message retry route carry the serving node's id, and another node answers 400 for an id it does not recognise, unless every node is configured with the load balancer's address | Identical hostname and port on every node |
| Setup (`SetupCommand`, `--setup-and-run`) | Runs migrations, queue creation and body storage provisioning per node | Migrations serialise on EF Core's migration lock. S3 bucket creation is check-then-create, and the loser's setup fails | Run `--setup` once, and start the others with plain run |

Smaller effects: event log `RaisedAt` and retention cutoffs come from whichever node wrote or swept, so clock skew between nodes shifts ordering and expiry by the skew. The message action audit trail is written to each node's own log sink, so one operation is split across files and has to be joined on the operation id. The usage report's `Host.*` and `Heartbeats.*` environment values describe the node that built it. Retry history can briefly exceed the configured depth. Group archive batch events name the fetched ids rather than the affected rows.

## Changing a component

A new hosted service, message handler or controller in the primary has to answer two questions before it is merged:

- Does it keep state that must be the same on every node, such as a dictionary that backs an API answer or a guard? If so, the state belongs in the database, or the component joins the tables above with what it would need.
- Is it a job that a deployment may run only once? If so, it is switched off in the ingestion-only host (`ErrorIngestionOnlyCommand`) and listed above. `When_hosting_error_ingestion_only` fails when a hosted service appears that the test does not expect.
