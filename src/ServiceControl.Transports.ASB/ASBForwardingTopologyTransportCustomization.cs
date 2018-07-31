namespace ServiceControl.Transports.ASB
{
    using System;
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBForwardingTopologyTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            ConfigureTransport(transport, transportSettings);

            endpointConfig.LimitMessageProcessingConcurrencyTo(Math.Min(Environment.ProcessorCount, transportSettings.MaxConcurrency));
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.ApplyHacksForNsbRaw();
            ConfigureTransport(transport, transportSettings);

            endpointConfig.LimitMessageProcessingConcurrencyTo(Math.Min(Environment.ProcessorCount, transportSettings.MaxConcurrency));
        }

        static void ConfigureTransport(TransportExtensions<AzureServiceBusTransport> transport, TransportSettings transportSettings)
        {
            transport.UseForwardingTopology();
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            transport.ConnectionString(transportSettings.ConnectionString);
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();

            transport.MessageReceivers().PrefetchCount(0);
            transport.MessageReceivers().AutoRenewTimeout(TimeSpan.FromMinutes(5));
            transport.MessagingFactories().NumberOfMessagingFactoriesPerNamespace(2);
        }
    }
}