# Usage report covers every customer choice

- Date: 2026-10-01
- Status: Accepted
- Implementation: link the pull requests here as they open.

## Context

The usage report tells Particular how customers run ServiceControl. The primary instance builds the report when a user downloads it from ServicePulse. The customer then sends the file to Particular. The report's `EnvironmentData` section is a flat dictionary of strings. Since 6.20.0 it has carried about 30 keys describing the host, the storage, security, a handful of features and retention.

Those keys were added one at a time, each when someone needed an answer. A key only appears in reports from the release that adds it. So the first question about a feature always arrives with no data to answer it. The proposal to ingest audit messages in the primary was withdrawn in September 2026, largely for that reason.

An audit on 1 October 2026 compared the report with every setting and runtime choice in the primary, audit and monitoring instances. The report carries about a third of what could be reported. The gaps include:

- Error ingestion turned off, OTLP metrics export, logging providers and level, and the CORS and forwarded headers posture.
- The RabbitMQ queue type and routing topology. Whenever the transport has a broker throughput query, the report's `MessageTransport` carries only the broker family name.
- The database and transport authentication modes, and the transport connection options that select a mode.
- The runtime choices users make in ServicePulse, apart from email notifications being on or off. Heartbeat instance tracking, retry redirects and report masks are all missing.
- Every tuning number, including concurrency, batch sizes, timeouts and thresholds.
- Nearly everything about the audit and monitoring instances.

The decision has to respect five constraints:

- The report names the licensee in `CustomerName` and carries masked queue names. `EnvironmentData` must add nothing that identifies the customer's infrastructure, people or data. Since August 2026 every value has been a fixed enum member, a boolean, a count, a number or a version.
- Only the primary builds the report. The primary already fetches each audit instance's `GET /api/configuration` once a day. Its only link to the monitoring instance is the throughput message monitoring sends every five minutes, and the primary reads only that message's body.
- `EnvironmentData` accepts new keys without a change to the signed report schema in `Particular.LicensingComponent.Report`. Any other change to the report needs a new version of that package.
- A value that fails to read must not stop the report. The `ReadFailed` value already covers this.
- Analysis compares reports across versions. Renaming a key forces analysis to read both spellings, as the `Persistence.*` to `Storage.*` rename did.

## Decision

Every setting or runtime choice that changes how ServiceControl behaves gets a key in `EnvironmentData`. The only exception is a setting that [the catalog](../usage-report.md) lists as excluded. An exclusion gives one of three reasons:

- Identifies: the setting is a name, address, path or secret.
- Not a choice: the setting is a test hook, a dead setting, an action, or a mode in which the instance cannot build a report.
- Out of scope: this decision excludes it.

Values follow fixed shapes:

- A switch reports `Enabled` or `Disabled`. A mode reports a fixed enum member.
- A runtime feature reports a count that shows whether it is in use, for example the number of retry redirects.
- A tuning number reports `Default` when the setting is absent from configuration. Otherwise it reports the configured number, in the unit the key name ends with.
- Security posture is reported per area, never per flag. For example, `Security.TokenValidation` is `Relaxed` when any token validation flag is off.
- A value derived from an identifying setting goes through a fixed classifier or becomes a count. `DatabaseHostClassifier` is the model for a classifier.

Each component reports its own keys through an `IEnvironmentDataProvider`. The primary and each persister already do. Each transport now does as well. Runtime choices are read from storage when the report is built.

Audit instance keys come only from the `GET /api/configuration` response the primary already fetches. That response gives retention, audit forwarding, maximum body size, log level and storage type. Several audit instances combine into one key per fact. A number reports the largest value, and an enum reports `Mixed` when the instances differ.

The monitoring instance is out of scope. `MonitoringEnabled` stays as it is, and the catalog documents what the key means.

[`docs/usage-report.md`](../usage-report.md) is the single list of what the report contains. It names every key, its values and its source. It also names every excluded setting and the reason. A pull request that adds or changes a setting updates the catalog in the same change. A missing entry is a review finding. The gaps from the audit are listed in the catalog as `Planned` rows.

## Consequences

- The report grows. A typical instance goes from about 30 `EnvironmentData` keys to about 80. Keys specific to a transport or a storage engine appear only on that transport or engine. The extra size is a few kilobytes in a file that is already zipped.
- Every new setting costs a little more. It needs a key or an exclusion, a catalog row, and a review against the privacy rule. Accepted. The three exclusion reasons keep that review short.
- Reporting `Default` needs the instance to know whether a setting was configured. Several settings parse straight to an effective value with the default folded in, `HeartbeatGracePeriod` for example. Each of those needs a small change to keep that information. Installers write some settings explicitly, such as `ShutdownTimeout`. Those report the installer's value, so analysis has to know the installer defaults.
- Transports start reporting. `ServiceControl.Transports` gains a reference to `Particular.LicensingComponent.Contracts`, which the persisters already have.
- Audit coverage stays thin. On the audit side, ingestion on or off, full-text search, embedded or external RavenDB, security, logging providers and OTLP stay invisible. Combining several audit instances also hides their differences behind `Mixed` or the largest value. Both are accepted for this decision. A dedicated audit environment endpoint has already been prototyped for the audit telemetry work. That endpoint is the channel if these facts are needed later.
- Monitoring stays invisible, and `MonitoringEnabled` stays misleading. The key is `False` when monitoring runs but no endpoint sends metrics. It stays `True` for up to 14 months after monitoring is removed. The catalog states what the key means, so analysis does not read it as "monitoring is installed".
- Security keys show that a protection is relaxed, not which one. Accepted. The decision gives up that detail so that the report does not itemise a named customer's weakened settings.
- Some counts are imprecise. `Heartbeats.MonitoredInstances` cannot separate instances a user stopped monitoring from instances that never sent a heartbeat. The catalog says so.
- Each value is read when the user downloads the report, so the report holds no history over its window. Accepted.
- Older versions do not emit the new keys. Analysis treats a missing key as unknown, never as `Disabled` or zero.
- Error ingestion workers stay invisible. A worker started with `--error-ingestion-only` never builds a report. This gap was already accepted for the ingestion telemetry work.

## Alternative approaches

- Add keys on demand. This is how keys were added until now, and it costs nothing until a question comes up. It is rejected because data starts only at the release that adds the key. This is the main point of leverage. A key is cheap to add while the setting is being written. Waiting a year for the evidence is expensive.
- Report the whole settings object, minus a deny-list of identifying values. This gives full coverage for very little ongoing effort. It is rejected because a deny-list fails open: a new identifying setting would leak until someone noticed. The catalog fails closed, because a setting nobody reviewed is missing from the report.
- Enforce coverage with a convention test. A test could list every `SettingsReader` key and fail when one has neither a key nor an exclusion. It is rejected because many settings never pass through `SettingsReader`. Examples are environment variables read directly (`OTEL_EXPORTER_OTLP_ENDPOINT` and the integrated ServicePulse variables), transport connection string options, and runtime state in storage. A scanner would miss all of them and still look complete. The catalog and review cover them all, at the cost of depending on reviewers.
- One key per security flag. This is more precise. It is rejected because the report names the customer. Area-level keys still answer the product question, which is how often customers relax a protection.
- A new audit endpoint for audit-side facts. It would cover full-text search, ingestion on or off and the audit security posture. It is deferred because it needs an audit release, and audit instances already in the field would never report through it. The existing `/api/configuration` gives five facts from every audit version deployed today.
- A header on the monitoring throughput message. It is cheap, and older primaries ignore unknown headers. It is deferred because it needs a monitoring release. A monitoring instance with no monitored endpoints also sends no message. The header would therefore miss the same case where `MonitoringEnabled` reports `False` today.
