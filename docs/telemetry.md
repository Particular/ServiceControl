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

#### Success or failure

- `sc.audit.ingestion.success` - Successful ingested audit message count (Counter)
- `sc.audit.ingestion.retry` - Retried audit message count (Counter)
- `sc.audit.ingestion.failed` - Failed audit message count (Counter)

The above metrics also have the following attributes attached:

- `messaging.message.body.size` - The size of the message body in bytes
- `messaging.message.type` - The logical message type of the message if present

#### Details

- `sc.audit.ingestion.duration` - Audit message processing duration in milliseconds (Histogram)
- `sc.audit.ingestion.forwarded` - Count of the number of forwarded audit messages if forwarding is enabled (Counter)

### Batching

- `sc.audit.ingestion.batch_duration` - Batch processing duration in milliseconds (Histogram)
  - Attributes:
    - `ingestion.batch_size`
- `sc.audit.ingestion.consecutive_batch_failures` - Consecutive batch failures (Counter)

## Monitoring

No telemetry is currently available.
