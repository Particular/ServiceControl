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

    public abstract class TransportCustomization
    {
        protected abstract void CustomizeTransportSpecificServiceControlEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        protected abstract void CustomizeTransportSpecificSendOnlyEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        protected abstract void CustomizeTransportSpecificMonitoringEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            CustomizeTransportSpecificServiceControlEndpointSettings(endpointConfiguration, transportSettings);
        }

        public void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            CustomizeTransportSpecificSendOnlyEndpointSettings(endpointConfiguration, transportSettings);

            endpointConfiguration.SendOnly();

            //DisablePublishing API is available only on TransportExtensions for transports that implement IMessageDrivenPubSub so we need to set settings directly
            endpointConfiguration.GetSettings().Set("NServiceBus.PublishSubscribe.EnablePublishing", false);
        }

        public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            CustomizeTransportSpecificMonitoringEndpointSettings(endpointConfiguration, transportSettings);
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
            // TODO NSB8 TransportDefinition temporarily set to null
            var config = RawEndpointConfiguration.CreateSendOnly(name, default);

            CustomizeRawSendOnlyEndpoint(config, transportSettings);

            return await RawEndpoint.Create(config);
        }

        public async Task<IQueueIngestor> InitializeQueueIngestor(
            string queueName,
            TransportSettings transportSettings,
            Func<MessageContext, Task> onMessage,
            Func<ErrorContext, Task<ErrorHandleResult>> onError,
            Func<string, Exception, Task> onCriticalError)
        {
            // TODO NSB8 TransportDefinition temporarily set to null
            var config = RawEndpointConfiguration.Create(queueName, default, (mt, _, __) => onMessage(mt), transportSettings.ErrorQueue);
            config.LimitMessageProcessingConcurrencyTo(transportSettings.MaxConcurrency);

            // TODO NSB8 Critical error is no longer there but since we move towards the Raw seam it probably doesnt matter
            //Func<ICriticalErrorContext, Task> onCriticalErrorAction = cet => onCriticalError(cet.Error, cet.Exception);
            //config.Settings.Set("onCriticalErrorAction", onCriticalErrorAction);

            config.CustomErrorHandlingPolicy(new IngestionErrorPolicy(onError));

            CustomizeForQueueIngestion(config, transportSettings);

            var startableRaw = await RawEndpoint.Create(config);
            return new QueueIngestor(startableRaw);
        }

        public virtual Task ProvisionQueues(string username, TransportSettings transportSettings, IEnumerable<string> additionalQueues)
        {
            // TODO NSB8 TransportDefinition temporarily set to null
            var config = RawEndpointConfiguration.Create(transportSettings.EndpointName, default, (_, __, ___) => throw new NotImplementedException(), transportSettings.ErrorQueue);

            CustomizeForQueueIngestion(config, transportSettings);

            config.AutoCreateQueues(additionalQueues.ToArray());

            //No need to start the raw endpoint to create queues
            return RawEndpoint.Create(config);
        }

        protected abstract void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        protected abstract void CustomizeForQueueIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

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