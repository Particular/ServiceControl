namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    public interface ITransportCustomization
    {
        RawEndpointConfiguration CreateRawEndpointForReturnToSenderIngestion(string name, Func<MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage, TransportSettings transportSettings);
        void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);
        void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);
        void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);
        IProvideQueueLength CreateQueueLengthProvider();
        Task<IMessageDispatcher> InitializeDispatcher(string name, TransportSettings transportSettings);

        Task<IQueueIngestor> InitializeQueueIngestor(
            string queueName,
            TransportSettings transportSettings,
            Func<MessageContext, Task> onMessage,
            Func<ErrorContext, Task<ErrorHandleResult>> onError,
            Func<string, Exception, Task> onCriticalError);

        Task ProvisionQueues(string username, TransportSettings transportSettings, IEnumerable<string> additionalQueues);
    }

    public abstract class TransportCustomization<TTransport> : ITransportCustomization where TTransport : TransportDefinition
    {
        protected abstract void CustomizeTransportSpecificServiceControlEndpointSettings(
            EndpointConfiguration endpointConfiguration, TTransport transportDefinition,
            TransportSettings transportSettings);

        protected abstract void CustomizeTransportSpecificSendOnlyEndpointSettings(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeTransportSpecificMonitoringEndpointSettings(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeForReturnToSenderIngestion(TTransport transportDefinition, TransportSettings transportSettings);

        public RawEndpointConfiguration CreateRawEndpointForReturnToSenderIngestion(string name,
            Func<MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage, TransportSettings transportSettings)
        {
            var transport = CreateTransport(transportSettings);
            CustomizeForReturnToSenderIngestion(transport, transportSettings);
            var config = RawEndpointConfiguration.Create(name, transport, onMessage, transportSettings.ErrorQueue);
            return config;
        }

        public void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
            CustomizeTransportSpecificServiceControlEndpointSettings(endpointConfiguration, transport, transportSettings);
        }

        public void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
            CustomizeTransportSpecificSendOnlyEndpointSettings(endpointConfiguration, transport, transportSettings);

            endpointConfiguration.SendOnly();

            //DisablePublishing API is available only on TransportExtensions for transports that implement IMessageDrivenPubSub so we need to set settings directly
            endpointConfiguration.GetSettings().Set("NServiceBus.PublishSubscribe.EnablePublishing", false);
        }

        public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
            CustomizeTransportSpecificMonitoringEndpointSettings(endpointConfiguration, transport, transportSettings);
        }

        protected void ConfigureDefaultEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            endpointConfiguration.DisableFeature<Audit>();
            endpointConfiguration.DisableFeature<AutoSubscribe>();
            endpointConfiguration.DisableFeature<Outbox>();
            endpointConfiguration.DisableFeature<Sagas>();
            endpointConfiguration.SendFailedMessagesTo(transportSettings.ErrorQueue);
        }

        public abstract IProvideQueueLength CreateQueueLengthProvider();

        public async Task<IMessageDispatcher> InitializeDispatcher(string name, TransportSettings transportSettings)
        {
            var transport = CreateTransport(transportSettings);
            CustomizeRawSendOnlyEndpoint(transport, transportSettings);

            // TODO NSB8 does this need to configured as SendOnly (it was previously)
            var ti = await transport.Initialize(null, null, null);
            return ti.Dispatcher;
        }

        public async Task<IQueueIngestor> InitializeQueueIngestor(
            string queueName,
            TransportSettings transportSettings,
            Func<MessageContext, Task> onMessage,
            Func<ErrorContext, Task<ErrorHandleResult>> onError,
            Func<string, Exception, Task> onCriticalError)
        {
            var transport = CreateTransport(transportSettings);
            var config = RawEndpointConfiguration.Create(queueName, transport, (mt, _, __) => onMessage(mt), transportSettings.ErrorQueue);
            config.LimitMessageProcessingConcurrencyTo(transportSettings.MaxConcurrency);

            // TODO NSB8 Pass around the cancellation token
            Task OnCriticalErrorAction(ICriticalErrorContext cet, CancellationToken cancellationToken) => onCriticalError(cet.Error, cet.Exception);

            config.CriticalErrorAction(OnCriticalErrorAction);
            config.CustomErrorHandlingPolicy(new IngestionErrorPolicy(onError));

            CustomizeForQueueIngestion(transport, transportSettings);

            var startableRaw = await RawEndpoint.Create(config);
            return new QueueIngestor(startableRaw);
        }

        public virtual Task ProvisionQueues(string username, TransportSettings transportSettings, IEnumerable<string> additionalQueues)
        {
            var transport = CreateTransport(transportSettings);
            var config = RawEndpointConfiguration.Create(transportSettings.EndpointName, transport, (_, __, ___) => throw new NotImplementedException(), transportSettings.ErrorQueue);

            CustomizeForQueueIngestion(transport, transportSettings);

            config.AutoCreateQueues(additionalQueues.ToArray());

            //No need to start the raw endpoint to create queues
            return RawEndpoint.Create(config);
        }

        protected abstract void CustomizeRawSendOnlyEndpoint(TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeForQueueIngestion(TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract TTransport CreateTransport(TransportSettings transportSettings);

        class IngestionErrorPolicy : IErrorHandlingPolicy
        {
            public IngestionErrorPolicy(Func<ErrorContext, Task<ErrorHandleResult>> onError) => this.onError = onError;

            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IMessageDispatcher dispatcher, CancellationToken cancellationToken)
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
                stoppableRaw = await startableRaw.Start();
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