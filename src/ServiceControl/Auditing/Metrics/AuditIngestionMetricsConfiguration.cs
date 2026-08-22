namespace ServiceControl.Auditing.Metrics
{
    using OpenTelemetry.Metrics;

    public static class AuditIngestionMetricsConfiguration
    {
        public static void AddAuditIngestionMetrics(this MeterProviderBuilder builder)
        {
            builder.AddMeter(AuditIngestionMetrics.MeterName);

            // Note: Views can be replaced by new InstrumentAdvice<double> { HistogramBucketBoundaries = [...] }; once we can update to the latest OpenTelemetry packages
            builder.AddView(
                instrumentName: AuditIngestionMetrics.MessageDurationInstrumentName,
                new ExplicitBucketHistogramConfiguration { Boundaries = [0.01, 0.05, 0.1, 0.5, 1, 5] });
            builder.AddView(
                instrumentName: AuditIngestionMetrics.BatchDurationInstrumentName,
                new ExplicitBucketHistogramConfiguration { Boundaries = [0.01, 0.05, 0.1, 0.5, 1, 5] });
        }
    }
}
