# Host Audit Ingestion in the Primary Instance

## Summary

For SQL Server and PostgreSQL persistence, move audit ingestion and the supporting audit capabilities into the primary ServiceControl process. A normal primary instance ingests audit messages by default once its persister advertises audit support. A setting disables its receiver. Additional primary processes can run with `--audit-ingestion-only` to scale ingestion through competing consumers.

The existing standalone RavenDB audit instance remains supported and retains its current behavior. RavenDB does not gain combined hosting or audit-ingestion-only support.

This plan covers the contracts, project boundaries, host composition, settings, and fail-fast command-line surface needed before EF audit persistence is implemented. It makes no installer changes, so RavenDB instances are unaffected by construction. It does not implement EF entities, migrations, SQL queries, retention algorithms, or provider registrations.

## Goals

- Host audit ingestion in the normal SQL Server or PostgreSQL primary instance.
- Allow audit ingestion to be disabled in the normal primary instance.
- Add `--audit-ingestion-only` so additional processes can scale audit ingestion.
- Include the audit capabilities that must exist when audit data is local: SagaAudit ingestion, failed audit imports, forwarding, ingestion metrics, health, and querying.
- Reuse primary-owned capabilities rather than duplicating them: endpoint detection, retry acknowledgement handling, body storage, retention sweeping, and known endpoints.
- Use the same database and existing primary EF persistence for primary and audit data.
- Keep existing primary API routes and their existing authorization policies.
- Continue supporting additional audit remotes through the existing scatter-gather.
- Restore the platform capabilities that today depend on an audit remote existing: platform connection details, saga audit forwarding, and licensing throughput collection.
- Put contracts and composition boundaries in place without starting the EF audit implementation.

## Non-goals

- Adding audit persistence to SQL Server or PostgreSQL in this work.
- Adding combined hosting or ingestion-only support to RavenDB.
- Replacing or removing the existing standalone RavenDB audit instance.
- Supporting the standalone `ServiceControl.Audit` executable with SQL Server or PostgreSQL.
- Finalizing provider-specific retention, partitioning, full-text search, or body storage implementations.
- Migrating existing RavenDB audit data into EF persistence.
- Running error ingestion and audit ingestion in the same ingestion-only worker.

## Decisions

### Data and persistence

- Audit and primary data use the same database and connection configuration.
- Shared data, including known endpoints, uses the existing primary tables.
- Audit-owned data uses explicit table names in the existing default schema, for example `AuditMessages`, `FailedAuditImports`, and `SagaSnapshots`.
- SQL Server and PostgreSQL extend the existing `ServiceControl.Persistence.EFCore`, `ServiceControl.Persistence.EFCore.SqlServer`, and `ServiceControl.Persistence.EFCore.PostgreSql` projects. The three audit-specific EF projects from the earlier spike are not recreated.
- The `SagaSnapshots` table maps the existing `ServiceControl.SagaAudit.SagaSnapshot` type. That type lives in `ServiceControl.Audit.Persistence.SagaAudit` and is already on the primary's reference graph through `ServiceControl.SagaAudit`. It does not move.

### Hosting

- The normal primary retains the existing HTTP routes and serves local audit data through them. There is no separate SQL Server or PostgreSQL audit HTTP service.
- `--audit-ingestion-only` always ingests and does not host an NServiceBus endpoint.
- `--audit-ingestion-only` and `--error-ingestion-only` are mutually exclusive. Passing both fails at startup with a clear message. Each queue gets its own worker pool so the two can be scaled independently, and each keeps a single, auditable component list. Combining them is a possible follow-up.
- Disabling ingestion in the normal primary stops only its receiver. Local queries, SagaAudit, failed-import tooling, and other audit capabilities remain active because workers may still ingest.

### API

- Local audit data is served through the existing primary routes under their existing policies. `/api/messages` and its variants stay on `error:messages:view`, `/api/sagas/{id}` stays on `error:sagas:view`, and `endpoints/{endpoint}/audit-count` stays on `error:messages:view`.
  A primary configured with an audit remote already serves that remote's audit data under `error:messages:view` today, so local audit data inherits an established gate and nothing about the `my/routes` manifest or ServicePulse navigation changes. The standalone audit instance keeps its own `audit:*` policies and its anonymous audit-count route.

### Settings and installer

- The primary reads the audit settings under the same key names the audit instance uses. Key names are reused rather than invented.
- No installer changes in this work. `ServiceControlInstaller.*`, `ServiceControl.Config` and `ServiceControl.Management.PowerShell` are untouched, so RavenDB instances behave exactly as they do today by construction. SCMU and PowerShell support for EF storage types is a separate, undecided workstream, and the audit settings belong to it. See "Settings and commands" for the handoff note.

### Observability

- The copied ingestion metrics keep their OpenTelemetry implementation. The primary gains the three OpenTelemetry package references and an `OtlpEndpointUrl` setting, and the meter is renamed from `Particular.ServiceControl.Audit` to `Particular.ServiceControl`.
  Copying faithfully keeps the primary and audit implementations comparable for the later reuse assessment, and it opens the door to moving error ingestion onto the same instrumentation. The cost is three new packages in the shipped primary artifact.

## What the primary already owns

Several capabilities the earlier draft treated as "moving from audit" already exist on the primary. The plan reuses them rather than copying an audit equivalent.

| Capability | Where it already lives | Consequence for this work |
| --- | --- | --- |
| Local-first scatter-gather | `ScatterGatherApi.Execute` runs the local query first, then remotes | No new "query coordinator" is needed. With zero remotes it is already a local-only query. |
| Message view queries | `IMessagesViewDataStore` | The five audit message queries extend this contract. Its EF implementation performs the union. |
| Saga history DTOs | `ServiceControl.Audit.Persistence.SagaAudit`, referenced through `ServiceControl.SagaAudit` | No new DTO project. `GetSagaByIdApi` stops being remote-only. |
| Audit retention period setting | `ServiceControl/AuditRetentionPeriod`, already validated and published in `/api/configuration` | Reuse it. Define what `null` means now that it drives behavior. |
| Full-text search toggle | `PersistenceSettings.EnableFullTextSearchOnBodies` | One value governs error and audit bodies. |
| Max body size | `BodyStorageSettings.MaxBodySizeToStore` | One value governs error and audit bodies. |
| Body storage and installers | `IBodyStorage`, `IBodyStoragePersistence`, FileSystem, AzureBlob and S3 implementations plus installers | Audit bodies use the same store. There is no database body store. |
| Retention sweeping | `RetentionSweeper`, a `BackgroundService` registered inside `BasePersistence` | Audit retention extends the sweeper. There is no host-level retention component. |
| Endpoint detection pattern | `DetectNewEndpointsFromErrorImportsEnricher` plus `unitOfWork.Monitoring.RecordKnownEndpoint` in `ErrorProcessor` | The audit enricher adopts the same shape. |
| Retry acknowledgement handling | `RetryConfirmationProcessor`, driven by acknowledgements arriving on the error queue | Unchanged. See "Retry acknowledgements" below. |
| Internal custom checks | `services.AddCustomCheck<T>()` plus `InternalCustomChecksHostedService` | The copied audit checks register through DI, not through `configuration.AddCustomCheck`. |
| Saga audit misconfiguration handling | `SagaUpdatedHandler` and `SagaAuditMisconfigurationCustomCheck` | Both need work. See "Platform connection details". |

## Project and boundary assessment

Do not add a new runtime project for the first implementation. Copy the audit runtime behavior needed by the SQL Server and PostgreSQL primary host into the `ServiceControl` project, then adapt that copy to primary persistence, primary settings, and the endpoint-free ingestion-only profile.

The copied primary implementation includes:

- Audit receiving, batching, and shutdown coordination.
- Audit message parsing and enrichment.
- Saga snapshot and relationship processing.
- Failed-ingestion handling and failed-audit orchestration.
- Forwarding orchestration.
- Ingestion metrics and readiness state.
- Registrations for the normal and ingestion-only primary hosts.

Do not make the primary executable reference `ServiceControl.Audit.csproj`. That project remains a standalone composition root containing RavenDB persistence selection, standalone settings, API hosting, installer commands, and its own NServiceBus endpoint.

Keep these concerns in the existing standalone audit executable:

- RavenDB persistence loading and lifecycle.
- Standalone audit settings and maintenance mode.
- Standalone audit HTTP API composition.
- Standalone installers and queue setup behavior.
- The existing audit NServiceBus endpoint and its `ReportCustomChecksTo` reporting.

### Shared surface

The two executables are not isolated. Four projects sit underneath both, and this work must change at least one of them.

| Project | Shared how | Risk |
| --- | --- | --- |
| `ServiceControl.SagaAudit` | Compiled into `ServiceControl.Audit` by source, referenced as a project by `ServiceControl.Persistence` | A change to `SagaSnapshotFactory` or `InvokedSagasParser` silently changes the shipped audit executable. |
| `ServiceControl.Audit.Persistence.SagaAudit` | Referenced by the audit persisters, the Raven primary persister, and transitively by `ServiceControl.Persistence` | The saga DTOs are shared. Changing their shape affects Raven audit documents. |
| `ServiceControl.Infrastructure` | `Watchdog`, `DeterministicGuid`, `ReadOnlyStream`, `LoggerUtil`, `Permissions` | A shutdown or watchdog change for the primary changes audit shutdown too. |
| `ServiceControl.Transports` | `ITransportCustomization.CreateTransportInfrastructure` must change for per-receiver concurrency | Both hosts create their receivers through this method. |

Any pull request touching these four projects runs the full audit acceptance suite and states in its description why the change is safe for the audit executable.

Note that the baseline already breaks the "audit runtime untouched" guarantee: PR #5800 modifies `AuditIngestion`, `AuditIngestor` and `AuditPersister` in the standalone audit project. The guarantee this plan makes is narrower and honest: no *behavioral* change to the standalone audit executable, verified by its acceptance suite.

### Divergence and later reuse

The copied implementations are expected to diverge initially. The primary copy removes endpoint assumptions, uses the primary persistence unit of work, and participates in local queries. The RavenDB implementation remains optimized for its existing standalone process. Once both paths are stable, compare them and extract shared code only where doing so removes meaningful duplication without coupling their composition roots.

Do not add a separate contracts project. Define the new primary audit contracts in `ServiceControl.Persistence`. Leave the current RavenDB audit persistence contracts and implementation untouched unless a later reuse refactor demonstrates a clear benefit.

## Target host profiles

| Capability | Normal SQL/Postgres primary | `--audit-ingestion-only` | Standalone RavenDB audit |
| --- | --- | --- | --- |
| Audit receiver | Enabled by setting, default on when persistence supports audit | Always enabled | Unchanged |
| Primary NServiceBus endpoint | Yes | No | Not applicable |
| Existing audit NServiceBus endpoint | No | No | Unchanged |
| Primary API | Yes | Health endpoints only, mapped as minimal API routes, no controllers | Existing audit API unchanged |
| Local audit queries | Yes | No | Unchanged |
| Optional remote audit queries | Yes | No | Unchanged |
| Endpoint discovery | Shared persistence unit of work | Shared persistence unit of work | Unchanged |
| Retry acknowledgement dispatch | Yes | Yes | Unchanged |
| Retry acknowledgement recording | Yes, via error ingestion | No | Not applicable |
| Forwarding | Yes | Yes | Unchanged |
| Failed-audit storage | Yes | Yes | Unchanged |
| Failed-audit reimport command | Yes | No | Unchanged |
| Retention | Inside the persister, gated by `RunRetentionSweep` | Off, `RunRetentionSweep` false | Unchanged, RavenDB document expiry |
| Platform connection details for audit | Yes, local provider | No | Unchanged |
| Licensing, throughput, email, event dispatch | Yes | No | Unchanged |
| Internal custom checks | Yes | Yes | Unchanged |
| Liveness and readiness | Yes | Yes | Unchanged unless adopted separately |

## Persistence contract direction

The current primary persistence design already exposes capability-specific children from `IIngestionUnitOfWork`. Add audit as a sibling to monitoring and recoverability:

```csharp
public interface IIngestionUnitOfWork : IAsyncDisposable
{
    IMonitoringIngestionUnitOfWork Monitoring { get; }
    IRecoverabilityIngestionUnitOfWork Recoverability { get; }
    IAuditIngestionUnitOfWork Audit { get; }
    Task Complete(CancellationToken cancellationToken = default);
}
```

The audit child initially expresses only the operations the runtime requires, without defining EF storage details:

- Record a processed audit message and its body reference.
- Record a Saga snapshot.

During a batch, the audit runtime uses the existing capability children as well:

- `Monitoring.RecordKnownEndpoint(...)` records endpoints detected from audit headers.
- `Audit.RecordProcessedMessage(...)` records an audit message.
- `Audit.RecordSagaSnapshot(...)` records a Saga snapshot.
- `Complete(...)` commits all derived state atomically where the persistence supports it.

This replaces the earlier spike's duplicated `KnownEndpoints` table, insert-only staging table, and reconciliation process.

### Query contracts

Extend `IMessagesViewDataStore` rather than adding a parallel audit query contract. Its five existing queries are exactly what the scatter-gather APIs call. The EF implementation unions failed messages and audit messages, subject to the precedence rule below.

Only three genuinely new query contracts are required:

- Audit counts per endpoint.
- Saga history by saga id.
- Audit body resolution, folded into `IBodyStorage` rather than added alongside it.

Plus two ingestion-side contracts:

- Failed audit import storage and reimport selection, mirroring `IFailedErrorImportDataStore`.
- Persistence capability discovery.

Do not move the existing monolithic `IAuditDataStore` into primary persistence.

### Capability discovery

Audit support is declared in `persistence.manifest`, read by `ServiceControl.Persistence.PersistenceManifest`. Add:

```json
"SupportsAuditIngestion": true
```

to the SQL Server and PostgreSQL manifests. The property is absent from the RavenDB manifest and from every file under `LegacyArtifacts`, and absent means false.

The installer has its own `PersistenceManifest` class over the same file. It does not need the property yet, and this work does not add it there. Whenever SCMU gains EF storage support it can pick the property up from the same file, so there is one source of truth waiting for it.

Host composition must not infer support by resolving optional services or catching startup failures.

## Removing the ingestion endpoint dependency

The ingestion-only process must not host an NServiceBus endpoint. Follow the approach established by the error-ingestion scale-out work:

- The receiver owns its low-level transport infrastructure and dispatcher.
- Forwarding uses the dispatcher belonging to the receiving infrastructure.
- Shutdown stops receiving under the real shutdown token, completes the writer, drains the channel, and only then tears down transport infrastructure.
- `HostInformation` and critical-error handling are supplied directly by the host.
- `IMessageSession` is absent and tested as absent.

### Endpoint detection

`DetectNewEndpointsFromAuditImportsEnricher` currently sends a `RegisterNewEndpoint` command through `IMessageSession`, routed to the primary's queue, where `RegisterNewEndpointHandler` calls `EndpointInstanceMonitoring.EndpointDetected`.

In the primary copy it instead writes through `IMonitoringIngestionUnitOfWork.RecordKnownEndpoint`, matching `ErrorProcessor`. Both paths write the same `KnownEndpoints` table.

This is the only use of `AuditEnricherContext.AddForSend(ICommand)` in the tree. Once it is gone, `IMessageSession` drops out of the copied `AuditPersister` entirely, and the `ICommand` overload is deleted from the copied `AuditEnricherContext`. Saga relationship enrichment and saga snapshot processing never needed the endpoint.

One behavioral difference must be characterized before the switch. The command path raises the `EndpointDetected` domain event, which `MonitoringDataPersister` handles and which anything downstream of the domain event observes. The unit-of-work path does not. Write a test that pins the current observable outcome, then decide whether the audit path must raise it.

### Retry acknowledgements

Do not short-circuit retry acknowledgements into `IRecoverabilityIngestionUnitOfWork`.

`DetectSuccessfulRetriesEnricher` does not perform a round trip to the primary endpoint. It emits a raw transport operation to whatever queue the `ServiceControl.Retry.AcknowledgementQueue` header names. That header is stamped by the instance that issued the retry, using its own error queue address. The receiving instance turns it into `RecordSuccessfulRetry` through `RetryConfirmationProcessor` on the normal error ingestion path.

Writing directly to the local recoverability unit of work is only correct when the acknowledgement queue resolves to this instance's error queue. Where the retry was issued by a different primary, a direct write records the confirmation in the wrong database and the real owner never resolves the failed message. The endpoint-side acknowledgement, signalled by `ServiceControl.Retry.AcknowledgementSent`, arrives on the error queue regardless, so the transport path cannot be removed anyway.

In a combined host the acknowledgement is dispatched to the local error queue and comes straight back into local error ingestion. That is one broker round trip, it is exactly what happens today, and it is provably correct. Keep it. Revisit the optimization only with a rule that compares the acknowledgement queue against the local error queue.

Transport operations therefore remain in the audit ingestion path for two reasons only: forwarding, and the retry acknowledgement.

## API and query behavior

Keep the existing primary API routes and policies used by ServicePulse, including messages, searches, conversations, audit counts, bodies, and Saga history.

No new query coordinator is required. `ScatterGatherApi.Execute` already runs the local query first and the remotes after, so a primary with no remotes already performs a local-only query. Two existing entry points must stop being remote-only:

- `GetAuditCountsForEndpointApi.LocalQuery` returns `Empty` with the comment "Will never be implemented on the primary instance". It takes an `IMessagesViewDataStore` it never uses. Both the comment and the unused dependency go.
- `GetSagaByIdApi` derives from `ScatterGatherRemoteOnly`. It becomes a normal `ScatterGatherApi` over the new saga history contract.

### Precedence, paging, and counting

`ScatterGatherApiMessageView.ProcessResults` deduplicates on `{ReceivingEndpoint.Name}-{MessageId}` using `TryAdd`, and relies on a documented invariant: the first result set comes from the main instance, so failed-message data wins over audit data.

Once one local result set contains both failed and audited rows through a union, that cross-source invariant becomes an intra-list ordering requirement on the SQL. If an audit row precedes the failed row for the same key, ServicePulse shows a message as successfully processed when it actually failed.

The local query contract must therefore state three rules, and each needs a test:

1. **Precedence.** Within a single local result set, the failed-message row for a given `{ReceivingEndpoint.Name}-{MessageId}` must precede the audit row. Either the union orders by source, or the local query deduplicates before returning.
2. **Paging.** `ProcessResults` truncates with `Take(PageSize)`. A local union that returns `PageSize` rows per source is silently truncated. The local query returns at most `PageSize` rows after its own deduplication.
3. **Counting.** `AggregateStats` sums `TotalCount` across sources. A message that both failed and was audited must be counted once by the local query, not once per source.

RavenDB primary instances continue using their existing local-error-plus-remote-audit behavior, which these rules do not change.

## Body storage

Every ingestion process must write bodies to storage readable by the normal primary and every relevant worker. Blob, S3, and explicitly shared filesystem storage are acceptable. There is no database body store in the primary EF persistence.

### Body id keyspace

The two sides key bodies differently today, and merging them into one store without a rule produces wrong answers.

- Audit body id is `Headers.MessageId`.
- Primary external body id is `UniqueMessageId`.
- `BodyStorage.TryFetch` resolves only against `FailedMessages`, first by `UniqueMessageId` and then falling back to `MessageId`.

Without a rule, `GET /api/messages/{messageId}/body` for a message that both failed and was audited returns the failed copy, which for an edited message is a different body, and for an audited-only message returns 404 because nothing consults the audit tables.

The plan adopts the existing precedent. `FailedErrorImportEntity.ExternalBodyId(...)` already prefixes a distinct keyspace inside the same store. Audit bodies use their own prefix.

`IBodyStorage.TryFetch` gains an explicit arbitration order, stated once and tested:

1. Failed message by `UniqueMessageId`.
2. Failed message by `MessageId`.
3. Audit message by `UniqueMessageId`, including bodies embedded in the row for full-text search.

Retention must sweep both keyspaces.

### Filesystem body storage in ingestion-only mode

The earlier draft required rejecting a "node-local filesystem path" at startup. That check is not implementable. `FileSystemBodyStorageSettings` carries only a path, a compression threshold and a size cap, and nothing distinguishes a shared mount from a local directory.

Instead, filesystem body storage requires an explicit opt-in in ingestion-only mode. Add a setting that asserts the path is shared. Without it, an ingestion-only worker configured for filesystem body storage fails at startup with a message naming the setting.

Apply the same rule to `--error-ingestion-only`, which PR #5801 documents as a known gap. The two ingestion-only modes must not disagree about this.

## Platform connection details

Two primary-owned capabilities currently depend on an audit remote existing, and both break in combined mode.

### Saga audit forwarding

`SagaUpdatedHandler` throws `UnrecoverableException` when it cannot resolve `SagaAudit.SagaAuditQueue`. That key is produced only by the standalone audit instance's `ConnectionController` and reaches the primary only through `RemotePlatformConnectionDetailsProvider`. A combined primary with no remotes fails every misdirected `SagaUpdatedMessage`.

### Endpoint configuration

`/api/connection` is what ServicePulse and the Platform Connector plugin read to configure endpoints. Without an audit remote it stops advertising `MessageAudit.AuditQueue` and `SagaAudit.SagaAuditQueue`, so endpoints cannot be told where to send audit or saga data at all.

### Correction

Add an audit platform connection details provider to the primary, registered when the persister advertises audit support and the primary owns an audit queue. It supplies the same `MessageAudit` and `SagaAudit` shapes the audit instance supplies today, so ServicePulse and the plugin see no difference.

`SagaUpdatedHandler` then resolves the local audit queue through the existing `IPlatformConnectionBuilder` with no change to its logic. Whether it should instead hand the snapshot straight to the audit unit of work is an open item, not a requirement of this plan.

## Licensing and throughput

Audit throughput collection is driven entirely by remote instances. `AuditQuery.GetAuditRemotes` derives audit queues, retention and version from `configurationApi.GetRemoteConfigs()`. `AuditThroughputCollectorHostedService.SaveAuditInstanceData` sets the static `AuditQueues` list from those remotes, and `PlatformEndpointHelper.IsPlatformEndpoint` uses that list to exclude platform queues from throughput.

With local audit and no remotes:

- `AuditQueues` stays empty, so the local `audit` and `audit.log` queues are counted as customer endpoints in the licensing throughput report. This is a licensing accuracy defect, not cosmetic.
- `SaveAuditServiceMetadata` records no audit versions or transports, so `AuditServicesData` in the report is blank.
- `TestAuditConnection` reports "No Audit Instances configured" in the ServicePulse diagnostics.

`IAuditQuery` gains a local audit source alongside the remote one. It contributes the local audit queue names, the local audit retention period, and the local instance version, and it satisfies the existing "minimum 2 days retention" gate from local settings rather than from a remote's configuration payload. `Particular.LicensingComponent` is an affected project and is listed in the work plan.

## Settings and commands

### Runtime settings

Settings reach the primary through `SettingsReader`, which reads environment variables, the registry and `ServiceControl.exe.config` independently of the installer. Nothing below requires an installer change to work.

| Setting | Status | Notes |
| --- | --- | --- |
| `ServiceControl/IngestAuditMessages` | New on the primary | Effective default is `true` where the persister advertises audit support, `false` otherwise. Applies to the normal primary host only. Always on under `--audit-ingestion-only`. |
| `ServiceBus/AuditQueue` | Reused key name | Same key the audit instance reads. Default `audit`. Not written by SCMU on a primary, see the installer note. |
| `ServiceBus/AuditLogQueue` | Reused key name | Defaults to the subscoped audit queue name, matching the audit instance. Not written by SCMU on a primary. |
| `ServiceControl/ForwardAuditMessages` | Reused key name | Default `false`, matching the audit instance. Not written by SCMU on a primary. |
| `ServiceControl/AuditRetentionPeriod` | Already exists | `TimeSpan?`, already validated at min 1 hour and max 365 days, already published in `/api/configuration`. Define `null` as "use the persister default", and state that default. |
| `ServiceControl/EnableFullTextSearchOnBodies` | Already exists | One value governs error and audit bodies. |
| `ServiceControl/MessageBody/...` | Already exists | One body storage configuration governs error and audit bodies. |
| Maximum audit ingestion concurrency | New | See the transport change below. |
| `ServiceControl/TimeToRestartAuditIngestionAfterFailure` | New on the primary | Mirrors the existing error equivalent. |
| `ServiceControl/OtlpEndpointUrl` | New on the primary | Required by the copied OpenTelemetry metrics. |
| Shared filesystem body storage assertion | New | Required by ingestion-only mode. See "Body storage". |

### Key collisions

`ServiceControl` and `ServiceControl.Audit` settings can both be set by bare environment variable name. `ServiceControl.Audit` also falls back to `ServiceControl/IngestAuditMessages` for backwards compatibility, and `ServiceBus/AuditQueue` is literally the same key for both processes.

The consequence is that a primary in combined mode and a standalone audit instance sharing one environment file will collide on `INGESTAUDITMESSAGES`, `AUDITRETENTIONPERIOD`, `FORWARDAUDITMESSAGES` and `SERVICEBUS_AUDITQUEUE`.

That combination is documented as unsupported. The primary logs a warning at startup when it has audit ingestion enabled and audit remotes configured at the same time, because that is the shape most likely to hit the collision.

### Installer: out of scope, with one handoff

No files under `ServiceControlInstaller.Engine`, `ServiceControl.Config` or `ServiceControl.Management.PowerShell` change in this work. RavenDB instances therefore behave exactly as they do today, by construction rather than by test. That is the requirement.

Windows deployments of an audit-capable primary configure these settings the same way EF instances are configured today, through environment variables or the config file directly, because SCMU does not yet support EF storage types at all.

The handoff, for whoever picks up SCMU and PowerShell support for EF storage types:

`ServiceBus/AuditQueue`, `ServiceBus/AuditLogQueue` and `ServiceControl/ForwardAuditMessages` are declared `RemovedFrom = 4.0.0` in `ServiceControlSettings`, and `ServiceControlAppConfig.UpdateSettings` calls `RemoveIfRetired` on each one. Once SCMU manages an audit-capable primary, applying settings would strip that instance's audit queue configuration out of `ServiceControl.exe.config`.

Version gating cannot express "supported on EF, retired on RavenDB", so the gate has to move to the persister. `IServiceControlInstance` already exposes `PersistenceManifest` through `IPersistenceConfig`, so `ServiceControlAppConfig` has what it needs: drop `RemovedFrom` from the three `SettingInfo` declarations, branch on the manifest, and call `settings.Remove(name)` on the non-audit branch to preserve today's RavenDB behavior. The three `RemoveIfRetired` calls become no-ops once `RemovedFrom` is gone, so they have to be deleted rather than left in place.

This is recorded so the trap is visible, not so it is fixed here. It is only reachable once SCMU can create an EF instance.

### Transport concurrency

`TransportSettings` is a singleton registered once by `AddTransportForPrimary`, and `CreateTransportInfrastructure` reads `transportSettings.MaxConcurrency.Value` for its `PushRuntimeSettings`. `CustomizePrimaryEndpoint` sets the default to 10; `CustomizeAuditEndpoint` sets it to 32. In a combined host only the primary path runs, so audit ingestion would run at 10 rather than 32, a threefold regression against the standalone instance, and the proposed per-receiver setting could not be honored at all.

`ITransportCustomization.CreateTransportInfrastructure` gains an explicit concurrency argument, and `AuditIngestion` derives its channel bound and batch size from that value rather than from the shared singleton. This changes a project shared with the audit executable, so it runs the audit suite. Consider folding it into PR #5800 while that is still open.

### Commands

Add `--audit-ingestion-only` to the primary command-line parser and to `Help.txt`.

Before EF audit persistence exists, the command is present but fails with a clear message that the selected persistence does not support audit ingestion. It also fails for RavenDB, and it fails when combined with `--error-ingestion-only`.

The normal primary must not activate audit ingestion until a persister advertises audit support, so the groundwork merges without changing existing behavior.

The setup command provisions the audit queue and optional audit forwarding queue for audit-capable primaries, through the existing `IComponentInstallationContext.CreateQueue` mechanism. The deployment or update setup path owns all infrastructure changes, including database schema migrations, queue creation, and body storage provisioning. Ingestion-only workers do not run installers or perform any setup or upgrade work.

## Scale-out rules

Per-message operations run on every normal or ingestion-only receiver and must be safe with concurrent writers:

- Audit message and Saga snapshot inserts.
- Known endpoint upserts.
- Failed audit import storage.
- Body storage writes.
- Audit forwarding.
- Retry acknowledgement dispatch.

Only the normal primary runs singleton work:

- Failed-audit reimport commands.
- API hosting and remote aggregation.
- Licensing and throughput ownership.
- Email notifications.
- Event and integration dispatch polling.
- Retention, which lives inside the persister and is gated by `RunRetentionSweep`.

### Idempotency

The earlier draft asserted idempotency as an acceptance criterion. The current code does not have it, so it is a requirement with named keys.

- `AuditIngestionFaultPolicy` sets `FailedAuditImport.Id = Guid.NewGuid()`. With competing consumers plus immediate retries, one poison message writes a new row per attempt per worker, the custom check fires permanently, and `--import-failed-audits` reprocesses duplicates. Replace it with a deterministic key plus a native-id fallback, modelled on `FailedErrorImport.DeriveKey`.
- `ProcessedMessage.Id` is `ProcessedMessages-{processingStartedTicks}-{ProcessingId()}`, and `ProcessingId()` returns a fresh `Guid` whenever message id, processing endpoint or processing-started headers are missing. State the deduplication key for audit rows, including that degenerate case.
- `ProcessingEndpointName()` throws for headers it cannot resolve. The per-message failure path already handles that, and the failed-import key must not depend on it.

### Ingestion-only component list

PR #5801 registers `EventLog`, `ExternalIntegrations`, `Recoverability`, `HeartbeatMonitoring` and `CustomChecks` in `--error-ingestion-only`, with the reasoning that which node ingests a given message is arbitrary, so nodes behaving differently makes derived data a coin flip per message.

The audit ingestion-only host registers:

| Component | Reason |
| --- | --- |
| `HeartbeatMonitoring` | `DetectNewEndpointsFromAuditImportsEnricher` asks `EndpointInstanceMonitoring.IsNewInstance`, which must be warmed from persistence. Without it every audited message writes a known-endpoint upsert. |
| `CustomChecks` | A stuck worker must report somewhere. Without it, an ingestion failure on a worker is invisible. |

It does not register `Hosting`, which claims the instance queue, or `Licensing`, which would count throughput once per node. `EventLog` and `ExternalIntegrations` are not required because audit ingestion raises no domain events and no integration events. State that explicitly in the composition test so a future registration forces a decision.

## Observability, health, and packaging

- The copied `IngestionMetrics` keeps its OpenTelemetry implementation. `ServiceControl.csproj` gains `OpenTelemetry.Exporter.Console`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` and `OpenTelemetry.Extensions.Hosting`, and the primary gains an `OtlpEndpointUrl` setting wired the same way the audit host wires it. The meter is renamed to `Particular.ServiceControl`.
- The copied custom checks are renamed so they do not collide with the standalone audit instance reporting the same names to the same primary through `ReportCustomChecksTo`. `FailedAuditImportCustomCheck` also drops its `ServiceControl.Audit Health` category, which would otherwise appear on a process that is not the audit instance. They register through `services.AddCustomCheck<T>()`, not `configuration.AddCustomCheck`.
- `/health` and `/health/ready` follow the error-ingestion-only conventions from PR #5803, mapped as minimal API routes.
- `AuditIngestor.VerifyCanReachForwardingAddress` dispatches an empty probe message to the log queue on every infrastructure start. With N workers restarting under the watchdog, N probes accumulate. Decide whether ingestion-only workers verify forwarding at all, and what happens when the log queue does not exist because setup has not run.
- Confirm the copied runtime ships inside the existing primary artifact without adding another assembly, and that the new package references do not break `ServiceControlInstaller.Packaging.UnitTests`.

## Work plan

### 1. Establish the baseline

- Land or rebase on PRs #5800, #5801 and #5803. None of them is merged, and `RunRetentionSweep`, `--error-ingestion-only`, `/health` and dispatcher-as-argument do not exist without them.
- Record the exact service composition of the existing standalone audit host.
- Write the characterization tests listed below. The existing audit acceptance suite is dominated by CORS, HTTPS, forwarded headers and OIDC, and has no coverage at all for forwarding, retention, queue setup or shutdown ordering.

Characterization tests to write, not to assume:

| Behavior | Why it matters |
| --- | --- |
| Audit forwarding to the log queue, including the startup probe | Nothing covers forwarding today. |
| Failed audit import round trip through `--import-failed-audits` | Establishes the duplicate-row behavior before the key changes. |
| `EndpointDetected` domain event on audit-discovered endpoints | Pins the observable difference the enricher change introduces. |
| Retry acknowledgement dispatch and recording end to end | Pins the behavior the plan deliberately leaves alone. |
| Audit shutdown with a non-empty channel and forwarding on | Pins PR #5800's fix. |
| Audit queue provisioning through setup | Nothing covers it. |
| `/api/connection` payload with and without an audit remote | Pins what the Platform Connector plugin receives. |

### 2. Persistence contracts, capability model, and a test persister

- Add the audit child to the primary ingestion unit of work.
- Add the failed-import, audit count, saga history and capability contracts. Extend `IMessagesViewDataStore` rather than duplicating it.
- Add `SupportsAuditIngestion` to `ServiceControl.Persistence.PersistenceManifest` and to the two EF `persistence.manifest` files.
- Add a test persister for the primary that advertises audit support, since none exists. There is no in-memory primary persister today: the only manifests are RavenDB, EFCore.SqlServer and EFCore.PostgreSql, and `ServiceControl.Persistence.Tests.InMemory` is a test context, not a persister. Either a fake registered in the acceptance-test host or a new in-memory persister is acceptable, but the plan needs one of them by name.
- Do not move the standalone RavenDB audit implementation onto the new contracts.
- Do not add EF entities, mappings, migrations, or SQL.

No runtime behavior change.

### 3. Copy and adapt the audit runtime

Merged into one pull request, because a copy nothing constructs is reviewable but not verifiable.

- Copy the receiving, parsing, enrichment, fault handling, forwarding orchestration, metrics and readiness behavior into `ServiceControl`, changing namespaces and dependencies so the copy is owned by the primary project.
- Do not copy standalone API composition, RavenDB settings, installers, maintenance mode, or persistence loading.
- Replace endpoint-registration commands with direct monitoring unit-of-work calls, and remove `IMessageSession` and the `ICommand` overload from the copy.
- Pass the low-level dispatcher into operations that perform transport output.
- Add the per-receiver concurrency argument to `CreateTransportInfrastructure`.
- Add the OpenTelemetry references and `OtlpEndpointUrl`, and rename the meter.
- Register the copy against the test persister from step 2, and add a composition test that actually starts it. This is what makes the pull request verifiable.
- Leave the source implementation in `ServiceControl.Audit` behaviorally unchanged, and run its acceptance suite.

### 4. Settings and the fail-fast command

- Add the runtime settings from the table above. No installer changes.
- Add `--audit-ingestion-only` parsing, `Help.txt`, and the three fail-fast paths: unsupported persistence, RavenDB, and combination with `--error-ingestion-only`.
- Add the audit queue and audit log queue to the setup component installation context.

### 5. Normal primary audit composition

- Add an audit component to the primary component model.
- Register the full audit capability in the normal primary profile when the persister advertises audit support.
- Keep all audit capabilities except the receiver when normal-primary ingestion is disabled.
- Add exact hosted-service composition tests against the test persister.

### 6. Local query composition

- Convert `GetAuditCountsForEndpointApi` and `GetSagaByIdApi` to local-capable APIs.
- Implement the precedence, paging and counting rules against the test persister.
- Fold audit body resolution into `IBodyStorage` with the stated arbitration order and the prefixed keyspace.
- Keep existing routes and authorization policies.

### 7. Platform connection details, saga forwarding, and licensing

- Add the local audit platform connection details provider.
- Verify `SagaUpdatedHandler` resolves the local audit queue.
- Add the local audit source to `IAuditQuery` and `AuditThroughputCollectorHostedService`, so the local audit queues are recognized as platform endpoints and audit service metadata is populated.

### 8. Ingestion-only composition

- Add the dedicated host builder path.
- Register the component list from "Ingestion-only component list", and assert the exact hosted-service set.
- Add `/health` and `/health/ready`.
- Reject unsupported persistence, RavenDB, mode combination, and filesystem body storage without the shared-path assertion.

### 9. Packaging and documentation

- Confirm the copied runtime ships in the existing primary artifact and that the new package references pass the packaging tests.
- Keep the standalone RavenDB audit artifact and manifests unchanged.
- Document the normal, disabled-ingestion, and ingestion-only deployment modes.
- Document queue ownership, body storage requirements, health endpoints, the setting collisions, and unsupported combinations.

### 10. Reassess reuse after delivery

- Compare the stable primary and RavenDB implementations.
- Identify code that remains behaviorally identical and has compatible dependencies.
- Extract shared code only when the resulting boundary is simpler than maintaining the copies.
- Treat a shared hosting project as an optional follow-up, not a prerequisite for EF audit support.

## Pull request sequence

1. Persistence contracts, capability model, and the primary test persister. No runtime behavior change.
2. Copy and adapt the audit runtime, registered against the test persister and exercised by a composition test.
3. Settings and the fail-fast command-line mode. No installer changes.
4. Local query composition, including audit counts, saga history, and body arbitration.
5. Platform connection details, saga forwarding, and the local licensing throughput source.
6. Ingestion-only host composition and health checks.
7. Packaging, documentation, and architecture tests.

Each pull request leaves the full RavenDB audit suite passing, states why any change to the four shared projects is safe, and avoids activating unsupported EF audit behavior.

## Validation and acceptance criteria

### Existing behavior

- The standalone RavenDB audit executable has no observable behavior or configuration changes, verified by its acceptance suite on every pull request.
- RavenDB primary instances continue querying configured audit remotes.
- Existing SQL Server and PostgreSQL primary instances behave as before until their persistence advertises audit support.
- No file under `ServiceControlInstaller.Engine`, `ServiceControl.Config` or `ServiceControl.Management.PowerShell` is modified. RavenDB install, upgrade and settings-apply behavior is therefore unchanged by construction, and needs no new test to prove it.

### Contracts

- Audit ingestion can persist an audit message and Saga snapshot without depending on RavenDB types.
- Endpoint discovery uses the existing monitoring persistence contract and writes the same `KnownEndpoints` rows as the error path.
- Persistence capability checks produce deterministic startup validation, driven by the manifest rather than by resolving optional services.

### Normal primary composition

- Audit ingestion can be enabled or disabled independently of the remaining audit capabilities.
- Existing API routes resolve using local audit query contracts, under their existing policies.
- Local results can be combined with remotes.
- Precedence, paging and counting rules hold for a merged local result. A message that both failed and was audited appears once, shows as failed, and is counted once.
- `/api/connection` advertises the local audit queue, and a misdirected saga audit message is forwarded rather than failed.
- The local audit queue and audit log queue are recognized as platform endpoints by throughput collection.

### Ingestion-only composition

- Every registered hosted service resolves without an NServiceBus endpoint.
- `IMessageSession` is absent.
- Installer services such as `IDatabaseMigrator` and body storage provisioners are absent.
- Starting an ingestion-only worker never changes the database schema, creates queues, or provisions external storage.
- The exact hosted-service set is asserted so future registrations force an explicit scale-out decision.
- RavenDB, persistence without audit support, combination with `--error-ingestion-only`, and unasserted filesystem body storage each fail clearly at startup.
- Liveness and readiness endpoints return JSON responses.

### Scale-out semantics

- Concurrent workers can process the same audit queue using competing consumers.
- A redelivered audit message produces one row, not one per delivery, including when the processing-started header is absent.
- A poison audit message produces one failed-import row, not one per attempt per worker.
- Shutdown drains accepted messages before transport infrastructure is torn down.
- Forwarded messages are not duplicated during graceful shutdown.
- Audit ingestion concurrency is independent of error ingestion concurrency.

## Constraints for the later EF implementation

The earlier audit EF spike remains useful evidence, especially for retention, full-text search, and body storage. The later implementation should revisit it using the current primary EF architecture as the authority.

Important retained findings are:

- PostgreSQL can use range partitioning and partition removal for retention.
- SQL Server needs a provider-specific strategy because full-text indexes prevent equivalent partition truncation.
- Retention requires distributed locking. This belongs to the EF implementation, not to host composition. Audit retention extends `RetentionSweeper`, which already deletes bodies through `IBodyStoragePersistence` and is gated by `RunRetentionSweep`.
- Cleanup capacity must be proportional to ingestion rate. A fixed delete batch can fall behind.
- Full-text search remains provider-specific, and is governed by the existing `EnableFullTextSearchOnBodies` setting shared with error bodies.
- Body storage lifecycle must align with audit retention, and must sweep the prefixed audit keyspace as well as the error keyspace.
- Stable lock ordering and provider-specific upsert behavior are requirements, following the `INSERT ... ON CONFLICT` and `MERGE WITH (HOLDLOCK)` patterns the error batch writer already uses.
- `--audit-ingestion-only` must never apply EF migrations, modify the database schema, create queues, or provision body storage. It assumes the deployment or update setup path has already prepared all required infrastructure.

Unlike the spike, the implementation uses one primary EF model and migration stream, one shared known-endpoint table, and no audit-to-primary endpoint reconciliation process.

## Open items

These do not block the groundwork, but they need answers before or during the EF implementation.

1. Should `SagaUpdatedHandler` forward a misdirected saga audit message to the local audit queue, or hand the snapshot straight to the audit unit of work? Forwarding preserves today's behavior and its warning. Direct handling removes a broker round trip.
2. Should the audit path raise the `EndpointDetected` domain event that the command path raises today? The characterization test in step 1 answers what is currently observable.
3. Do ingestion-only workers verify the forwarding address at startup? N workers restarting under the watchdog put N probe messages in the log queue.
4. What is the retention lock scope? One lock for the whole sweeper, or separate error and audit locks so a slow audit sweep does not block error retention.
5. What is the default audit retention period when `ServiceControl/AuditRetentionPeriod` is null? The audit instance defaults to 30 days while SCMU and the Dockerfile default to 7.
6. Should a later release combine the two ingestion-only modes, or replace both flags with a single `--ingestion-only` governed by `IngestErrorMessages` and `IngestAuditMessages`?
7. Handed to the SCMU and PowerShell workstream for EF storage types, not answered here: how are the audit settings surfaced for a Windows primary, and what fixes the `RemoveIfRetired` trap described in "Installer: out of scope, with one handoff"? Nothing in this plan is blocked on it, because SCMU cannot create an EF instance today.

## Reference pull requests

- [Audit EF spike: #5318](https://github.com/Particular/ServiceControl/pull/5318)
- [Scale out error ingestion 1/3, dispatcher ownership: #5800](https://github.com/Particular/ServiceControl/pull/5800)
- [Scale out error ingestion 2/3, ingestion-only host: #5801](https://github.com/Particular/ServiceControl/pull/5801)
- [Scale out error ingestion 3/3, health endpoints: #5803](https://github.com/Particular/ServiceControl/pull/5803)
