namespace ServiceControl.LoadTests.AuditGenerator
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class LoadGenerator
    {
        public LoadGenerator(string destination, Func<string, QueueInfo, CancellationToken, Task> generateMessages, int minLength, int maxLength)
        {
            this.destination = destination;
            this.generateMessages = generateMessages;
            this.minLength = minLength;
            this.maxLength = maxLength;
        }

        public async Task ProcessedCountReported(long processed)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (processed != long.MinValue)
                {
                    queueInfo.Processed(processed);
                }

                var length = queueInfo.Length;
                if (length > maxLength && tokenSource != null)
                {
                    log.Info($"Stopping sending messages to {destination} as the current queue length ({length}) is over the defined threshold ({maxLength}).");
                    tokenSource.Cancel();

                    try
                    {
                        await generationTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        //Ignore
                    }
                    catch (Exception e)
                    {
                        log.Error("Error stopping generating of messages", e);
                    }

                    tokenSource.Dispose();
                    tokenSource = null;
                    generationTask = null;
                    return;
                }

                if (length < minLength && tokenSource == null)
                {
                    log.Info($"Starting sending messages to {destination} as the current queue length ({length}) is under the defined threshold ({minLength}).");
                    tokenSource = new CancellationTokenSource();
                    generationTask = Task.Run(() => generateMessages(destination, queueInfo, tokenSource.Token), tokenSource.Token);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        QueueInfo queueInfo = new QueueInfo();
        string destination;
        Func<string, QueueInfo, CancellationToken, Task> generateMessages;
        int minLength;
        int maxLength;
        CancellationTokenSource tokenSource;
        Task generationTask;
        SemaphoreSlim semaphore = new SemaphoreSlim(1);
        static ILog log = LogManager.GetLogger<LoadGenerator>();
    }
}