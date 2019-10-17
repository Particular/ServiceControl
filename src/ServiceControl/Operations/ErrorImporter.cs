namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.CustomChecks;
    using NServiceBus.Features;
    using Raven.Client;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestionCustomCheck : CustomCheck
    {
        readonly CriticalErrorHolder criticalErrorHolder;

        public ErrorIngestionCustomCheck(CriticalErrorHolder criticalErrorHolder) 
            : base("Error Ingestion", "Health", TimeSpan.FromMinutes(1))
        {
            this.criticalErrorHolder = criticalErrorHolder;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var failure = criticalErrorHolder.GetLastFailureAndClear();
            if (failure == null)
            {
                return Task.FromResult(CheckResult.Pass);
            }

            return Task.FromResult(CheckResult.Failed(failure));
        }
    }

    class CriticalErrorHolder
    {
        volatile string lastFailure;

        public void Clear() => lastFailure = null;
        public void ReportError(string failure) => lastFailure = failure;
        public string GetLastFailureAndClear()
        {
            var failure = lastFailure;
            lastFailure = null;
            return failure;
        }
    }

    class ErrorIngestionComponent
    {
        CriticalErrorHolder criticalErrorHolder;
        ImportFailedErrors failedImporter;
        ErrorIngestion ingestion;
        object startStopLock = new object();

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
            var announcer = new FailedMessageAnnouncer(domainEvents);
            var persister = new ErrorPersister(documentStore, bodyStorageEnricher, enrichers, failedMessageEnrichers);
            var ingestor = new ErrorIngestor(persister, announcer, settings.ForwardErrorMessages, settings.ErrorLogQueue);
            failedImporter = new ImportFailedErrors(documentStore, ingestor, rawEndpointFactory);
            ingestion = new ErrorIngestion(ingestor, settings.ErrorQueue, rawEndpointFactory, documentStore, loggingSettings, OnCriticalError);
        }

        async Task OnCriticalError(string failure, Exception arg2)
        {
            //Cannot deadlock because CriticalError used in NServiceBus.Raw  raises errors on a separate thread.
            //lock (startStopLock)
            {
                criticalErrorHolder.ReportError(failure);
                await ingestion.Stop().ConfigureAwait(false);
            }
        }

        public Task Start()
        {
            lock (startStopLock)
            {
                return ingestion.Start();
            }
        }

        public Task Stop()
        {
            lock (startStopLock)
            {
                return ingestion.Stop();
            }
        }

        public Task ImportFailedErrors(CancellationToken cancellationToken)
        {
            return failedImporter.Run(cancellationToken);
        }
    }

    class ErrorImporter : Feature
    {
        public ErrorImporter()
        {
            EnableByDefault();
            Prerequisite(c =>
            {
                var settings = c.Settings.Get<Settings>("ServiceControl.Settings");
                return settings.IngestErrorMessages;
            }, "Ingestion of failed messages has been disabled.");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ErrorIngestor>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ErrorPersister>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<FailedMessageAnnouncer>(DependencyLifecycle.SingleInstance);
            context.RegisterStartupTask(b => new StartupTask(b.Build<ErrorIngestionComponent>()));
        }

        class StartupTask : FeatureStartupTask
        {
            readonly ErrorIngestionComponent errorIngestion;

            public StartupTask(ErrorIngestionComponent errorIngestion)
            {
                this.errorIngestion = errorIngestion;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return errorIngestion.Start();
            }

            protected override Task OnStop(IMessageSession session)
            {
                return errorIngestion.Stop();
            }
        }
    }
}