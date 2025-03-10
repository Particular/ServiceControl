# Telemetry

Instances can be configured to emit telemetry to aid in performance testing or troubleshooting performance-related issues.

## Error

Setting `ServiceControl/PrintMetrics` to `true` will print metrics to the logs at `INFO` level.

## Audit

Set `ServiceControl.Audit/OtlpEndpointUrl` to a valid [OTLP endpoint url](https://opentelemetry.io/docs/specs/otel/protocol/exporter/#configuration-options). Only GRPC endpoints are supported at this stage.

It's recommended to use a local [OTEL Collector](https://opentelemetry.io/docs/collector/) to collect, batch and export the metrics to the relevant observability backend being used.

Example configuration: https://github.com/andreasohlund/Docker/tree/main/otel-monitoring

The following ingestion metrics with their corresponding dimensions are available:

- `sc.audit.ingestion.batch_duration_seconds` - Message batch processing duration in seconds
  - `result` - Indicates if the full batch size was used (batch size == max concurrency of the transport): `full`, `partial` or `failed`
- `sc.audit.ingestion.message_duration_seconds` - Audit message processing duration in seconds
  - `message.category` - Indicates the category of the message ingested: `audit-message`, `saga-update` or `control-message`
  - `result` - Indicates the outcome of the operation: `success`, `failed` or `skipped` (if the message was filtered out and skipped)
- `sc.audit.ingestion.failures_total` - Failure counter
  - `message.category` - Indicates the category of the message ingested: `audit-message`, `saga-update` or `control-message`
  - `result` - Indicates how the failure was resolved: `retry` or `stored-poison`
- `sc.audit.ingestion.consecutive_batch_failure_total` - Consecutive batch failures

Example queries in PromQL for use in Grafana:

- Ingestion rate: `sum (rate(sc_audit_ingestion_message_duration_seconds_count[$__rate_interval])) by (exported_job)`
- Failure rate: `sum(rate(sc_audit_ingestion_failures_total[$__rate_interval])) by (exported_job,result)`
- Message duration: `histogram_quantile(0.9,sum(rate(sc_audit_ingestion_message_duration_seconds_bucket[$__rate_interval])) by (le,exported_job))` 

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
