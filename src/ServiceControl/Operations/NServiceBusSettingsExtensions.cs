namespace ServiceControl.Operations
{
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    public static class NServiceBusSettingsExtensions
    {
        public static string ToTransportAddress(this ReadOnlySettings settings, string queueName)
        {
            var transportInfrastructure = settings.Get<TransportInfrastructure>();
            var logicalAddress = LogicalAddress.CreateLocalAddress(queueName, null);
            return transportInfrastructure.ToTransportAddress(logicalAddress);
        }
    }
}