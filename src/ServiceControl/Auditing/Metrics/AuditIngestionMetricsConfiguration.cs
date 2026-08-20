namespace ServiceControl.Auditing.Metrics;

using OpenTelemetry.Metrics;
using ServiceControl.Infrastructure.Ingestion.Metrics;

public static class AuditIngestionMetricsConfiguration
{
    // The meter is already added by the error ingestion configuration, which shares it. Only the
    // audit instruments' bucket boundaries need declaring.
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
