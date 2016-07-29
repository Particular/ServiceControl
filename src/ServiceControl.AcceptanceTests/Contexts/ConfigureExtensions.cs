namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System.Collections.Generic;
    using NServiceBus;
    using TransportIntegration;

    public static class ConfigureExtensions
    {
        public static string GetOrNull(this IDictionary<string, string> dictionary, string key)
        {
            if (!dictionary.ContainsKey(key))
            {
                return null;
            }

            return dictionary[key];
        }

        public static void DefineTransport(this BusConfiguration config, ITransportIntegration transport)
        {
            var transportDefinitionType = transport.Type;
            var connectionString = transport.ConnectionString;

            if (connectionString == null)
            {
                config.UseTransport(transportDefinitionType);
                return;
            }

            config.UseTransport(transportDefinitionType).ConnectionString(connectionString);
        }
    }
}