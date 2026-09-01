namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Diagnostics.Metrics;
    using ServiceControl.Recoverability.Retrying.Metrics;

    static class TestRetryMetrics
    {
        public static RetryMetrics Create() => new(new TestMeterFactory(), TimeProvider.System);
    }

    sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options.Name, options.Version, options.Tags, scope: this);

        public void Dispose()
        {
        }
    }
}
