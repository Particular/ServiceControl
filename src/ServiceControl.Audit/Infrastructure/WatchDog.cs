// ReSharper disable NotAccessedField.Local
#pragma warning disable CS0649
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

        public async Task Start()
        {
            var startupAttemptDone = new TaskCompletionSource<bool>();
            Exception startupException = null;

            watchdog = Task.Run(async () =>
            {
                while (!shutdownTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        try
                        {
                            await ensureStarted(shutdownTokenSource.Token).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            startupException = e;
                            throw;
                        }

                        clearFailure();
                    }
                    catch (OperationCanceledException)
                    {
                        //Do not Delay
                        continue;
                    }
                    catch (Exception e)
                    {
                        log.Error(
                            $"Error while trying to start {processName}. Starting will be retried in {timeToWaitBetweenStartupAttempts}.",
                            e);
                        reportFailure(e.Message);
                    }
                    finally
                    {
                        startupAttemptDone.SetResult(true);
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
                    //We don't pass the shutdown token here because it has already been cancelled and we want to ensure we stop the ingestion.
                    await ensureStopped(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    log.Error($"Error while trying to stop {processName}.", e);
                    reportFailure(e.Message);
                }
            });

            await startupAttemptDone.Task.ConfigureAwait(false);

            if (startupException != null)
            {
                dynamic exception = startupException;

                if (exception.ShutdownReason.ReplyCode == 404 && exception.ShutdownReason.ReplyText.Contains("audit"))
                {
                    throw startupException;
                }
            }
        }

        public Task Stop()
        {
            shutdownTokenSource.Cancel();
            return watchdog;
        }
    }
}