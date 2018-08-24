namespace ServiceControl.Transports.ASBS
{
    using NServiceBus;

    static class Helper
    {
        public static void ConfigureTransport(this TransportExtensions<AzureServiceBusTransport> transport, TransportSettings transportSettings)
        {
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            transport.ConnectionString(transportSettings.ConnectionString);

        }
    }
}