﻿namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using Raven.Client.Documents;
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

        public async Task EnsureStarted(CancellationToken cancellationToken)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (ingestionEndpoint != null)
                {
                    return; //Already started
                }

                var rawConfiguration = rawEndpointFactory.CreateErrorIngestor(
                    errorQueue,
                    (messageContext, dispatcher) => errorIngestor.Ingest(messageContext));

                rawConfiguration.Settings.Set("onCriticalErrorAction", (Func<ICriticalErrorContext, Task>)OnCriticalErrorAction);

                rawConfiguration.CustomErrorHandlingPolicy(new ErrorIngestionFaultPolicy(importFailuresHandler));

                var startableRaw = await RawEndpoint.Create(rawConfiguration).ConfigureAwait(false);

                await errorIngestor.Initialize(startableRaw).ConfigureAwait(false);

                ingestionEndpoint = await startableRaw.Start()
                    .ConfigureAwait(false);
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        Task OnCriticalErrorAction(ICriticalErrorContext ctx) => onCriticalError(ctx.Error, ctx.Exception);

        public async Task EnsureStopped(CancellationToken cancellationToken)
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
        ErrorIngestor errorIngestor;
        string errorQueue;
        RawEndpointFactory rawEndpointFactory;
        Func<string, Exception, Task> onCriticalError;
        SatelliteImportFailuresHandler importFailuresHandler;

        IReceivingRawEndpoint ingestionEndpoint;
    }
}