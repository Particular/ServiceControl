# Telemetry

Instances can be configured to emit telemetry to aid in performance testing or troubleshooting performance-related issues.

## Error

Setting `ServiceControl/PrintMetrics` to `true` will print metrics to the logs at `INFO` level.

## Audit

Set `ServiceControl.Audit/OtlpEndpointUrl` to a valid [OTLP endpoint url](https://opentelemetry.io/docs/specs/otel/protocol/exporter/#configuration-options). Only GRPC endpoints are supported at this stage.

It's recommended to use a local [OTEL Collector](https://opentelemetry.io/docs/collector/) to collect, batch and export the metrics to the relevant observability backend being used.

Example configuration: https://github.com/andreasohlund/Docker/tree/main/otel-monitoring

The following metrics are available:

### Ingestion

- `sc.audit.ingestion.count` - Successful ingested audit message count
- `sc.audit.ingestion.retry` - Retried audit message count
- `sc.audit.ingestion.failed` - Failed audit message count
- `sc.audit.ingestion.duration` - Audit message processing duration (in milliseconds)
- `sc.audit.ingestion.message_size` - Audit message body size (in kilobytes)
- `sc.audit.ingestion.forwarded_count` - Forwarded audit messages count

### Batching

- `sc.audit.ingestion.batch_duration` - Batch processing duration (in milliseconds)
- `sc.audit.ingestion.batch_size` - Batch size (number of messages)
- `sc.audit.ingestion.consecutive_batch_failures` - Consecutive batch failures

### Storage

- `sc.audit.ingestion.audits_count` - Stored audit message count
- `sc.audit.ingestion.sagas_count` - Stored sagas message count
- `sc.audit.ingestion.commit_duration` - Storage unit of work commit duration (in milliseconds)

## Monitoring

No telemetry is currently available.
