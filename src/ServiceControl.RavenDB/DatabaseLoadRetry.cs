namespace ServiceControl.RavenDB
{
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Exceptions.Database;

    public static class DatabaseLoadRetry
    {
        public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(500);

        public static Task Execute(Func<CancellationToken, Task> action, ILogger logger, CancellationToken cancellationToken = default) =>
            Execute(action, logger, DefaultRetryDelay, cancellationToken);

        public static async Task Execute(Func<CancellationToken, Task> action, ILogger logger, TimeSpan retryDelay, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await action(cancellationToken);
                    return;
                }
                catch (DatabaseLoadTimeoutException e)
                {
                    logger.LogWarning(e, "Connecting to the RavenDB database timed out. Retrying in {RetryDelay}ms...", retryDelay.TotalMilliseconds);
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }
        }
    }
}
