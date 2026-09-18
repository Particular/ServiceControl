namespace ServiceControl.Auditing.Metrics;

using OpenTelemetry.Metrics;
using ServiceControl.Infrastructure.Ingestion.Metrics;

public static class AuditIngestionMetricsConfiguration
{
    // Audit ingestion publishes on the primary instance's meter, which AddIngestionMetrics has
    // already registered, so only the audit instruments' bucket boundaries need declaring.
    public static void AddAuditIngestionMetrics(this MeterProviderBuilder builder)
    {
        foreach (var instrumentName in DurationInstruments)
        {
            builder.AddView(
                instrumentName,
                new ExplicitBucketHistogramConfiguration { Boundaries = IngestionDurations.BucketBoundaries });
        }
    }

    static readonly string[] DurationInstruments =
    [
        AuditIngestionMetrics.MessageDurationInstrumentName,
        AuditIngestionMetrics.BatchDurationInstrumentName,
        AuditIngestionMetrics.StorageDurationInstrumentName
    ];
}
