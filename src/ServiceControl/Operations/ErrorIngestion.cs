namespace ServiceControl.Operations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using Raven.Client;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestion
    {
        public ErrorIngestion(ErrorIngestor errorIngestor, string errorQueue, RawEndpointFactory rawEndpointFactory, IDocumentStore documentStore, LoggingSettings loggingSettings, Func<string, Exception, Task> onCriticalError)
        {
            this.errorIngestor = errorIngestor;
            this.errorQueue = errorQueue;
            this.rawEndpointFactory = rawEndpointFactory;
            this.onCriticalError = onCriticalError;
            importFailuresHandler = new SatelliteImportFailuresHandler(documentStore, loggingSettings, onCriticalError);
        }

        public async Task Start()
        {
            var rawConfiguration = rawEndpointFactory.CreateRawEndpointConfiguration(
                errorQueue,
                (messageContext, dispatcher) => errorIngestor.Ingest(messageContext));

            rawConfiguration.Settings.Set("onCriticalErrorAction", (Func<ICriticalErrorContext, Task>)OnCriticalErrorAction);

            rawConfiguration.CustomErrorHandlingPolicy(new ErrorIngestionFaultPolicy(importFailuresHandler));

            var startableRaw = await RawEndpoint.Create(rawConfiguration).ConfigureAwait(false);

            await errorIngestor.Initialize(startableRaw).ConfigureAwait(false);

            ingestionEndpoint = await RawEndpoint.Start(rawConfiguration)
                .ConfigureAwait(false);
        }

        Task OnCriticalErrorAction(ICriticalErrorContext ctx) => onCriticalError(ctx.Error, ctx.Exception);

        public Task Stop() => ingestionEndpoint.Stop();

        ErrorIngestor errorIngestor;
        string errorQueue;
        RawEndpointFactory rawEndpointFactory;
        Func<string, Exception, Task> onCriticalError;
        SatelliteImportFailuresHandler importFailuresHandler;

        IReceivingRawEndpoint ingestionEndpoint;
    }
}