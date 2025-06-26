namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class Watchdog
    {
        Func<CancellationToken, Task> ensureStopped;
        Func<CancellationToken, Task> ensureStarted;
        Action<string> reportFailure;
        Action clearFailure;
        Task watchdog;
        CancellationTokenSource shutdownTokenSource = new();
        TimeSpan timeToWaitBetweenStartupAttempts;
        ILogger log;
        string taskName;

        public Watchdog(
            string taskName,
            Func<CancellationToken, Task> ensureStarted,
            Func<CancellationToken, Task> ensureStopped, Action<string> reportFailure,
            Action clearFailure,
            TimeSpan timeToWaitBetweenStartupAttempts,
            ILogger log
        )
        {
            this.taskName = taskName;
            this.ensureStopped = ensureStopped;
            this.ensureStarted = ensureStarted;
            this.reportFailure = reportFailure;
            this.clearFailure = clearFailure;
            this.timeToWaitBetweenStartupAttempts = timeToWaitBetweenStartupAttempts;
            this.log = log;
        }

        public Task OnFailure(string failure)
        {
            reportFailure(failure);
            return ensureStopped(shutdownTokenSource.Token);
        }

        public Task Start(Action onFailedOnStartup, CancellationToken cancellationToken)
        {
            watchdog = Task.Run(async () =>
            {
                log.LogDebug("Starting watching {TaskName}", taskName);

                bool startup = true;

                while (!shutdownTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        // Host builder start is launching the loop. The watch dog loop task runs in isolation
                        // We want the start not to run to infinity. An NServiceBus endpoint should easily
                        // start within 15 seconds.
                        const int MaxStartDurationMs = 15000;
                        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownTokenSource.Token);
                        cancellationTokenSource.CancelAfter(MaxStartDurationMs);

                        log.LogDebug("Ensuring {TaskName} is running", taskName);
                        await ensureStarted(cancellationTokenSource.Token).ConfigureAwait(false);
                        clearFailure();
                        startup = false;
                    }
                    catch (OperationCanceledException e) when (shutdownTokenSource.IsCancellationRequested)
                    {
                        log.LogDebug(e, "Cancelled");
                        return;
                    }
                    catch (Exception e)
                    {
                        reportFailure(e.Message);

                        if (startup)
                        {
                            log.LogError(e, "Error during initial startup attempt for {TaskName}", taskName);
                            onFailedOnStartup();
                            return;
                        }

                        log.LogError(e, "Error while trying to start {TaskName}. Starting will be retried in {TimeToWaitBetweenStartupAttempts}", taskName, timeToWaitBetweenStartupAttempts);
                    }
                    try
                    {
                        await Task.Delay(timeToWaitBetweenStartupAttempts, shutdownTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (shutdownTokenSource.IsCancellationRequested)
                    {
                        //Ignore, no need to log cancellation of delay
                    }
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public async Task Stop(CancellationToken cancellationToken)
        {
            try
            {
                log.LogDebug("Starting watching {TaskName}", taskName);
                await shutdownTokenSource.CancelAsync().ConfigureAwait(false);
                await watchdog.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                log.LogError(e, "Ensuring {TaskName} is running", taskName);
                throw;
            }
            finally
            {
                await ensureStopped(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
