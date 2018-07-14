namespace ServiceControl.Transports.ASQ
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASQTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {           
            var transport = endpointConfig.UseTransport<AzureStorageQueueTransport>();
            ConfigureTransport(transport, transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureStorageQueueTransport>();
            transport.ApplyHacksForNsbRaw();
            ConfigureTransport(transport, transportSettings);
        }
                
        static void ConfigureTransport(TransportExtensions<AzureStorageQueueTransport> transport, TransportSettings transportSettings)
        {
            transport.SanitizeQueueNamesWith(BackwardsCompatibleQueueNameSanitizer.Sanitize);
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ConnectionString(transportSettings.ConnectionString);
        }
    }
}