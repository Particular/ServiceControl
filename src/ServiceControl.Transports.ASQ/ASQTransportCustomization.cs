namespace ServiceControl.Transports.ASQ
{
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Infrastructure.Transport;

    public class ASQTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, string connectionString)
        {
            endpointConfig.UseSerialization<NewtonsoftSerializer>();
            
            var transport = endpointConfig.UseTransport<AzureStorageQueueTransport>();
            ConfigureTransport(transport, connectionString);
            CustomizeEndpointTransport(transport);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, string connectionString)
        {
            var transport = endpointConfig.UseTransport<AzureStorageQueueTransport>();
            transport.ApplyHacksForNsbRaw();
            ConfigureTransport(transport, connectionString);
            CustomizeRawEndpointTransport(transport);
        }
        
        protected virtual void CustomizeEndpointTransport(TransportExtensions<AzureStorageQueueTransport> extensions)
        {
        }

        protected virtual void CustomizeRawEndpointTransport(TransportExtensions<AzureStorageQueueTransport> extensions)
        {
        }
        
        static void ConfigureTransport(TransportExtensions<AzureStorageQueueTransport> transport, string connectionString)
        {
            transport.SanitizeQueueNamesWith(BackwardsCompatibleQueueNameSanitizer.Sanitize);
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ConnectionString(connectionString);
        }
    }
}