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
