namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Infrastructure.Metrics;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestion : IHostedService
    {
        static ILog log = LogManager.GetLogger<ErrorIngestion>();
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public ErrorIngestion(
            Settings settings,
            RawEndpointFactory rawEndpointFactory,
            Metrics metrics,
            IDocumentStore documentStore,
            LoggingSettings loggingSettings,
            ErrorIngestionCustomCheck.State ingestionState,
            ErrorIngestor ingestor)
        {
            this.settings = settings;
            errorQueue = settings.ErrorQueue;
            this.rawEndpointFactory = rawEndpointFactory;
            this.ingestor = ingestor;

            receivedMeter = metrics.GetCounter("Error ingestion - received");
            batchSizeMeter = metrics.GetMeter("Error ingestion - batch size");
            batchDurationMeter = metrics.GetMeter("Error ingestion - batch processing duration", FrequencyInMilliseconds);

            channel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(settings.MaximumConcurrencyLevel)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            errorHandlingPolicy = new ErrorIngestionFaultPolicy(documentStore, loggingSettings, (failure, arg2) =>
            {
                log.Warn($"OnCriticalError. '{failure}'", arg2);
                return watchdog.OnFailure(failure);
            });

            watchdog = new Watchdog(EnsureStarted, EnsureStopped, ingestionState.ReportError,
                ingestionState.Clear, settings.TimeToRestartErrorIngestionAfterFailure, log, "failed message ingestion");

            ingestionWorker = Task.Run(() => Loop(), CancellationToken.None);
        }

        public Task StartAsync(CancellationToken _) => watchdog.Start();

        public async Task StopAsync(CancellationToken _)
        {
            await watchdog.Stop().ConfigureAwait(false);
            channel.Writer.Complete();
            await ingestionWorker.ConfigureAwait(false);
        }

        async Task Loop()
        {
            var contexts = new List<MessageContext>(settings.MaximumConcurrencyLevel);

            while (await channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                // will only enter here if there is something to read.
                try
                {
                    // as long as there is something to read this will fetch up to MaximumConcurrency items
                    while (channel.Reader.TryRead(out var context))
                    {
                        contexts.Add(context);
                    }

                    batchSizeMeter.Mark(contexts.Count);
                    using (batchDurationMeter.Measure())
                    {
                        await ingestor.Ingest(contexts, dispatcher).ConfigureAwait(false);
                    }
                }
                catch (Exception e) // show must go on
                {
                    if (log.IsInfoEnabled)
                    {
                        log.Info("Ingesting messages failed", e);
                    }

                    // signal all message handling tasks to terminate
                    foreach (var context in contexts)
                    {
                        context.GetTaskCompletionSource().TrySetException(e);
                    }
                }
                finally
                {
                    contexts.Clear();
                }
            }
            // will fall out here when writer is completed
        }

        async Task EnsureStarted(CancellationToken cancellationToken = default)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (ingestionEndpoint != null)
                {
                    return; //Already started
                }

                var rawConfiguration = rawEndpointFactory.CreateErrorIngestor(errorQueue, OnMessage);

                rawConfiguration.Settings.Set("onCriticalErrorAction", (Func<ICriticalErrorContext, Task>)OnCriticalErrorAction);

                rawConfiguration.CustomErrorHandlingPolicy(errorHandlingPolicy);

                var startableRaw = await RawEndpoint.Create(rawConfiguration).ConfigureAwait(false);

                dispatcher = startableRaw;

                if (settings.ForwardErrorMessages)
                {
                    await ingestor.VerifyCanReachForwardingAddress(dispatcher).ConfigureAwait(false);
                }

                ingestionEndpoint = await startableRaw.Start()
                    .ConfigureAwait(false);
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        async Task OnMessage(MessageContext messageContext, IDispatchMessages dispatcher)
        {
            if (settings.MessageFilter != null && settings.MessageFilter(messageContext))
            {
                return;
            }

            var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            messageContext.SetTaskCompletionSource(taskCompletionSource);

            receivedMeter.Mark();

            await channel.Writer.WriteAsync(messageContext).ConfigureAwait(false);
            await taskCompletionSource.Task.ConfigureAwait(false);
        }

        Task OnCriticalErrorAction(ICriticalErrorContext ctx)
        {
            log.Warn($"OnCriticalError. '{ctx.Error}'", ctx.Exception);
            return watchdog.OnFailure(ctx.Error);
        }

        async Task EnsureStopped(CancellationToken cancellationToken = default)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (ingestionEndpoint == null)
                {
                    return; //Already stopped
                }
                var stoppable = ingestionEndpoint;
                ingestionEndpoint = null;
                await stoppable.Stop().ConfigureAwait(false);
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        SemaphoreSlim startStopSemaphore = new SemaphoreSlim(1);
        readonly Settings settings;
        string errorQueue;
        RawEndpointFactory rawEndpointFactory;
        ErrorIngestionFaultPolicy errorHandlingPolicy;
        IReceivingRawEndpoint ingestionEndpoint;
        readonly Watchdog watchdog;
        readonly Channel<MessageContext> channel;
        readonly Task ingestionWorker;
        readonly Meter batchDurationMeter;
        readonly Meter batchSizeMeter;
        readonly Counter receivedMeter;
        readonly ErrorIngestor ingestor;
        IDispatchMessages dispatcher;
    }
}