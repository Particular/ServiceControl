namespace ServiceControl.Audit.Auditing.Metrics;

using OpenTelemetry.Metrics;
using ServiceControl.Infrastructure.Ingestion.Metrics;

public static class IngestionMetricsConfiguration
{
    public static void AddIngestionMetrics(this MeterProviderBuilder builder)
    {
        builder.AddMeter(IngestionMetrics.MeterName);

        builder.AddView(
            instrumentName: IngestionMetrics.MessageDurationInstrumentName,
            new ExplicitBucketHistogramConfiguration { Boundaries = IngestionDurations.BucketBoundaries });
        builder.AddView(
            instrumentName: IngestionMetrics.BatchDurationInstrumentName,
            new ExplicitBucketHistogramConfiguration { Boundaries = IngestionDurations.BucketBoundaries });
    }
}