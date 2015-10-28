namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
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

            Action action = () => transport.OnEndpointShutdown(config.GetSettings().EndpointName());
            config.GetSettings().Set("CleanupTransport", action);

            if (connectionString == null)
            {
                config.UseTransport(transportDefinitionType);
                return;
            }

            config.UseTransport(transportDefinitionType).ConnectionString(connectionString);
        }
    }
}