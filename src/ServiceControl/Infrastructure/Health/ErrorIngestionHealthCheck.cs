namespace ServiceControl.Infrastructure.Health
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;

    /// <summary>
    /// Reports the state the ingestion watchdog publishes. A batch that keeps failing, including
    /// because the database is unreachable, trips the fault policy's circuit breaker, which raises a
    /// critical error, which the watchdog records here. So this covers rather more than the receiver
    /// failing to start.
    /// </summary>
    class ErrorIngestionHealthCheck(ErrorIngestionCustomCheck.State ingestionState, Settings settings) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!settings.IngestErrorMessages)
            {
                return Task.FromResult(HealthCheckResult.Healthy("Error ingestion is disabled"));
            }

            var failure = ingestionState.GetLastFailure();

            return Task.FromResult(failure == null
                ? HealthCheckResult.Healthy("Ingesting error messages")
                : HealthCheckResult.Unhealthy(failure));
        }
    }
}
