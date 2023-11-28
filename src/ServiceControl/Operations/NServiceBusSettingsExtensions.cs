namespace ServiceControl.Operations
{
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    static class NServiceBusSettingsExtensions
    {
        public static string ToTransportAddress(this IReadOnlySettings settings, string queueName)
        {
            var transportInfrastructure = settings.Get<TransportInfrastructure>();
            var logicalAddress = new QueueAddress(queueName);
            return transportInfrastructure.ToTransportAddress(logicalAddress);
        }
    }
}