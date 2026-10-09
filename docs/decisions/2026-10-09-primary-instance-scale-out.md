# Scale-out of the primary instance is limited to error ingestion

- Date: 2026-10-09
- Status: Accepted
- Implementation: [#5801](https://github.com/Particular/ServiceControl/pull/5801) added the `--error-ingestion-only` host and the single-owner split it relies on. Link further pull requests here as they open.

## Context

The primary instance is one process that does everything the error side of the platform needs. It ingests failed messages, stages and forwards retries, archives groups, monitors heartbeats and custom checks, dispatches integration events, collects usage data, sweeps retention and serves the HTTP API that ServicePulse polls. Its default storage is an embedded RavenDB server inside the process, so a second process sharing the data was never part of the design.

SQL Server and PostgreSQL storage change that. Customers run those databases as highly available clusters, and once the database survives a node failure the question of whether the primary can do the same follows. It comes in two forms: several primaries behind a load balancer sharing the work, and a standby that takes over when the active one fails. The error ingestion path was built for concurrent writers from the start ([ingestion-pipeline.md](../ingestion-pipeline.md)), and #5801 used that to add ingestion workers that run beside one full instance. That change listed what a worker must not run: the retry pipeline, the retention sweep, integration event dispatch and heartbeat monitoring. It did not say whether the full instance itself could run more than once.

An audit on 7 October 2026 read every hosted service, message handler, controller and persister path in the primary against both forms, with every full instance configured identically and the HTTP API behind a round-robin load balancer. [primary-instance-scale-out.md](../primary-instance-scale-out.md) holds the result per component. In summary:

- Ingestion, retry acknowledgements, retry claims, every database-backed API read and write, ETags, authentication and scatter-gather already tolerate several processes.
- The retry pipeline assumes one process in five places. The staging batch is selected without a claim. Every node holds a receiver on the one staging queue, and the counting-mode forwarder can only finish when its own node has received the whole batch. The forwarding pointer is a single unguarded row. Orphan adoption treats every other process as dead. Retry and archive progress, and the guards between them, live in memory. Two full instances duplicate retries, then stop retrying altogether until a restart.
- Heartbeats are commands on the shared input queue, each node records the share it receives in memory, and each node runs the dead check. Two full instances report healthy endpoints as dead and restored, and publish those transitions to the event log, to integration subscribers and by email.
- Integration event dispatch is select, publish, delete with no claim, so every event reaches subscribers once per node.
- Throughput collection reads the last collected day and then adds absolute daily totals, and on RabbitMQ, SQL Server and PostgreSQL transports it samples a broker counter per process, so usage figures are multiplied by the node count.
- Several answers come from per-process state: the endpoints and heartbeat views, group retry and archive progress, license status, the MassTransit connector heartbeat and the manual purge status.
- Some state is local to the machine: file system message bodies, the license file, the NServiceBus host id that keys internal custom check rows, and MSMQ queues.

Three constraints shape the decision:

- The bulk retry design depends on one process serialising the staging and forwarding steps ([bulk-retries-design.md](../bulk-retries-design.md)). Making that safe across processes needs a lease or a claim at every step, and a defined handover for a lease holder that dies.
- Heartbeat readings arrive every ten seconds per endpoint instance and retry progress changes per batch, which is why both are kept in memory. Persisting them on every change adds write load and needs a schema the current persisters do not have.
- No test runs two full instances against one database. The existing concurrency tests exercise several writers inside one process, and the ingestion-only test asserts the hosted service set of a worker.

## Decision

Exactly one full primary instance runs against a database and its queues. Horizontal scale comes from `--error-ingestion-only` workers on SQL Server and PostgreSQL. High availability for the full instance is active-passive: a standby starts only after the active process has exited, and the two never run at the same time.

The components that assume one process, and what each would need to become deployment-global, are listed in [primary-instance-scale-out.md](../primary-instance-scale-out.md). That list is the contract. A change that adds a hosted service, a message handler or a controller to the primary decides whether it may run on every node. A job that may run only once is switched off in `ErrorIngestionOnlyCommand` and added to the list, and `When_hosting_error_ingestion_only` fails when a hosted service appears that it does not expect.

Nothing detects a second full instance at runtime, and this decision does not add detection. The constraint is stated in `Help.txt` and on the topic page.

## Consequences

- Customers get high availability by making the database highly available and running the full instance as a single-replica resource that an orchestrator or a cluster restarts or moves. A rolling update, or a cluster that starts the standby before the active has stopped, produces the duplicate retries and false heartbeat alarms the audit describes. Accepted and documented. The recovery paths a standby relies on (orphan adoption, forwarding resume and at-least-once dispatch) already exist for a single-node restart.
- A standby start replays single-node restart behaviour. Every monitored endpoint shows as failing until its next heartbeat, monitoring is re-enabled for endpoints a user disabled, up to one batch of integration events is published again, and a forwarding batch completes 45 seconds after the staging queue goes quiet. Accepted. The first two are what a restart does today.
- Everything except ingestion is bounded by one process. Accepted. Ingestion is the path whose load grows with the system's message volume, and the workers cover it.
- The HTTP API has one target. A load balancer in front of the primary only provides failover, because workers host only the health endpoints. Accepted.
- File system body storage must be a shared mount whenever more than one host writes or reads bodies. This is not enforced at startup. Accepted for now, with the startup warning that #5801 left open as follow-up work.
- An operator who starts two full instances by mistake gets no error, only the symptoms on the topic page. The risk is bounded by the symptoms being loud: retries stop and the event log fills with heartbeat alarms. A startup lease would turn the mistake into a refusal. See the alternatives.
- The per-component list ages with the code. Mitigation: the review questions on the topic page, and the ingestion-only test as the tripwire for hosted services.

## Alternative approaches

- Database leases for the jobs that may run once. A lease row or an advisory lock (`sp_getapplock`, `pg_advisory_lock`) around the retry processor and staging receiver, the retention sweep, the dispatcher, the throughput collectors and the settings sync would let several full instances run with one of them doing the single-owner work. This is the main point of leverage and the first step if active-active is ever offered. It is deferred because a lease on its own leaves the in-memory API state, the heartbeat split and the per-node license as they are, and because every lease needs renewal, expiry and a defined handover for a holder that dies, which is a redesign of the retry recovery paths.
- Persist the in-memory state. A last-seen time per endpoint instance written on every heartbeat, retry and archive progress written per batch, the connector heartbeat and the purge status in rows, and the groups and endpoints API served from the database. Together with leases this is the full active-active design. It is deferred for the same reason, and because it only pays for itself once leases exist.
- A designated owner node. A per-node setting, like `RunRetentionSweep` today, that turns the single-owner jobs off on all but one full instance, so the others serve the API and ingest. It is rejected because heartbeats and commands still arrive on the shared input queue and are consumed by every node, so the owner's view of endpoints and retry progress stays partial, and because a second owner is a configuration error nothing detects.
- Separate queues per concern. Route heartbeats, custom check reports and retry commands to a queue only the owner node consumes. It is rejected because it changes the platform connection contract every endpoint and ServicePulse rely on, and it still needs the owner designation above.
- Sticky sessions at the load balancer. They are rejected because they make the in-memory API views consistent for one client and change nothing about the background jobs or the heartbeat split.
- Transport-level exclusivity, such as a single active consumer on RabbitMQ. It is rejected because it is not available on every supported transport, and because it covers only the receivers and none of the timers.
