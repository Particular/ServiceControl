namespace ServiceControl.Infrastructure.Health
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing;

    /// <summary>
    /// Reports the state the audit ingestion watchdog publishes, which covers a batch that keeps
    /// failing as well as a receiver that fails to start.
    /// </summary>
    class AuditIngestionHealthCheck(AuditIngestionCustomCheck.State ingestionState, Settings settings) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!settings.IngestAuditMessages)
            {
                return Task.FromResult(HealthCheckResult.Healthy("Audit ingestion is disabled"));
            }

            var failure = ingestionState.GetLastFailure();

            return Task.FromResult(failure == null
                ? HealthCheckResult.Healthy("Ingesting audit messages")
                : HealthCheckResult.Unhealthy(failure));
        }
    }
}
