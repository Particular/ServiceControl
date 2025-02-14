namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    public class Watchdog
    {
        Func<CancellationToken, Task> ensureStopped;
        Func<CancellationToken, Task> ensureStarted;
        Action<string> reportFailure;
        Action clearFailure;
        Task watchdog;
        CancellationTokenSource shutdownTokenSource = new();
        TimeSpan timeToWaitBetweenStartupAttempts;
        ILog log;
        string taskName;

        public Watchdog(
            string taskName,
            Func<CancellationToken, Task> ensureStarted,
            Func<CancellationToken, Task> ensureStopped, Action<string> reportFailure,
            Action clearFailure,
            TimeSpan timeToWaitBetweenStartupAttempts,
            ILog log
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

        public Task Start(Action onFailedOnStartup)
        {
            watchdog = Task.Run(async () =>
            {
                log.Debug($"Starting watching {taskName}");

                bool startup = true;

                while (!shutdownTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownTokenSource.Token);
                        cancellationTokenSource.CancelAfter(15000);

                        log.Debug($"Ensuring {taskName} is running");
                        await ensureStarted(cancellationTokenSource.Token).ConfigureAwait(false);
                        clearFailure();
                        startup = false;
                    }
                    catch (OperationCanceledException e) when (shutdownTokenSource.IsCancellationRequested)
                    {
                        log.Debug("Cancelled", e);
                        return;
                    }
                    catch (Exception e)
                    {
                        reportFailure(e.Message);

                        if (startup)
                        {
                            log.Error($"Error during initial startup attempt for {taskName}.", e);
                            onFailedOnStartup();
                            return;
                        }

                        log.Error($"Error while trying to start {taskName}. Starting will be retried in {timeToWaitBetweenStartupAttempts}.", e);
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
            });
            return Task.CompletedTask;
        }

        public async Task Stop(CancellationToken cancellationToken)
        {
            try
            {
                log.Debug($"Stopping watching process {taskName}");
                await shutdownTokenSource.CancelAsync().ConfigureAwait(false);
                await watchdog.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                log.Error($"Error while trying to stop {taskName}.", e);
                throw;
            }
            finally
            {
                await ensureStopped(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
