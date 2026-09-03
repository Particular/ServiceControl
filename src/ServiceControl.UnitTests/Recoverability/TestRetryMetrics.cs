namespace ServiceControl.UnitTests.Recoverability
{
    using System;
    using System.Diagnostics.Metrics;
    using ServiceControl.Recoverability.Retrying.Metrics;

    static class TestRetryMetrics
    {
        public static RetryMetrics Create(TimeProvider timeProvider) => new(new TestMeterFactory(), timeProvider);
    }

    sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options.Name, options.Version, options.Tags, scope: this);

        public void Dispose()
        {
        }
    }
}
