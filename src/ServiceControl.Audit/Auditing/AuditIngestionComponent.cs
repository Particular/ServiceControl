namespace ServiceControl.Audit.Auditing
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using Infrastructure.Settings;
    using Monitoring;
    using NServiceBus;
    using Raven.Client;
    using Recoverability;
    using SagaAudit;
    using ServiceControl.Infrastructure.Metrics;

    class AuditIngestionComponent
    {
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        ImportFailedAudits failedImporter;
        AuditPersister auditPersister;
        AuditIngestor ingestor;
        readonly AuditIngestion ingestion;

        public AuditIngestionComponent(
            Metrics metrics,
            Settings settings,
            IDocumentStore documentStore,
            IBodyStorage bodyStorage,
            RawEndpointFactory rawEndpointFactory,
            LoggingSettings loggingSettings,
            AuditIngestionCustomCheck.State ingestionState,
            EndpointInstanceMonitoring endpointInstanceMonitoring,
            IEnumerable<IEnrichImportedAuditMessages> auditEnrichers, // allows extending message enrichers with custom enrichers registered in the DI container
            IMessageSession messageSession
        )
        {
            var ingestedAuditMeter = metrics.GetCounter("Audit ingestion - ingested audit");
            var ingestedSagaAuditMeter = metrics.GetCounter("Audit ingestion - ingested saga audit");
            var auditBulkInsertDurationMeter = metrics.GetMeter("Audit ingestion - audit bulk insert duration", FrequencyInMilliseconds);
            var sagaAuditBulkInsertDurationMeter = metrics.GetMeter("Audit ingestion - saga audit bulk insert duration", FrequencyInMilliseconds);
            var bulkInsertCommitDurationMeter = metrics.GetMeter("Audit ingestion - bulk insert commit duration", FrequencyInMilliseconds);

            var enrichers = new IEnrichImportedAuditMessages[]
            {
                new MessageTypeEnricher(),
                new EnrichWithTrackingIds(),
                new ProcessingStatisticsEnricher(),
                new DetectNewEndpointsFromAuditImportsEnricher(endpointInstanceMonitoring),
                new DetectSuccessfulRetriesEnricher(),
                new SagaRelationshipsEnricher()
            }.Concat(auditEnrichers).ToArray();

            var bodyStorageEnricher = new BodyStorageEnricher(bodyStorage, settings);
            auditPersister = new AuditPersister(documentStore, bodyStorageEnricher, enrichers, ingestedAuditMeter, ingestedSagaAuditMeter, auditBulkInsertDurationMeter, sagaAuditBulkInsertDurationMeter, bulkInsertCommitDurationMeter, messageSession);
            ingestor = new AuditIngestor(auditPersister, settings);

            ingestion = new AuditIngestion(settings, rawEndpointFactory, ingestor, metrics, documentStore, loggingSettings, ingestionState);
            failedImporter = new ImportFailedAudits(documentStore, ingestor, rawEndpointFactory);
        }

        public Task Start() => ingestion.Start();

        public Task Stop() => ingestion.Stop();

        public Task ImportFailedAudits(CancellationToken cancellationToken = default) => failedImporter.Run(cancellationToken);
    }
}
