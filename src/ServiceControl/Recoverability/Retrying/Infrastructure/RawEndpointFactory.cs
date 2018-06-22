namespace ServiceControl.Recoverability
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Raw;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NServiceBus.Unicast.Messages;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;

    public class RawEndpointFactory
    {
        Settings settings;

        public RawEndpointFactory(Settings settings)
        {
            this.settings = settings;
        }

        public RawEndpointConfiguration CreateRawEndpointConfiguration(string name, Func<MessageContext, IDispatchMessages, Task> onMessage, TransportTransactionMode transactionMode)
        {
            var config = RawEndpointConfiguration.Create(name, onMessage, $"{settings.ServiceName}.errors");
            config.AutoCreateQueue();
            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            var transport = config.UseTransport(settings.TransportType);
            
            ApplyASQSettingsHacksIfRequired(transport, settings.TransportType.FullName);

            var s = settings.TransportConnectionString;
            if (s != null)
            {
                transport.ConnectionString(s);
            }
            transport.Transactions(transactionMode);
            return config;
        }

        private static void ApplyASQSettingsHacksIfRequired(TransportExtensions extensions, string transportType)
        {
            if (transportType != "NServiceBus.AzureStorageQueueTransport")
            {
                return;
            }

            var settings = extensions.GetSettings();
            var serializer = Tuple.Create(new NewtonsoftSerializer() as SerializationDefinition, new SettingsHolder());
            settings.Set("MainSerializer", serializer);

            var ctor = typeof(MessageMetadataRegistry).GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Func<Type, bool>) }, null);
            settings.Set<MessageMetadataRegistry>(ctor.Invoke(new object[] { (Func<Type, bool>)IsMessageType }));
            
            settings.Set("Transport.AzureStorageQueue.QueueSanitizer", (Func<string, string>)AsqBackwardsCompatibleQueueNameSanitizer.Sanitize);
        }

        static bool IsMessageType(Type t)
        {
            return t.FullName == "NServiceBus.Azure.Transports.WindowsAzureStorageQueues.MessageWrapper";
        }
    }
}