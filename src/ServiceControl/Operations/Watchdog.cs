namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class Watchdog
    {
        Func<Task> ensureStopped;
        Func<Task> ensureStarted;
        Action<string> reportFailure;
        Action clearFailure;
        Task watchdog;
        CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
        TimeSpan timeToWaitBetweenStartupAttempts;
        ILog log;
        string processName;

        public Watchdog(Func<Task> ensureStarted, Func<Task> ensureStopped, Action<string> reportFailure, Action clearFailure, TimeSpan timeToWaitBetweenStartupAttempts, ILog log, string processName)
        {
            this.ensureStopped = ensureStopped;
            this.ensureStarted = ensureStarted;
            this.reportFailure = reportFailure;
            this.clearFailure = clearFailure;
            this.timeToWaitBetweenStartupAttempts = timeToWaitBetweenStartupAttempts;
            this.log = log;
            this.processName = processName;
        }

        public Task OnFailure(string failure)
        {
            reportFailure(failure);
            return ensureStopped();
        }

        public Task Start()
        {
            watchdog = Task.Run(async () =>
            {
                while (!shutdownTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        await ensureStarted().ConfigureAwait(false);
                        clearFailure();
                    }
                    catch (OperationCanceledException)
                    {
                        //Do not Delay
                        continue;
                    }
                    catch (Exception e)
                    {
                        log.Error($"Error while trying to start {processName}. Starting will be retried in {timeToWaitBetweenStartupAttempts}.", e);
                        reportFailure(e.Message);
                    }
                    try
                    {
                        await Task.Delay(timeToWaitBetweenStartupAttempts, shutdownTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        //Ignore
                    }
                }
                try
                {
                    await ensureStopped().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    log.Error($"Error while trying to stop {processName}.", e);
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