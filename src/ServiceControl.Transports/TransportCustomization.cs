namespace ServiceControl.Transports
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

        public abstract void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract IProvideQueueLength CreateQueueLengthProvider();

        public abstract Task<IQueueIngestor> InitializeIngestor(
            string queueName,
            TransportSettings transportSettings,
            Func<MessageContext, Task> onMessage,
            IErrorHandlingPolicy onError,
            Func<string, Exception, Task> onCriticalError);
    }
}