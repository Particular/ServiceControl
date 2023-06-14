namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class Watchdog
    {
        Func<CancellationToken, Task> ensureStopped;
        Func<CancellationToken, Task> ensureStarted;
        Action<string> reportFailure;
        Action clearFailure;
        Task watchdog;
        CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
        TimeSpan timeToWaitBetweenStartupAttempts;
        ILog log;
        string processName;
        bool startedSuccessfuly;

        public Watchdog(Func<CancellationToken, Task> ensureStarted, Func<CancellationToken, Task> ensureStopped, Action<string> reportFailure, Action clearFailure, TimeSpan timeToWaitBetweenStartupAttempts, ILog log, string processName)
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
            return ensureStopped(shutdownTokenSource.Token);
        }

        public Task Start()
        {
            watchdog = Task.Run(async () =>
            {
                while (!shutdownTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        await ensureStarted(shutdownTokenSource.Token).ConfigureAwait(false);
                        startedSuccessfuly = true;
                        clearFailure();
                    }
                    catch (OperationCanceledException)
                    {
                        //Do not Delay
                        continue;
                    }
                    catch (Exception e)
                    {
                        reportFailure(e.Message);
                        if (!startedSuccessfuly)
                        {
                            //there was an error during startup hence we want to shut down the instance
                            shutdownTokenSource.Cancel(); //NOTE seems to be hanging
                        }
                        else
                        {
                            log.Error($"Error while trying to start {processName}. Starting will be retried in {timeToWaitBetweenStartupAttempts}.", e);
                        }
                    }
                    try
                    {
                        if (startedSuccessfuly)
                        {
                            await Task.Delay(timeToWaitBetweenStartupAttempts, shutdownTokenSource.Token).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        //Ignore
                    }
                }
                try
                {
                    //We don't pass the shutdown token here because it has already been cancelled and we want to ensure we stop the ingestion.
                    await ensureStopped(CancellationToken.None).ConfigureAwait(false);
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