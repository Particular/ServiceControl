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
        CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
        TimeSpan timeToWaitBetweenStartupAttempts;
        ILog log;
        string taskName;

        public Watchdog(string taskName, Func<CancellationToken, Task> ensureStarted,
            Func<CancellationToken, Task> ensureStopped, Action<string> reportFailure, Action clearFailure,
            TimeSpan timeToWaitBetweenStartupAttempts, ILog log)
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

                bool? failedOnStartup = null;

                while (!shutdownTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        log.Debug($"Ensuring {taskName} is running");
                        await ensureStarted(shutdownTokenSource.Token).ConfigureAwait(false);
                        clearFailure();

                        failedOnStartup ??= false;
                    }
                    catch (OperationCanceledException e) when (!shutdownTokenSource.IsCancellationRequested)
                    {
                        // Continue, as OCE is not from caller
                        log.Info("Start cancelled, retrying...", e);
                        continue;
                    }
                    catch (Exception e)
                    {
                        reportFailure(e.Message);

                        if (failedOnStartup == null)
                        {
                            failedOnStartup = true;

                            log.Error($"Error during initial startup attempt for {taskName}.", e);

                            //there was an error during startup hence we want to shut down the instance
                            onFailedOnStartup();
                        }
                        else
                        {
                            log.Error($"Error while trying to start {taskName}. Starting will be retried in {timeToWaitBetweenStartupAttempts}.", e);
                        }
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
                try
                {
                    log.Debug($"Stopping watching process {taskName}");
                    //We don't pass the shutdown token here because it has already been cancelled and we want to ensure we stop the ingestion.
                    await ensureStopped(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    log.Error($"Error while trying to stop {taskName}.", e);
                    reportFailure(e.Message);
                }
            });
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            shutdownTokenSource.Cancel();
            return watchdog;
        }
    }
}
