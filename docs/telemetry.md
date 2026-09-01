# Telemetry

Instances can be configured to emit telemetry to aid in performance testing or troubleshooting performance-related issues.

Both the error and the audit instance report their ingestion the same way. Exporting is configured with the standard [OpenTelemetry environment variables](https://opentelemetry.io/docs/specs/otel/protocol/exporter/#configuration-options), not with instance settings, so the same variables that configure any other OpenTelemetry process apply here. Setting `OTEL_EXPORTER_OTLP_ENDPOINT` is enough to turn metrics on. Both gRPC and HTTP endpoints are supported, and `OTEL_EXPORTER_OTLP_PROTOCOL` selects between them.

The signal-specific variables, `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` and its siblings, have no effect. The SDK only honours those under `UseOtlpExporter`, and instances use `AddOtlpExporter` so that OTLP applies to metrics without also being turned on for every other signal.

Logs are exported separately. Add `Otlp` to the instance's `LoggingProviders` setting, which is what turns the OTLP log exporter on, and it then reads the same environment variables for its endpoint.

The instruments differ only in their prefix and in the categories a message can fall into, so the same dashboard works for both with the prefix swapped. What the batches being measured actually are is covered in [ingestion-pipeline.md](ingestion-pipeline.md).

## Error

Meter `Particular.ServiceControl`.

- `sc.error.ingestion.batch_duration_seconds` - Message batch processing duration in seconds
  - `result` - Whether the batch was written at its configured size: `full`, `partial` or `failed`
- `sc.error.ingestion.storage_duration_seconds` - The storage write of a batch, without the announcing and forwarding around it
- `sc.error.ingestion.message_duration_seconds` - Error message processing duration in seconds
  - `message.category` - What the message is: `failed-message` or `retry-confirmation`
  - `result` - The outcome: `success`, `failed` or `skipped` (if the message was filtered out and skipped)
- `sc.error.ingestion.failures_total` - Failure counter
  - `message.category` - What the message is: `failed-message` or `retry-confirmation`
  - `result` - How the failure was resolved: `retry` or `stored-poison`
- `sc.error.ingestion.consecutive_batch_failures_total` - Consecutive batch failures

`ServiceControl/PrintMetrics` predates this and no longer has anything to print.

### Retry

The retry pipeline runs in four stages: a bulk request is queued, preparation scans the store and cuts batches of 1000, staging dispatches a batch to the staging queue, and forwarding returns the staged messages to their senders. Each stage gets its own duration histogram, and the whole operation gets one end-to-end histogram on top.

Every instrument carries `retry.type`, one of `all`, `endpoint`, `group`, `queue`, `batch` or `single`.

- `sc.retry.operation_duration_seconds` - The whole retry operation, from the request arriving to the last message forwarded or skipped
  - `result` - `success` or `failed`
- `sc.retry.prepare_duration_seconds` - Preparation, covering the store scan and batch creation. This is the stage that grows with the size of the error store.
  - `result` - `success`, `failed`, or `cancelled` if shutdown cut the preparation short
- `sc.retry.stage_duration_seconds` - Staging one batch to the staging queue
  - `result` - `success`, `failed`, `empty` for a batch that had no messages left and was discarded, or `cancelled` if shutdown cut the staging short
- `sc.retry.forward_duration_seconds` - Forwarding one batch back to the senders
  - `result` - `success`, `failed`, or `cancelled` if shutdown cut the forwarding short
  - `mode` - `counting`, or `timeout` when recovering from a premature shutdown. Timeout mode only ends on the forwarder's 45 second idle timer, so its distribution has a floor at that value.
- `sc.retry.messages_total` - Messages moved through the pipeline
  - `result` - `staged`, `forwarded`, `skipped`, `staging_retried`, or `abandoned` for a message that hit the staging retry limit and was dropped from its batch. `abandoned` is the one to alert on: it is a message the user asked to retry that will not be retried.
- `sc.retry.operations_in_progress` - Retry operations currently in progress
  - `retry.state` - `waiting`, `preparing` or `forwarding`
- `sc.retry.pending_bulk_requests` - Bulk retry requests queued behind each other, drained one per five second tick

A retry that hangs never records a duration, so on the histograms alone a stuck operation looks identical to no traffic. `operations_in_progress` holding a non-zero value while the duration histograms stay flat is the stuck-operation signal.

### Archive

Group archive and unarchive run as a loop over batches of 1000 until the group is drained. These instruments are emitted only when the instance runs on the SQL Server or PostgreSQL persistence; on RavenDB the family does not exist.

Every instrument carries `archive.operation`, either `archive` or `unarchive`.

- `sc.archive.operation_duration_seconds` - The whole group operation
- `sc.archive.batch_duration_seconds` - One batch of the loop, measured as the wall time between successive batch completions
- `sc.archive.messages_total` - Messages archived and unarchived
- `sc.archive.operations_in_progress` - Operations currently in progress
  - `archive.state` - `started`, `progressing` or `finalizing`

### Host

With an OTLP endpoint configured the error instance also exports the standard [ASP.NET Core](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-aspnetcore) instruments (`http.server.request.duration` per route, which covers the read APIs), the [HTTP client](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-system-net) instruments (`http.client.request.duration`, which covers the scatter-gather calls to remote instances), and the [runtime](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime) instruments (GC, thread pool, exceptions).

## Audit

Meter `Particular.ServiceControl.Audit`.

- `sc.audit.ingestion.batch_duration_seconds` - Message batch processing duration in seconds
  - `result` - Whether the batch was written at its configured size: `full`, `partial` or `failed`
- `sc.audit.ingestion.message_duration_seconds` - Audit message processing duration in seconds
  - `message.category` - Indicates the category of the message ingested: `audit-message`, `saga-update` or `control-message`
  - `result` - Indicates the outcome of the operation: `success`, `failed` or `skipped` (if the message was filtered out and skipped)
- `sc.audit.ingestion.failures_total` - Failure counter
  - `message.category` - Indicates the category of the message ingested: `audit-message`, `saga-update` or `control-message`
  - `result` - Indicates how the failure was resolved: `retry` or `stored-poison`
- `sc.audit.ingestion.consecutive_batch_failures_total` - Consecutive batch failures

## Reading the ingestion metrics

Example queries in PromQL, shown for audit. Swap `sc_audit` for `sc_error` for the error instance.

- Ingestion rate: `sum (rate(sc_audit_ingestion_message_duration_seconds_count[5m])) by (exported_job)`
- Failure rate: `sum(rate(sc_audit_ingestion_failures_total[5m])) by (exported_job,result)`
- Message duration: `histogram_quantile(0.9,sum(rate(sc_audit_ingestion_message_duration_seconds_bucket[5m])) by (le,exported_job))` 

What the shapes mean when tuning:

- `result="full"` dominating the batch duration histogram means batches are being filled, so the
  limit is the write and not the arrival of messages. More writers, or a larger batch size, is
  where to look.
- `result="partial"` dominating at high throughput means the pipeline is writing before batches
  fill. A batch timeout is what makes them accumulate.
- Message duration far above batch duration means messages are queueing behind the writers rather
  than being slow to write.
- `consecutive_batch_failures_total` counts whole batches that failed in a row, whatever ended them:
  the storage write, but equally announcing or forwarding. It is not a tuning signal, and it is not
  the same thing the ingestion custom check and the health endpoints report, which is the critical
  failure the watchdog acts on. A gauge climbing while health stays clear means batches are failing
  and being retried.

Example Grafana dashboard - https://github.com/andreasohlund/Docker/blob/main/otel-monitoring/grafana-platform-template.json

## Retention

Only the SQL persisters report retention. On RavenDB the expiry is metadata on each document and the deletes happen inside the Raven server, so there is no sweep of ours to measure.

Meter `Particular.ServiceControl`, the same meter the instance uses for its ingestion. The prefix carries no instance segment, so when the audit SQL persister reports its own retention it uses these same names, and `job` (or `exported_job`) separates the two.

The sweep runs hourly and makes one pass per kind of row. Each pass is measured on its own, tagged `retention.entity`: `failed_messages`, `event_log` or `group_comments`.

- `sc.retention.cycle_duration_seconds` - Retention sweep pass duration in seconds
  - `retention.entity` - Which pass this was
  - `result` - The outcome: `success`, `failed`, or `cancelled` if shutdown cut the pass short
- `sc.retention.rows_deleted_total` - Rows deleted by the retention sweep
  - `retention.entity` - Which pass deleted them
- `sc.retention.consecutive_failures_total` - Consecutive failures of that pass
  - `retention.entity` - Which pass is failing

### Reading the retention metrics

- Rows reclaimed per hour: `sum(rate(sc_retention_rows_deleted_total[1h])) by (exported_job,retention_entity)`
- Sweep duration: `histogram_quantile(0.9,sum(rate(sc_retention_cycle_duration_seconds_bucket[6h])) by (le,exported_job,retention_entity))`
- Retention is broken: `max(sc_retention_consecutive_failures_total) by (exported_job,retention_entity) > 2`

What the shapes mean:

- `consecutive_failures_total` above zero is the signal that rows are no longer being reclaimed. Nothing else in the product reports this, and the database grows without bound while it lasts.
- `rows_deleted_total` flat at zero across a long window is only healthy if the instance is also not ingesting. Deletion stopping while ingestion continues means the retention window is not being enforced.
- A cycle duration climbing towards the hourly interval means the sweep is no longer keeping up with the arrival rate, and each run starts further behind than the last.
- A body store that refuses a delete fails the `failed_messages` pass rather than orphaning the body, so an expired credential or a changed permission shows as a climbing gauge instead of storage that quietly keeps growing. Those rows stay until a sweep can delete the body and the row together.
- Each pass is isolated, so one kind of row failing to be reclaimed does not stop the others. Every pass reports a result on every run, and the gauge is per pass, so alert across all entities rather than on any one of them.

## Monitoring

No telemetry is currently available.

## RavenDB

To emit and visualize RavenDB telemetry:

1. Install a RavenDB developer license (needed to get support for emitting telemetry)
2. [Enable and configure Raven to emit telemetry](https://ravendb.net/docs/article-page/6.2/csharp/server/administration/monitoring/open-telemetry) (the example below shows targeting a local OTEL collector)
    ```
    environment:
      RAVEN_Monitoring_OpenTelemetry_Enabled: true
      RAVEN_Monitoring_OpenTelemetry_OpenTelemetryProtocol_Enabled: true
      RAVEN_Monitoring_OpenTelemetry_OpenTelemetryProtocol_Protocol: gRPC
      RAVEN_Monitoring_OpenTelemetry_OpenTelemetryProtocol_Endpoint: http://host.docker.internal:4317
    ```
3. Visualize the data, for example https://grafana.com/grafana/dashboards/22698-ravendb-prometheus/ 

## OTEL Collector

It's recommended to use a local [OTEL Collector](https://opentelemetry.io/docs/collector/) to collect, batch and export the metrics to the relevant observability backend being used.

Example configuration: https://github.com/andreasohlund/Docker/tree/main/otel-monitoring

### Azure Monitor

User the [exporter for Azure Monitor](https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/main/exporter/azuremonitorexporter/README.md) to push telemetry to application insights.
