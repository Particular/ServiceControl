namespace ServiceControl.Transports.ASQ
{
    using System;
    using System.Reflection;
    using NServiceBus;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;
    using NServiceBus.Unicast.Messages;

    static class Helper
    {
        public static void ApplyHacksForNsbRaw(this TransportExtensions<AzureStorageQueueTransport> extensions)
        {
            var settings = extensions.GetSettings();
            var serializer = Tuple.Create(new NewtonsoftSerializer() as SerializationDefinition, new SettingsHolder());
            settings.Set("MainSerializer", serializer);

            var ctor = typeof(MessageMetadataRegistry).GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Func<Type, bool>) }, null);
            settings.Set<MessageMetadataRegistry>(ctor.Invoke(new object[] { (Func<Type, bool>)IsMessageType }));
        }
        
        static bool IsMessageType(Type t) => t == typeof(MessageWrapper);
    }
}