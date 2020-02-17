namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    
    class AuditIngestionComponent
    {
        static ILog log = LogManager.GetLogger<AuditIngestionComponent>();

        ImportFailedAudits failedImporter;
        Watchdog watchdog;
        AuditPersister auditPersister;

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
            var errorHandlingPolicy = new AuditIngestionFaultPolicy(documentStore, loggingSettings, FailedMessageFactory, OnCriticalError);

            auditPersister = new AuditPersister(documentStore, bodyStorageEnricher, enrichers);
            var ingestor = new AuditIngestor(auditPersister, settings);

            var ingestion = new AuditIngestion(
                (messageContext, dispatcher) => ingestor.Ingest(messageContext), 
                dispatcher => ingestor.Initialize(dispatcher),
                settings.AuditQueue, rawEndpointFactory, errorHandlingPolicy, OnCriticalError);

            failedImporter = new ImportFailedAudits(documentStore, ingestor, rawEndpointFactory);

            watchdog = new Watchdog(ingestion.EnsureStarted, ingestion.EnsureStopped, ingestionState.ReportError,
                ingestionState.Clear, settings.TimeToRestartAuditIngestionAfterFailure, log, "failed message ingestion");

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