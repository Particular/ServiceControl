# Usage report covers every customer choice

- Date: 2026-10-01
- Revised: 2026-10-06. Security configuration is no longer reported, and Particular deletes the security values it has already received. `SqlVersion` reports only the major version. Email notification details and licensed endpoint details are now out of scope.
- Status: Accepted
- Implementation: [#5945](https://github.com/Particular/ServiceControl/pull/5945). Link further pull requests here as they open.

## Context

The usage report tells Particular how customers run ServiceControl. The primary instance builds the report when a user downloads it from ServicePulse. The customer then sends the file to Particular. The report's `EnvironmentData` section is a flat dictionary of strings. Since 6.20.0 it has carried about 30 keys describing the host, the storage, security, a handful of features and retention.

Those keys were added one at a time, each when someone needed an answer. A key only appears in reports from the release that adds it. So the first question about a feature always arrives with no data to answer it. The proposal to ingest audit messages in the primary was withdrawn in September 2026, largely for that reason.

An audit on 1 October 2026 compared the report with every setting and runtime choice in the primary, audit and monitoring instances. The report carries about a third of what could be reported. The gaps include:

- Error ingestion turned off, OTLP metrics export, and logging providers and level.
- The RabbitMQ queue type and routing topology. Whenever the transport has a broker throughput query, the report's `MessageTransport` carries only the broker family name.
- The transport connection options that change behaviour, such as the Azure Service Bus topology and the SQL Server queue schema.
- The runtime choices users make in ServicePulse, apart from email notifications being on or off. Heartbeat instance tracking, retry redirects and report masks are all missing.
- Every tuning number, including concurrency, batch sizes, timeouts and thresholds.
- Nearly everything about the audit and monitoring instances.

The audit also found security settings missing from the report, such as CORS, forwarded headers, and the database and transport authentication modes. Whether to report them is part of this decision.

The decision has to respect seven constraints:

- The report names the licensee in `CustomerName`. Its queue list carries queue names and, on the SQL Server and PostgreSQL transports, a `Scope` of `[Database].[Schema]`. These are masked only when the customer configures report masks. Otherwise they are in clear text. `EnvironmentData` must add nothing that identifies the customer's infrastructure, people or data. Since August 2026 every value has been a fixed enum member, a boolean, a count, a number or a version.
- The report is signed, so a customer cannot remove one key and send the rest. Security reviewers in regulated organisations often classify security configuration as confidential. A reviewer who does would block the whole report, and with it the licensing data.
- Licensing has no need to know how a named customer secures its instances.
- Only the primary builds the report. The primary already fetches each audit instance's `GET /api/configuration` once a day. Its only link to the monitoring instance is the throughput message monitoring sends every five minutes, and the primary reads only that message's body.
- `EnvironmentData` accepts new keys without a change to the signed report schema in `Particular.LicensingComponent.Report`. Any other change to the report needs a new version of that package.
- A value that fails to read must not stop the report. The `ReadFailed` value already covers this.
- Analysis compares reports across versions. Renaming a key forces analysis to read both spellings, as the `Persistence.*` to `Storage.*` rename did.

A breach of the store where Particular keeps reports is a real risk, but a secondary one for security settings. The values carry no host, URL or secret. Exploiting any of them needs network access to the instance or to the database, broker or storage it uses, and an attacker with that access can observe the same facts directly.

## Decision

Every setting or runtime choice that changes how ServiceControl behaves gets a key in `EnvironmentData`. The only exception is a setting that [the catalog](../usage-report.md) lists as excluded. An exclusion gives one of four reasons:

- Identifies: the setting is a name, address, path or secret.
- Not a choice: the setting is a test hook, a dead setting, an action, or a mode in which the instance cannot build a report.
- Security configuration: the setting describes how the instance authenticates callers or connections, encrypts traffic, validates certificates or tokens, or restricts access.
- Out of scope: this decision excludes it.

The report carries no security configuration. It says nothing about how the customer authenticates callers or connections, encrypts traffic, validates certificates or tokens, or restricts access. A setting that identifies and is also security configuration is excluded as Identifies.

Values follow fixed shapes:

- A switch reports `Enabled` or `Disabled`. A mode reports a fixed enum member.
- A runtime feature reports a count that shows whether it is in use, for example the number of retry redirects.
- A tuning number reports `Default` when the setting is absent from configuration. Otherwise it reports the configured number, in the unit the key name ends with.
- A value derived from an identifying setting goes through a fixed classifier or becomes a count. `DatabaseHostClassifier` is the model for a classifier.
- `SqlVersion` reports only the major version of the database engine, the same shape as `Storage.ServerVersion` on SQL Server and PostgreSQL. The full version text carries the patch level, edition and operating system build. That tells a reader which known vulnerabilities apply, and no analysis needs it. Azure SQL Database always reports version 12, which cannot be compared with SQL Server's version numbers. On Azure SQL Database, `SqlVersion` is `AzureSql` instead.

Each component reports its own keys. The primary and each persister do it through an `IEnvironmentDataProvider`. A transport does it through `ITransportCustomization.GetEnvironmentData`, which returns nothing by default, and the primary's `TransportEnvironmentDataProvider` adds what it returns to the report. Runtime choices are read from storage when the report is built.

Audit instance keys come only from the `GET /api/configuration` response the primary already fetches. That response gives retention, audit forwarding, maximum body size, log level and storage type. Several audit instances combine into one key per fact. A number reports the largest value, and an enum reports `Mixed` when the instances differ.

The monitoring instance is out of scope. `MonitoringEnabled` stays as it is, and the catalog documents what the key means.

Email notification details beyond on or off are out of scope. The question is whether notifications are in use, and `Features.EmailNotifications` already answers it. Licensed endpoint details are out of scope as well. They apply only to Endpoint Size licenses with endpoint metadata, and no analysis needs them.

[`docs/usage-report.md`](../usage-report.md) is the single list of what the report contains. It names every key, its values and its source. It also names every excluded setting and the reason. A pull request that adds or changes a setting updates the catalog in the same change. A missing entry is a review finding. The gaps from the audit that this decision covers are listed in the catalog as `Planned` rows.

## Consequences

- The report grows. A typical instance goes from about 30 `EnvironmentData` keys to about 65. Keys specific to a transport or a storage engine appear only on that transport or engine. The extra size is a few kilobytes in a file that is already zipped.
- Every new setting costs a little more. It needs a key or an exclusion, a catalog row, and a review against the privacy and security rules. Accepted. The four exclusion reasons keep that review short.
- Reporting `Default` needs the instance to know whether a setting was configured. Several settings parse straight to an effective value with the default folded in, `HeartbeatGracePeriod` for example. Each of those needs a small change to keep that information. Installers write some settings explicitly, such as `ShutdownTimeout`. Those report the installer's value, so analysis has to know the installer defaults.
- Transports report through a second mechanism. `ServiceControl.Transports` is strong-named and `Particular.LicensingComponent.Contracts` is not, so the transports do not reference `IEnvironmentDataProvider`. They return their keys from `ITransportCustomization.GetEnvironmentData` instead, and the primary wraps them. Accepted. The keys follow the same rules and land in the same dictionary.
- Particular has no report data on security configuration. That includes how many customers keep the insecure defaults. Accepted. Evidence for changing a secure default has to come from another source.
- Reports from 6.20.0 and 6.21.0 carry `Security.Authentication`, `Security.RoleBasedAuthorization`, `Security.Https` and `Persistence.BodyStorage.Auth`. Later versions do not. Particular deletes these values from the reports it has already received, and from reports that still arrive from instances on those versions. Instances that stay on 6.20.0 or 6.21.0 keep sending the keys until they upgrade, so a security reviewer can still object to their reports.
- `SqlVersion` changes shape but keeps its name. Reports up to 6.21.0 carry the full `@@VERSION` or `version()` text. Later reports carry the major version. Analysis reads the major version, or `AzureSql`, from either shape.
- Audit coverage stays thin. On the audit side, ingestion on or off, full-text search, embedded or external RavenDB, logging providers and OTLP stay invisible. Combining several audit instances also hides their differences behind `Mixed` or the largest value. Both are accepted for this decision. A dedicated audit environment endpoint has already been prototyped for the audit telemetry work. That endpoint is the channel if these facts are needed later.
- Monitoring stays invisible, and `MonitoringEnabled` stays misleading. The key is `False` when monitoring runs but no endpoint sends metrics. It stays `True` for up to 14 months after monitoring is removed. The catalog states what the key means, so analysis does not read it as "monitoring is installed".
- Some counts are imprecise. `Heartbeats.MonitoredInstances` cannot separate instances a user stopped monitoring from instances that never sent a heartbeat. The catalog says so.
- Each value is read when the user downloads the report, so the report holds no history over its window. Accepted.
- Older versions do not emit the new keys. Analysis treats a missing key as unknown, never as `Disabled` or zero.
- Error ingestion workers stay invisible. A worker started with `--error-ingestion-only` never builds a report. This gap was already accepted for the ingestion telemetry work.

## Alternative approaches

- Add keys on demand. This is how keys were added until now, and it costs nothing until a question comes up. It is rejected because data starts only at the release that adds the key. This is the main point of leverage. A key is cheap to add while the setting is being written. Waiting a year for the evidence is expensive.
- Report the whole settings object, minus a deny-list of identifying values. This gives full coverage for very little ongoing effort. It is rejected because a deny-list fails open: a new identifying setting would leak until someone noticed. The catalog fails closed, because a setting nobody reviewed is missing from the report.
- Enforce coverage with a convention test. A test could list every `SettingsReader` key and fail when one has neither a key nor an exclusion. It is rejected because many settings never pass through `SettingsReader`. Examples are environment variables read directly (`OTEL_EXPORTER_OTLP_ENDPOINT` and the integrated ServicePulse variables), transport connection string options, and runtime state in storage. A scanner would miss all of them and still look complete. The catalog and review cover them all, at the cost of depending on reviewers.
- Report security configuration per area, never per flag. This was the rule from 1 October 2026 until 6 October 2026. For example, `Security.TokenValidation` would be `Relaxed` when any token validation flag is off. It answers how often customers relax a protection without listing which flag a named customer turned off. It is rejected because an area-level key is still security configuration tied to a named customer. The signed report cannot be sent in part, so a reviewer who treats that as confidential blocks all of it. Licensing does not need the data either.
- Replace `CustomerName` with the license id, so that security keys could be reported. It is rejected because this is only pseudonymisation: Particular must map the id back to the customer for licensing. The report also identifies the customer in other ways. The zip and JSON file names are built from `CustomerName`. The report arrives from the customer's email domain. Queue names and the SQL Server and PostgreSQL `Scope` are in clear text unless the customer configured report masks. `NameHash` and `ScopeHash` are unsalted SHA-256 hashes of the unmasked value, so a guessed name can be confirmed. The `Report` model in `Particular.LicensingComponent.Report` 1.3.0 also has no license id field.
- A new audit endpoint for audit-side facts. It would cover full-text search and ingestion on or off. It is deferred because it needs an audit release, and audit instances already in the field would never report through it. The existing `/api/configuration` gives five facts from every audit version deployed today.
- A header on the monitoring throughput message. It is cheap, and older primaries ignore unknown headers. It is deferred because it needs a monitoring release. A monitoring instance with no monitored endpoints also sends no message. The header would therefore miss the same case where `MonitoringEnabled` reports `False` today.
