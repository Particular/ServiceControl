namespace ServiceControl.Infrastructure.Ingestion.Metrics;

/// <summary>
/// The histogram buckets every ingestion duration is reported in, shared so the instances stay
/// comparable on one dashboard.
/// </summary>
public static class IngestionDurations
{
    // Views can give way to new InstrumentAdvice<double> { HistogramBucketBoundaries = ... } once we
    // can update to the latest OpenTelemetry packages
    public static readonly double[] BucketBoundaries = [0.01, 0.05, 0.1, 0.5, 1, 5];
}