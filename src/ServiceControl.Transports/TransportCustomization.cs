﻿namespace ServiceControl.Transports
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    public abstract class TransportCustomization
    {
        public abstract void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForQueueIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract IProvideQueueLength CreateQueueLengthProvider();

        public async Task<IQueueIngestor> InitializeQueueIngestor(
            string queueName,
            TransportSettings transportSettings,
            int maximumConcurrencyLevel,
            Func<MessageContext, Task> onMessage,
            Func<ErrorContext, Task<ErrorHandleResult>> onError,
            Func<string, Exception, Task> onCriticalError)
        {
            var config = RawEndpointConfiguration.Create(queueName, (mt, _) => onMessage(mt), $"{transportSettings.EndpointName}.Errors");
            config.LimitMessageProcessingConcurrencyTo(maximumConcurrencyLevel);

            Func<ICriticalErrorContext, Task> onCriticalErrorAction = (cet) => onCriticalError(cet.Error, cet.Exception);
            config.Settings.Set("onCriticalErrorAction", onCriticalErrorAction);

            config.CustomErrorHandlingPolicy(new IngestionErrorPolicy(onError));

            CustomizeForQueueIngestion(config, transportSettings);

            var startableRaw = await RawEndpoint.Create(config).ConfigureAwait(false);
            return new QueueIngestor(startableRaw);
        }

        public async Task<IQueueIngestor> CreateReturnToSenderDequeuer(
            string queueName,
            TransportSettings transportSettings,
            int maximumConcurrencyLevel,
            Func<MessageContext, IDispatchMessages, Task> onMessage,
            Func<ErrorContext, Task<ErrorHandleResult>> onError,
            Func<string, Exception, Task> onCriticalError)
        {
            var config = RawEndpointConfiguration.Create(queueName, onMessage, $"{transportSettings.EndpointName}.Errors");
            config.LimitMessageProcessingConcurrencyTo(maximumConcurrencyLevel);

            Func<ICriticalErrorContext, Task> onCriticalErrorAction = (cet) => onCriticalError(cet.Error, cet.Exception);
            config.Settings.Set("onCriticalErrorAction", onCriticalErrorAction);

            config.CustomErrorHandlingPolicy(new IngestionErrorPolicy(onError));

            CustomizeForQueueIngestion(config, transportSettings);

            var startableRaw = await RawEndpoint.Create(config).ConfigureAwait(false);
            return new QueueIngestor(startableRaw);
        }

        class IngestionErrorPolicy : IErrorHandlingPolicy
        {
            public IngestionErrorPolicy(Func<ErrorContext, Task<ErrorHandleResult>> onError) => this.onError = onError;

            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                return onError(handlingContext.Error);
            }

            readonly Func<ErrorContext, Task<ErrorHandleResult>> onError;
        }

        class QueueIngestor : IQueueIngestor
        {

            public QueueIngestor(IStartableRawEndpoint startableRaw) => this.startableRaw = startableRaw;

            public async Task Start()
            {
                stoppableRaw = await startableRaw.Start().ConfigureAwait(false);
            }

            public Task Stop()
            {
                if (stoppableRaw != null)
                {
                    return stoppableRaw.Stop();
                }

                return Task.CompletedTask;
            }

            IStoppableRawEndpoint stoppableRaw;
            readonly IStartableRawEndpoint startableRaw;
        }
    }
}