# Usage report contents

The primary instance builds the usage report when a user downloads it from ServicePulse (`GET api/licensing/report/file`). The customer sends the signed file to Particular. This page lists every key in the report's `EnvironmentData`, with its values and its source. The page also lists every setting that is deliberately left out, with the reason. The reasoning behind these rules is in [the coverage decision](decisions/2026-10-01-usage-report-feature-coverage.md). How usage data itself is collected is covered in [throughput-collection.md](throughput-collection.md).

## Rules for a value

The report names the licensee in `CustomerName`. Its queue list carries queue names and, on the SQL Server and PostgreSQL transports, a `Scope` of `[Database].[Schema]`. These are masked only when the customer configures report masks. Otherwise they are in clear text. `EnvironmentData` must add nothing that identifies the customer's infrastructure, people or data. That rules out host names, URLs, connection strings, paths, queue and endpoint names, addresses, user names, client ids and secrets. It also rules out a hash or prefix of any of them.

`EnvironmentData` also says nothing about security configuration: how the customer authenticates callers or connections, encrypts traffic, validates certificates or tokens, or restricts access.

A value is always one of these:

- `Enabled` or `Disabled` for a switch.
- A fixed enum member for a mode.
- A count or a number.
- A version.

Tuning numbers report `Default` when the setting is absent from configuration. Otherwise they report the configured number in invariant culture, in the unit the key name ends with. Installers write some settings explicitly, for example `ShutdownTimeout`. Those report the installer's value, not `Default`.

A value derived from an identifying setting goes through a fixed classifier or becomes a count. `DatabaseHostClassifier` is the model for a classifier.

Three values have a fixed meaning on every key:

- `NotApplicable` means the feature the key describes is off or absent.
- `Unknown` means the instance cannot tell.
- `ReadFailed` means reading the value threw. The report is still generated.

Audit keys combine every live audit instance. Numbers report the largest value. An enum reports `Mixed` when the instances differ.

A key name is permanent. Analysis compares reports across versions, and a rename forces it to read both spellings.

## Adding or changing a setting

A pull request that adds a setting, a runtime choice, or a new mode of an existing setting does one of two things:

1. It adds a key. That means an `IEnvironmentDataProvider` in the component that owns the setting, a row in the tables below, and an entry in `ExpectedKeys` in `When_reporting_the_environment` when the key is emitted on every storage. A transport adds its key to `GetEnvironmentData` on its transport customization instead of an `IEnvironmentDataProvider`.
2. It adds a row to [Not reported](#not-reported) with one of the four reasons.

A pull request that does neither gets a review finding.

## Reported keys

`Status` is the first release that emits the key, `Unreleased` for a key on master that has not shipped, or `Planned` for a key this page commits to. Planned names and value sets are final at implementation review.

### Versions and instance counts

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `ServiceControlVersion` | version | the running primary | 5.4.0 |
| `ServicePulseVersion` | version | the `spVersion` query parameter ServicePulse sends | 5.4.0 |
| `AuditEnabled` | `True`, `False` | any audit throughput in the report window | 5.4.0 |
| `MonitoringEnabled` | `True`, `False` | any monitoring throughput in the report window | 5.4.0 |
| `RabbitMQVersion` | version | the broker throughput query on RabbitMQ | 5.4.0 |
| `SqlVersion` | major version, or `AzureSql` on Azure SQL Database | the broker throughput query on SQL Server and PostgreSQL | 5.4.0 |
| `Audit.ConfiguredInstances` | count | entries in `ServiceControl/RemoteInstances` | Unreleased |
| `Audit.LiveInstances` | count | remotes that answer as an audit instance | Unreleased |

`MonitoringEnabled` does not mean a monitoring instance is installed. It means a monitoring instance delivered non-zero throughput to this primary on at least one day of the report window, which covers the last 14 months and excludes today. It is `False` when monitoring is installed but no endpoint sends metrics, or when the throughput queue names do not match. It stays `True` for up to 14 months after monitoring is removed.

Releases up to 6.21.0 report `SqlVersion` as the full text of `@@VERSION` on SQL Server or `version()` on PostgreSQL. That text includes the patch level, edition and operating system build. Later releases report only the major version. Azure SQL Database always reports version 12, which cannot be compared with SQL Server's version numbers. On Azure SQL Database, `SqlVersion` is `AzureSql` instead.

### Host

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `Host.Model` | `Container`, `WindowsService`, `Console` | the process | 6.20.0 |
| `Host.Orchestrator` | `Kubernetes`, `None` | `KUBERNETES_SERVICE_HOST` | 6.20.0 |
| `Host.OSPlatform` | `Windows`, `Linux`, `macOS`, `Unknown` | the runtime | 6.20.0 |
| `Host.OSVersion` | major.minor | the runtime | 6.20.0 |
| `Host.Architecture` | the process architecture | the runtime | 6.20.0 |
| `Host.RuntimeVersion` | version | the runtime | 6.20.0 |
| `Host.ProcessorCount` | count | the runtime | 6.20.0 |
| `Host.AvailableMemoryGB` | number | the GC memory limit, which honours a container limit | 6.20.0 |
| `Host.VirtualDirectory` | `None`, `Configured` | `ServiceControl/VirtualDirectory` | Planned |
| `Host.ShutdownTimeoutSeconds` | `Default` or number | `ServiceControl/ShutdownTimeout` | Planned |

### Storage

6.20.0 and 6.21.0 emit these keys as `Persistence.*`. The rename to `Storage.*` is unreleased.

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `Storage.Type` | `RavenDB`, `SQLServer`, `PostgreSQL` | the persister | 6.20.0 |
| `Storage.RavenServer` | `Embedded`, `External` | RavenDB only | 6.20.0 |
| `Storage.Hosting` | a fixed hosting class from `DatabaseHostClassifier` | the database host, and the engine's own answer where it gives one | 6.20.0 |
| `Storage.HostingSource` | how `Storage.Hosting` was decided | the persister | 6.20.0 |
| `Storage.ServerVersion` | major version on SQL Server and PostgreSQL, major.minor on RavenDB. Always 12 on Azure SQL Database, so read it with `Storage.Hosting` | the engine | 6.20.0 |
| `Storage.FullTextSearch` | `Enabled`, `Disabled` | `EnableFullTextSearchOnBodies` | 6.20.0 |
| `Storage.BodyStorage.Type` | `RavenAttachments`, `FileSystem`, `AzureBlob`, `S3` | the persister | 6.20.0 |
| `Limits.MaxBodySizeToStore` | bytes | `MaxBodySizeToStore`, SQL Server and PostgreSQL only | 6.20.0 |
| `Storage.Schema` | `Default`, `Custom` | `Database/Schema`, SQL Server and PostgreSQL only | Planned |
| `Storage.LogLevel` | `None`, `Information`, `Operations` | `RavenDBLogLevel`, RavenDB only | Planned |
| `Storage.CommandTimeoutSeconds` | `Default` or number | `Database/CommandTimeout`, SQL Server and PostgreSQL only | Planned |
| `Storage.QueryTimeoutSeconds` | `Default` or number | `QueryTimeoutInSeconds` | Planned |
| `Storage.SubscriptionCacheSeconds` | `Default` or number | `SubscriptionCacheDuration`, SQL Server and PostgreSQL only | Planned |
| `Storage.BodyStorage.MinCompressionBytes` | `Default` or number | `MessageBody/MinCompressionSize`, SQL Server and PostgreSQL only | Planned |
| `Storage.FreeSpaceThresholdPercent` | `Default` or number. `NotApplicable` on SQL Server and PostgreSQL when body storage is not the file system | `DataSpaceRemainingThreshold` on RavenDB, `MessageBody/FileSystem/DataSpaceRemainingThreshold` on file system body storage | Planned |
| `Storage.MinimumFreeSpaceForIngestionPercent` | `Default` or number | `MinimumStorageLeftRequiredForIngestion`, RavenDB only | Planned |
| `Storage.ExpirationIntervalSeconds` | `Default` or number | `ExpirationProcessTimerInSeconds`, RavenDB only | Planned |

### Transport

The report's top-level `MessageTransport` carries the broker family name (`RabbitMQ`) whenever the transport has a broker throughput query. The RabbitMQ queue type and routing topology are lost there, so `Transport.Type` carries the full manifest name. `MessageTransport` stays as it is for analysis that already reads it.

The primary's `TransportEnvironmentDataProvider` emits `Transport.Type` and adds the keys the transport returns from `ITransportCustomization.GetEnvironmentData`. Keys under `Transport.<Name>.*` are emitted only by that transport.

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `Transport.Type` | the manifest name, for example `RabbitMQ.QuorumConventionalRouting` | `ServiceControl/TransportType`, resolved through the manifest | Planned |
| `Transport.AzureServiceBus.Topology` | `TopicPerEvent`, `Migration`, `Custom` | `TopicName` in the connection string, then `ServiceControl.Transport.ASBS/Topology` | Planned |
| `Transport.AzureServiceBus.Partitioning` | `Enabled`, `Disabled` | `EnablePartitioning` | Planned |
| `Transport.AzureServiceBus.WebSockets` | `Enabled`, `Disabled` | `TransportType=AmqpWebSockets` | Planned |
| `Transport.AzureServiceBus.HierarchyNamespace` | `None`, `Configured` | `HierarchyNamespace` | Planned |
| `Transport.AmazonSQS.NamePrefixes` | `None`, `Queue`, `Topic`, `QueueAndTopic` | `QueueNamePrefix`, `TopicNamePrefix` | Planned |
| `Transport.AmazonSQS.LargeMessageBucket` | `None`, `Configured` | `S3BucketForLargeMessages` | Planned |
| `Transport.AmazonSQS.MessageWrapping` | `Enabled`, `Disabled` | `DoNotWrapOutgoingMessages` | Planned |
| `Transport.AmazonSQS.ReservedBytesInMessageSize` | `Default` or number | `ReservedBytesInMessageSize` | Planned |
| `Transport.RabbitMQ.DeliveryLimitValidation` | `Enabled`, `Disabled` | `ValidateDeliveryLimits` | Planned |
| `Transport.RabbitMQ.ManagementApi` | `Default`, `Configured` | `ManagementApiUrl` | Planned |
| `Transport.SQLServer.QueueSchema` | `Default`, `Custom` | `Queue Schema` in the connection string | Planned |
| `Transport.SQLServer.SubscriptionsTable` | `Default`, `Custom` | `Subscriptions Table` in the connection string | Planned |
| `Transport.PostgreSQL.QueueSchema` | `Default`, `Custom` | `Queue Schema` in the connection string | Planned |
| `Transport.PostgreSQL.SubscriptionsTable` | `Default`, `Custom` | `Subscriptions Table` in the connection string | Planned |

### Features

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `Features.IntegratedServicePulse` | `Enabled`, `Disabled` | `ServiceControl/EnableIntegratedServicePulse` | 6.13.0 |
| `Features.MessageEditing` | `Enabled`, `Disabled` | `ServiceControl/AllowMessageEditing` | 6.20.0 |
| `Features.ExternalIntegrationsPublishing` | `Enabled`, `Disabled` | `ServiceControl/DisableExternalIntegrationsPublishing`, inverted | 6.20.0 |
| `Features.ForwardErrorMessages` | `Enabled`, `Disabled` | `ServiceControl/ForwardErrorMessages` | 6.20.0 |
| `Features.EmailNotifications` | `Enabled`, `Disabled`, `NotConfigured` | the stored email settings | 6.20.0 |
| `Features.ErrorIngestion` | `Enabled`, `Disabled` | `ServiceControl/IngestErrorMessages` | Planned |
| `Features.ConfigurationValidation` | `Enabled`, `Disabled` | `ServiceControl/ValidateConfig` | Planned |
| `Limits.ExternalIntegrationsBatchSize` | `Default` or number | `ExternalIntegrationsDispatchingBatchSize` | Planned |

### Integrated ServicePulse

These keys are `NotApplicable` when `Features.IntegratedServicePulse` is `Disabled`. The values come from the environment variables the integrated ServicePulse reads.

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `ServicePulse.MonitoringUrl` | `Default`, `Custom`, `Disabled`, `NotApplicable` | `MONITORING_URL`, or the legacy `MONITORING_URLS`. `!` means disabled | Planned |
| `ServicePulse.DefaultRoute` | `Default`, `Custom`, `NotApplicable` | `DEFAULT_ROUTE` | Planned |
| `ServicePulse.ShowPendingRetry` | `Enabled`, `Disabled`, `NotApplicable` | `SHOW_PENDING_RETRY` | Planned |

### Error ingestion

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `Ingestion.Error.MaxConcurrency` | `Default` or number | `ServiceControl/MaximumConcurrencyLevel` | Planned |
| `Ingestion.Error.BatchSize` | `Default` or number | `ServiceControl/ErrorIngestionBatchSize` | Planned |
| `Ingestion.Error.MaxParallelWriters` | `Default` or number | `ServiceControl/ErrorIngestionMaxParallelWriters` | Planned |
| `Ingestion.Error.BatchTimeoutMs` | `Default` or number | `ServiceControl/ErrorIngestionBatchTimeout` | Planned |
| `Ingestion.Error.RestartAfterFailureSeconds` | `Default` or number | `ServiceControl/TimeToRestartErrorIngestionAfterFailure` | Planned |

### Retention

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `Retention.ErrorHours` | whole hours | `ErrorRetentionPeriod` | 6.20.0 |
| `Retention.EventsHours` | whole hours | `EventRetentionPeriod` | 6.20.0 |

`Retention.EventsHours` reports `ServiceControl/EventRetentionPeriod`, the spelling the instance validates and the public documentation uses. Both persisters enforce `ServiceControl/EventsRetentionPeriod` instead, with a 14 day default. Until the two are reconciled, the reported value can differ from the retention the storage applies.

### Heartbeats, recoverability and licensing

Most of these are choices users make in ServicePulse, or the heartbeat state those choices apply to. `Heartbeats.GracePeriodSeconds` and `Recoverability.RetryHistoryDepth` are configuration settings. Every value is read when the report is built.

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `Heartbeats.TrackInstancesDefault` | `Enabled`, `Disabled` | the stored default endpoint settings row. Falls back to `ServiceControl/TrackInstancesInitialValue` when no row is stored | Planned |
| `Heartbeats.TrackInstancesOverrides` | count | stored endpoint settings rows whose value differs from the default | Planned |
| `Heartbeats.KnownInstances` | count | `IEndpointInstanceMonitoring.GetEndpoints` | Planned |
| `Heartbeats.MonitoredInstances` | count | the same, where `Monitored` is true | Planned |
| `Heartbeats.GracePeriodSeconds` | `Default` or number | `ServiceControl/HeartbeatGracePeriod` | Planned |
| `Recoverability.Redirects` | count | `IMessageRedirectsDataStore.GetRedirects` | Planned |
| `Recoverability.RetryHistoryDepth` | `Default` or number | `ServiceControl/RetryHistoryDepth` | Planned |
| `Licensing.ReportMasks` | count | `ILicensingDataStore.GetReportMasks` | Planned |

An instance becomes monitored on its first heartbeat. An instance first seen in an ingested message starts unmonitored. So `KnownInstances` minus `MonitoredInstances` counts instances that a user stopped monitoring together with instances that have never sent a heartbeat. Storage cannot separate the two.

### Logging and telemetry

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `Logging.Providers` | the active providers from `NLog`, `Seq`, `Otlp`, comma separated in that order | `ServiceControl/LoggingProviders`. `NLog` when unset | Planned |
| `Logging.Level` | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None` | `ServiceControl/LogLevel` | Planned |
| `Telemetry.OtlpMetrics` | `Enabled`, `Disabled` | whether `OTEL_EXPORTER_OTLP_ENDPOINT` is set | Planned |

### Audit instances

These come from the `GET /api/configuration` response of each live audit instance. The primary already fetches that response once a day. No change to the audit instance is needed.

| Key | Values | Source | Status |
| --- | --- | --- | --- |
| `Audit.RetentionHours` | whole hours, largest across instances | `data_retention.audit_retention_period` | Planned |
| `Audit.Features.ForwardAuditMessages` | `Enabled`, `Disabled`, `Mixed` | `transport.forward_audit_messages` | Planned |
| `Audit.Limits.MaxBodySizeToStore` | bytes, largest across instances | `performance_tunning.max_body_size_to_store` | Planned |
| `Audit.Logging.Level` | the `Logging.Level` values, or `Mixed` | `host.logging.logging_level` | Planned |
| `Audit.Storage.Type` | `RavenDB`, `SQLServer`, `PostgreSQL`, `Mixed` | `persistence.persistence_type`, normalised because the raw value can be a legacy type name | Planned |

## Not reported

Every setting below is left out for one of four reasons:

- Identifies: the setting is a name, address, path or secret, or a hash or prefix of one.
- Not a choice: the setting is a test hook, a dead setting, an action, or a mode in which the instance cannot build a report.
- Security configuration: the setting describes how the instance authenticates callers or connections, encrypts traffic, validates certificates or tokens, or restricts access.
- Out of scope: the coverage decision leaves it out.

A setting that identifies and is also security configuration is listed as Identifies.

| Setting | Reason | Notes |
| --- | --- | --- |
| `ServiceControl/InstanceName`, `InternalQueueName` | Identifies | |
| `ServiceBus/ErrorQueue`, `ServiceBus/ErrorLogQueue` | Identifies | |
| `ServiceControl/Hostname`, `ServiceControl/Port` | Identifies | Together they form the instance's address. |
| `ServiceControl/VirtualDirectory` value | Identifies | Reported as `Host.VirtualDirectory`. |
| Transport connection string | Identifies | Some of its options are reported under Transport as fixed values. Its authentication, encryption and certificate validation options are security configuration. |
| `ServiceControl/RemoteInstances` | Identifies | Counted by `Audit.ConfiguredInstances` and `Audit.LiveInstances`. |
| `Database/ConnectionString`, `RavenDB/ConnectionString`, `RavenDB/DatabaseName`, `DbPath` | Identifies | The hosting class and server version are reported under Storage. |
| `RavenDB/ClientCertificatePath`, `ClientCertificateBase64`, `ClientCertificatePassword` | Identifies | |
| `Database/Schema` value | Identifies | Reported as `Storage.Schema`. |
| `MessageBody/*` path, container, bucket, key prefix, region, service URL, credentials, managed identity client id, authority host | Identifies | The type is reported as `Storage.BodyStorage.Type`. |
| `LogPath`, `SeqAddress`, the `OTEL_EXPORTER_OTLP_ENDPOINT` value | Identifies | The providers and OTLP use are reported. |
| `Https.CertificatePath`, `Https.CertificatePassword` | Identifies | |
| `Authentication.Authority`, `Audience`, `ServicePulse.ClientId`, `ServicePulse.ApiScopes`, `ServicePulse.Authority`, claim names | Identifies | |
| `Cors.AllowedOrigins`, `ForwardedHeaders.KnownProxies`, `ForwardedHeaders.KnownNetworks` | Identifies | |
| Email server, sender, recipients, account and password | Identifies | Only whether email notifications are on is reported, as `Features.EmailNotifications`. |
| `ServiceControl/NotificationsFilter` check ids | Identifies | |
| Report mask strings, redirect addresses, endpoint names, licensed endpoint details | Identifies | Masks and redirects are reported as counts. |
| `MONITORING_URL`, `DEFAULT_ROUTE` values, `SERVICECONTROL_URL` | Identifies | The integrated ServicePulse keys report `Default` or `Custom`. |
| `Authentication.Enabled`, `RoleBasedAuthorizationEnabled`, `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`, `RequireHttpsMetadata`, `ServicePulse.OfflineAccessScopeEnabled`, and whether claim names differ from the defaults | Security configuration | 6.20.0 and 6.21.0 report the first two as `Security.Authentication` and `Security.RoleBasedAuthorization`. |
| `Https.Enabled`, `RedirectHttpToHttps`, `EnableHsts`, `HstsMaxAgeSeconds`, `HstsIncludeSubDomains`, `Https.Port` | Security configuration | 6.20.0 and 6.21.0 report `Https.Enabled` as `Security.Https`. |
| `Cors.AllowAnyOrigin` and whether origins are restricted | Security configuration | |
| `ForwardedHeaders.Enabled`, `TrustAllProxies` and whether proxies are restricted | Security configuration | |
| Transport authentication, encryption and certificate validation options in the connection string, for example RabbitMQ `UseExternalAuthMechanism` and `DisableRemoteCertificateValidation` | Security configuration | |
| Database authentication, encryption and certificate validation options, for example SQL Server `Encrypt` and `TrustServerCertificate` or PostgreSQL `SSL Mode`, and whether a RavenDB client certificate is used | Security configuration | |
| Body storage authentication mode | Security configuration | 6.20.0 and 6.21.0 report it as `Persistence.BodyStorage.Auth`. |
| Email notification TLS and SMTP authentication | Security configuration | |
| Audit instance security settings | Security configuration | `GET /api/configuration` does not return them. |
| `ServiceControl/PrintMetrics` | Not a choice | Nothing reads it. |
| `ServiceControl/AuditRetentionPeriod` on the primary | Not a choice | It is displayed and returned by `api/configuration`, but nothing acts on it. |
| `EmailDropFolder`, `MessageFilter` | Not a choice | Acceptance tests only. |
| `RunCleanupBundle`, `DisableHealthChecks` | Not a choice | Set by commands, never by configuration. |
| `--error-ingestion-only` | Not a choice | A worker mode. Workers do not build reports. |
| RavenDB `MaintenanceMode` | Not a choice | An instance in maintenance mode serves no API, so it cannot build a report. |
| `ASPNETCORE_ENVIRONMENT=Development` | Not a choice | A development mode. |
| `DOTNET_RUNNING_IN_CONTAINER` | Not a choice | Already covered by `Host.Model`. |
| Retry, archive, unarchive, resolve, edit and group comment operations | Not a choice | These are actions. Their on/off switch, where one exists, is reported. |
| Custom checks | Not a choice | Endpoints report them and ServiceControl stores them. Deleting one does not keep a muted state. |
| Email notification details: whether a filter is set, port, number of recipients and mail provider | Out of scope | Only `Features.EmailNotifications` is reported. |
| Whether licensed endpoint details are uploaded, and whether they match the license | Out of scope | They only apply to Endpoint Size licenses with endpoint metadata. |
| Transport `QueueLengthQueryDelayInterval`, `QueueLengthQueryMaxDelayInterval` | Out of scope | Only the monitoring instance reads them. |
| Every monitoring instance setting | Out of scope | |
| Audit instance settings that `GET /api/configuration` does not return | Out of scope | Includes `IngestAuditMessages`, full-text search, embedded or external RavenDB, logging providers, OTLP, ingestion and RavenDB tuning, `ServiceControlQueueAddress`, `VirtualDirectory` and maintenance mode. Audit security settings are listed under Security configuration. |
