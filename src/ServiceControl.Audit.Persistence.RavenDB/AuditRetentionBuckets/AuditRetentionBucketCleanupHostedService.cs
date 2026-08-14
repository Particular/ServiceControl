namespace ServiceControl.Audit.Persistence.RavenDB.AuditRetentionBuckets
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Periodically retires expired retention buckets and deletes their dedicated indexes and
    /// collections. Only registered when bucket mode is enabled. The interval reuses the existing
    /// expiration process timer setting.
    ///
    /// The loop is driven by a <see cref="PeriodicTimer"/> created with the injected
    /// <see cref="TimeProvider"/>, so tests that register a controllable provider also control when
    /// the cleanup cycle runs.
    /// </summary>
    sealed class AuditRetentionBucketCleanupHostedService(
        AuditRetentionBucketManager bucketManager,
        DatabaseConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<AuditRetentionBucketCleanupHostedService> logger)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configuration.ExpirationProcessTimerInSeconds), timeProvider);

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await RunCleanup(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutting down, nothing to retry.
            }
        }

        async Task RunCleanup(CancellationToken cancellationToken)
        {
            try
            {
                await bucketManager.RunCleanup(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutting down, nothing to retry.
            }
            catch (Exception e)
            {
                // The bucket manager serializes cleanup, so a failure here is retried on the next cycle.
                logger.LogWarning(e, "Audit retention bucket cleanup failed and will be retried on the next cycle.");
            }
        }
    }
}
