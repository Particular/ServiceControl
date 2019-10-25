namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;
    using Raven.Client;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestionComponent
    {
        static ILog log = LogManager.GetLogger<ErrorIngestionComponent>();

        ImportFailedErrors failedImporter;
        Watchdog watchdog;

        public ErrorIngestionComponent(
            Settings settings,
            IDocumentStore documentStore,
            IDomainEvents domainEvents,
            RawEndpointFactory rawEndpointFactory,
            LoggingSettings loggingSettings,
            BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher,
            IEnrichImportedErrorMessages[] enrichers,
            IFailedMessageEnricher[] failedMessageEnrichers,
            ErrorIngestionCustomCheck.State ingestionState
        )
        {
            var announcer = new FailedMessageAnnouncer(domainEvents);
            var persister = new ErrorPersister(documentStore, bodyStorageEnricher, enrichers, failedMessageEnrichers);
            var ingestor = new ErrorIngestor(persister, announcer, settings.ForwardErrorMessages, settings.ErrorLogQueue);
            var ingestion = new ErrorIngestion(ingestor, settings.ErrorQueue, rawEndpointFactory, documentStore, loggingSettings, OnCriticalError);

            failedImporter = new ImportFailedErrors(documentStore, ingestor, rawEndpointFactory);

            watchdog = new Watchdog(ingestion.EnsureStarted, ingestion.EnsureStopped, ingestionState.ReportError,
                ingestionState.Clear, settings.TimeToRestartErrorIngestionAfterFailure, log, "failed message ingestion");
        }

        Task OnCriticalError(string failure, Exception arg2)
        {
            return watchdog.OnFailure(failure);
        }

        public Task Start()
        {
            return watchdog.Start();
        }

        public Task Stop()
        {
            return watchdog.Stop();
        }

        public Task ImportFailedErrors(CancellationToken cancellationToken)
        {
            return failedImporter.Run(cancellationToken);
        }
    }
}