namespace ServiceControl.Transports.ASB
{
    using System;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;

    static class Helper
    {
#pragma warning disable CS0618 // Type or member is obsolete
        public static void ApplyHacksForNsbRaw(this TransportExtensions<AzureServiceBusTransport> extensions)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            var settings = extensions.GetSettings();
            var serializer = Tuple.Create(new NewtonsoftSerializer() as SerializationDefinition, new SettingsHolder());
            settings.Set("MainSerializer", serializer);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public static void ConfigureTransport(this TransportExtensions<AzureServiceBusTransport> transport, TransportSettings transportSettings, TransportTransactionMode transactionMode)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            //If the custom part stays in the connection string and is at the end, the sdk will treat is as part of the SharedAccessKey
            var connectionString = ConnectionStringPartRemover.Remove(transportSettings.ConnectionString, QueueLengthProvider.QueueLengthQueryIntervalPartName);

            transport.ConnectionString(connectionString);
            transport.Transactions(transactionMode);
            transport.Queues().LockDuration(TimeSpan.FromMinutes(5));
            transport.Subscriptions().LockDuration(TimeSpan.FromMinutes(5));
            transport.MessagingFactories().NumberOfMessagingFactoriesPerNamespace(2);
            transport.NumberOfClientsPerEntity(Math.Min(Environment.ProcessorCount, transportSettings.MaxConcurrency));
        }
    }
}