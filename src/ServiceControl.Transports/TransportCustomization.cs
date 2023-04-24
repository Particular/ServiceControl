namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    public abstract class TransportCustomization
    {
        public abstract void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract IProvideQueueLength CreateQueueLengthProvider();

        public async Task<IDispatchMessages> InitializeDispatcher(string name, TransportSettings transportSettings)
        {
            var config = RawEndpointConfiguration.CreateSendOnly(name);

            CustomizeRawSendOnlyEndpoint(config, transportSettings);

            return await RawEndpoint.Create(config).ConfigureAwait(false);
        }

        public async Task<IQueueIngestor> InitializeQueueIngestor(
            string queueName,
            TransportSettings transportSettings,
            Func<MessageContext, Task> onMessage,
            Func<ErrorContext, Task<ErrorHandleResult>> onError,
            Func<string, Exception, Task> onCriticalError)
        {
            var config = RawEndpointConfiguration.Create(queueName, (mt, _) => onMessage(mt), transportSettings.ErrorQueue);
            config.LimitMessageProcessingConcurrencyTo(transportSettings.MaxConcurrency);

            Func<ICriticalErrorContext, Task> onCriticalErrorAction = (cet) => onCriticalError(cet.Error, cet.Exception);
            config.Settings.Set("onCriticalErrorAction", onCriticalErrorAction);

            config.CustomErrorHandlingPolicy(new IngestionErrorPolicy(onError));

            CustomizeForQueueIngestion(config, transportSettings);

            var startableRaw = await RawEndpoint.Create(config).ConfigureAwait(false);
            return new QueueIngestor(startableRaw);
        }

        public virtual Task ProvisionQueues(string username, TransportSettings transportSettings, IEnumerable<string> additionalQueues)
        {
            var config = RawEndpointConfiguration.Create(transportSettings.EndpointName, (_, __) => throw new NotImplementedException(), transportSettings.ErrorQueue);

            CustomizeForQueueIngestion(config, transportSettings);

            config.AutoCreateQueues(additionalQueues.ToArray(), username);

            //No need to start the raw endpoint to create queues
            return RawEndpoint.Create(config);
        }

        protected abstract void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        protected abstract void CustomizeForQueueIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

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