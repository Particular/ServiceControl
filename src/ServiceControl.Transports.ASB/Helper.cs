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
    }
}