namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Transports;

    class AuditIngestion
    {
        public AuditIngestion(
            Func<MessageContext, IDispatchMessages, Task> onMessage,
            Func<IReadOnlyCollection<BatchMessage>, Task> onBatches,
            Func<IDispatchMessages, Task> initialize,
            string inputEndpoint,
            RawEndpointFactory rawEndpointFactory,
            TransportCustomization transportCustomization,
            TransportSettings transportSettings,
            IErrorHandlingPolicy errorHandlingPolicy,
            Func<string, Exception, Task> onCriticalError)
        {
            this.transportSettings = transportSettings;
            this.onBatches = onBatches;
            this.transportCustomization = transportCustomization;
            this.onMessage = onMessage;
            this.initialize = initialize;
            this.inputEndpoint = inputEndpoint;
            this.rawEndpointFactory = rawEndpointFactory;
            this.errorHandlingPolicy = errorHandlingPolicy;
            this.onCriticalError = onCriticalError;
        }

        public async Task EnsureStarted(CancellationToken cancellationToken)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (ingestionEndpoint != null || batchConsumer != null)
                {
                    return; //Already started
                }

                batchConsumer = transportCustomization.CreateBatchConsumer();
                batchConsumer?.Start(onBatches, inputEndpoint, transportSettings.ConnectionString);

                if (batchConsumer != null)
                {
                    return;
                }

                var rawConfiguration = rawEndpointFactory.CreateAuditIngestor(inputEndpoint, onMessage);

                rawConfiguration.Settings.Set("onCriticalErrorAction", (Func<ICriticalErrorContext, Task>)OnCriticalErrorAction);

                rawConfiguration.CustomErrorHandlingPolicy(errorHandlingPolicy);

                var startableRaw = await RawEndpoint.Create(rawConfiguration).ConfigureAwait(false);

                await initialize(startableRaw).ConfigureAwait(false);

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

                if (ingestionEndpoint == null && batchConsumer == null)
                {
                    return; //Already stopped
                }
                var stoppableEndpoint = ingestionEndpoint;
                ingestionEndpoint = null;
                if (stoppableEndpoint != null)
                {
                    await stoppableEndpoint.Stop().ConfigureAwait(false);
                    return;
                }

                var stoppableBatchConsumer = batchConsumer;
                batchConsumer = null;
                if (stoppableBatchConsumer != null)
                {
                    await stoppableBatchConsumer.Stop().ConfigureAwait(false);
                }
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        SemaphoreSlim startStopSemaphore = new SemaphoreSlim(1);
        Func<MessageContext, IDispatchMessages, Task> onMessage;
        Func<IDispatchMessages, Task> initialize;
        string inputEndpoint;
        RawEndpointFactory rawEndpointFactory;
        IErrorHandlingPolicy errorHandlingPolicy;
        Func<string, Exception, Task> onCriticalError;
        IReceivingRawEndpoint ingestionEndpoint;
        readonly TransportCustomization transportCustomization;
        Func<IReadOnlyCollection<BatchMessage>, Task> onBatches;
        TransportSettings transportSettings;
        IConsumeBatches batchConsumer;
    }
}