namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure.DomainEvents;
    using Infrastructure.Metrics;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Client;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestionComponent
    {
        static ILog log = LogManager.GetLogger<ErrorIngestionComponent>();
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        ImportFailedErrors failedImporter;
        Watchdog watchdog;
        Task ingestionWorker;
        Settings settings;
        Channel<MessageContext> channel;
        ErrorIngestor ingestor;
        ErrorPersister persister;
        Counter receivedMeter;
        Meter batchSizeMeter;
        Meter batchDurationMeter;

        public ErrorIngestionComponent(
            Metrics metrics,
            Settings settings,
            IDocumentStore documentStore,
            IDomainEvents domainEvents,
            RawEndpointFactory rawEndpointFactory,
            LoggingSettings loggingSettings,
            BodyStorageEnricher bodyStorageEnricher,
            IEnrichImportedErrorMessages[] enrichers,
            IFailedMessageEnricher[] failedMessageEnrichers,
            ErrorIngestionCustomCheck.State ingestionState
        )
        {
            receivedMeter = metrics.GetCounter("Error ingestion - received");
            batchSizeMeter = metrics.GetMeter("Error ingestion - batch size");
            var ingestedMeter = metrics.GetCounter("Error ingestion - ingested");
            var bulkInsertDurationMeter = metrics.GetMeter("Error ingestion - bulk insert duration", FrequencyInMilliseconds);
            batchDurationMeter = metrics.GetMeter("Error ingestion - batch processing duration", FrequencyInMilliseconds);

            this.settings = settings;
            var announcer = new FailedMessageAnnouncer(domainEvents);
            persister = new ErrorPersister(documentStore, bodyStorageEnricher, enrichers, failedMessageEnrichers, ingestedMeter, bulkInsertDurationMeter);
            ingestor = new ErrorIngestor(persister, announcer, settings.ForwardErrorMessages, settings.ErrorLogQueue);

            var ingestion = new ErrorIngestion(async messageContext =>
                {
                    var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    messageContext.SetTaskCompletionSource(taskCompletionSource);

                    receivedMeter.Mark();

                    await channel.Writer.WriteAsync(messageContext).ConfigureAwait(false);
                    await taskCompletionSource.Task.ConfigureAwait(false);
                },
                dispatcher => ingestor.Initialize(dispatcher), settings.ErrorQueue, rawEndpointFactory, documentStore, loggingSettings, OnCriticalError);

            failedImporter = new ImportFailedErrors(documentStore, ingestor, rawEndpointFactory);

            watchdog = new Watchdog(ingestion.EnsureStarted, ingestion.EnsureStopped, ingestionState.ReportError,
                ingestionState.Clear, settings.TimeToRestartErrorIngestionAfterFailure, log, "failed message ingestion");

            channel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(settings.MaximumConcurrencyLevel)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            ingestionWorker = Task.Run(() => Loop(), CancellationToken.None);
        }

        Task OnCriticalError(string failure, Exception arg2)
        {
            return watchdog.OnFailure(failure);
        }

        public Task Start()
        {
            return watchdog.Start();
        }

        public async Task Stop()
        {
            await watchdog.Stop().ConfigureAwait(false);
            channel.Writer.Complete();
            await ingestionWorker.ConfigureAwait(false);
        }

        public Task ImportFailedErrors(CancellationToken cancellationToken = default)
        {
            return failedImporter.Run(cancellationToken);
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
                        await ingestor.Ingest(contexts).ConfigureAwait(false);
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
    }
}