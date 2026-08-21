namespace ServiceControl.Operations.Metrics;

using OpenTelemetry.Metrics;
using ServiceControl.Infrastructure.Ingestion.Metrics;

public static class IngestionMetricsConfiguration
{
    public static void AddIngestionMetrics(this MeterProviderBuilder builder)
    {
        builder.AddMeter(IngestionMetrics.MeterName);

        foreach (var instrumentName in DurationInstruments)
        {
            builder.AddView(
                instrumentName,
                new ExplicitBucketHistogramConfiguration { Boundaries = IngestionDurations.BucketBoundaries });
        }
    }

    static readonly string[] DurationInstruments =
    [
        IngestionMetrics.MessageDurationInstrumentName,
        IngestionMetrics.BatchDurationInstrumentName,
        IngestionMetrics.StorageDurationInstrumentName
    ];
}