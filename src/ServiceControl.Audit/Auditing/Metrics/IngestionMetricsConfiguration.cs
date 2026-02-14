namespace ServiceControl.Audit.Auditing.Metrics;

using OpenTelemetry.Metrics;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

public static class IngestionMetricsConfiguration
{
    public static void AddIngestionMetrics(this MeterProviderBuilder builder)
    {
        builder.AddMeter(IngestionMetrics.MeterName);

        // Note: Views can be replaced by new InstrumentAdvice<double> { HistogramBucketBoundaries = [...] }; once we can update to the latest OpenTelemetry packages
        builder.AddView(
            instrumentName: IngestionMetrics.MessageDurationInstrumentName,
            new ExplicitBucketHistogramConfiguration { Boundaries = [0.01, 0.05, 0.1, 0.5, 1, 5] });
        builder.AddView(
            instrumentName: IngestionMetrics.BatchDurationInstrumentName,
            new ExplicitBucketHistogramConfiguration { Boundaries = [0.01, 0.05, 0.1, 0.5, 1, 5] });

        // Retention cleanup metrics - using longer bucket boundaries since cleanup operations take longer
        builder.AddView(
            instrumentName: RetentionMetrics.CleanupDurationInstrumentName,
            new ExplicitBucketHistogramConfiguration { Boundaries = [1, 5, 10, 30, 60, 300] });
    }
}