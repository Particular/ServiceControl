namespace ServiceControl.Transports.ASB
{
    using System;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;

    static class Helper
    {
        public static void ApplyHacksForNsbRaw(this TransportExtensions<AzureServiceBusTransport> extensions)
        {
            var settings = extensions.GetSettings();
            var serializer = Tuple.Create(new NewtonsoftSerializer() as SerializationDefinition, new SettingsHolder());
            settings.Set("MainSerializer", serializer);
        }

        public static void ConfigureTransport(this TransportExtensions<AzureServiceBusTransport> transport, TransportSettings transportSettings)
        {
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            transport.ConnectionString(transportSettings.ConnectionString);

            transport.MessageReceivers().PrefetchCount(0);
            transport.Queues().LockDuration(TimeSpan.FromMinutes(5));
            transport.Subscriptions().LockDuration(TimeSpan.FromMinutes(5));
            transport.MessagingFactories().NumberOfMessagingFactoriesPerNamespace(2);
            transport.NumberOfClientsPerEntity(Math.Min(Environment.ProcessorCount, transportSettings.MaxConcurrency));
        }
    }
}