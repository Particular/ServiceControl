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

        CriticalErrorHolder criticalErrorHolder;
        ImportFailedErrors failedImporter;
        ErrorIngestion ingestion;
        Task watchdog;
        CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
        TimeSpan timeToWaitBetweenStartupAttempts;

        public ErrorIngestionComponent(
            Settings settings,
            IDocumentStore documentStore,
            IDomainEvents domainEvents,
            RawEndpointFactory rawEndpointFactory,
            LoggingSettings loggingSettings,
            BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher,
            IEnrichImportedErrorMessages[] enrichers,
            IFailedMessageEnricher[] failedMessageEnrichers,
            CriticalErrorHolder criticalErrorHolder
        )
        {
            this.criticalErrorHolder = criticalErrorHolder;
            timeToWaitBetweenStartupAttempts = settings.TimeToRestartAfterCriticalFailure;
            var announcer = new FailedMessageAnnouncer(domainEvents);
            var persister = new ErrorPersister(documentStore, bodyStorageEnricher, enrichers, failedMessageEnrichers);
            var ingestor = new ErrorIngestor(persister, announcer, settings.ForwardErrorMessages, settings.ErrorLogQueue);
            failedImporter = new ImportFailedErrors(documentStore, ingestor, rawEndpointFactory);
            ingestion = new ErrorIngestion(ingestor, settings.ErrorQueue, rawEndpointFactory, documentStore, loggingSettings, OnCriticalError);
        }

        async Task OnCriticalError(string failure, Exception arg2)
        {
            criticalErrorHolder.ReportError(failure);
            await ingestion.EnsureStopped().ConfigureAwait(false);
        }

        public Task Start()
        {
            watchdog = Task.Run(async () =>
            {
                while (!shutdownTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        await ingestion.EnsureStarted().ConfigureAwait(false);
                        criticalErrorHolder.Clear();
                    }
                    catch (OperationCanceledException)
                    {
                        //Do not Delay
                        continue;
                    }
                    catch (Exception e)
                    {
                        log.Error($"Error while trying to start failed message ingestion. Starting will be retried in {timeToWaitBetweenStartupAttempts}.", e);
                        criticalErrorHolder.ReportError(e.Message);
                    }
                    await Task.Delay(timeToWaitBetweenStartupAttempts, shutdownTokenSource.Token).ConfigureAwait(false);
                }

                try
                {
                    await ingestion.EnsureStopped().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    log.Error("Error while trying to stop failed message ingestion", e);
                    criticalErrorHolder.ReportError(e.Message);
                }
            });
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            shutdownTokenSource.Cancel();
            return watchdog;
        }

        public Task ImportFailedErrors(CancellationToken cancellationToken)
        {
            return failedImporter.Run(cancellationToken);
        }
    }
}