namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Client;

    class AuditIngestionComponent
    {
        static ILog log = LogManager.GetLogger<AuditIngestionComponent>();

        ImportFailedAudits failedImporter;
        Watchdog watchdog;
        AuditPersister auditPersister;
        Channel<MessageContext> channel;
        AuditIngestor ingestor;
        Task ingestionWorker;
        Settings settings;

        public AuditIngestionComponent(
            Settings settings,
            IDocumentStore documentStore,
            RawEndpointFactory rawEndpointFactory,
            LoggingSettings loggingSettings,
            BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher,
            IEnrichImportedAuditMessages[] enrichers,
            AuditIngestionCustomCheck.State ingestionState
        )
        {
            this.settings = settings;
            var errorHandlingPolicy = new AuditIngestionFaultPolicy(documentStore, loggingSettings, FailedMessageFactory, OnCriticalError);
            auditPersister = new AuditPersister(documentStore, bodyStorageEnricher, enrichers);
            ingestor = new AuditIngestor(auditPersister, settings);

            var ingestion = new AuditIngestion(
                async (messageContext, dispatcher) =>
                {
                    var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    messageContext.SetTaskCompletionSource(taskCompletionSource);

                    await channel.Writer.WriteAsync(messageContext).ConfigureAwait(false);
                    await taskCompletionSource.Task.ConfigureAwait(false);
                },
                dispatcher => ingestor.Initialize(dispatcher),
                settings.AuditQueue, rawEndpointFactory, errorHandlingPolicy, OnCriticalError);

            failedImporter = new ImportFailedAudits(documentStore, ingestor, rawEndpointFactory);

            watchdog = new Watchdog(ingestion.EnsureStarted, ingestion.EnsureStopped, ingestionState.ReportError,
                ingestionState.Clear, settings.TimeToRestartAuditIngestionAfterFailure, log, "failed message ingestion");

            channel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(settings.MaximumConcurrencyLevel)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            ingestionWorker = Task.Run(() => Loop(), CancellationToken.None);
        }

        FailedAuditImport FailedMessageFactory(FailedTransportMessage msg)
        {
            return new FailedAuditImport
            {
                Message = msg
            };
        }

        Task OnCriticalError(string failure, Exception arg2)
        {
            return watchdog.OnFailure(failure);
        }

        public Task Start(IMessageSession session)
        {
            auditPersister.Initialize(session);
            return watchdog.Start();
        }

        public async Task Stop()
        {
            await watchdog.Stop().ConfigureAwait(false);
            channel.Writer.Complete();
            await ingestionWorker.ConfigureAwait(false);
        }

        async Task Loop()
        {
            var firstSweep = new List<MessageContext>(settings.MaximumConcurrencyLevel);
            var secondSweep = new List<MessageContext>(settings.MaximumConcurrencyLevel);

            while (await channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                // will only enter here if there is something to read.
                try
                {
                    // as long as there is something to read this will fetch up to MaximumConcurrency items
                    while (channel.Reader.TryRead(out var context))
                    {
                        firstSweep.Add(context);
                    }

                    // we do this to interleave the bulk ingestion. This primarily is powerful with transport that
                    // offer high throughput. What we have seen with transports with high throughput is that the
                    // ingestion dances around have the concurrency and due to that the other half that would like to be
                    // ingested would have to wait until the first half is completed. By interleaving we achieve higher
                    // throughput
                    var firstIngestion = ingestor.Ingest(firstSweep);

                    // as long as there is something to read this will fetch up to MaximumConcurrency items
                    var secondReadSuccessful = false;
                    while (channel.Reader.TryRead(out var context))
                    {
                        secondReadSuccessful = true;
                        secondSweep.Add(context);
                    }

                    var secondIngestion = secondReadSuccessful ?
                        ingestor.Ingest(secondSweep) : Task.CompletedTask;

                    await Task.WhenAll(firstIngestion, secondIngestion).ConfigureAwait(false);
                }
                finally
                {
                    firstSweep.Clear();
                    secondSweep.Clear();
                }
            }
            // will fall out here when writer is completed
        }

        public Task ImportFailedErrors(CancellationToken cancellationToken)
        {
            return failedImporter.Run(cancellationToken);
        }
    }
}