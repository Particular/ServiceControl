namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Settings;
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

        public static Configure DefineTransport(this Configure config, ITransportIntegration transport)
        {
            var transportDefinitionType = typeof(Msmq);
            string connectionString = null;

            if (transport != null)
            {
                transportDefinitionType = transport.Type;
                connectionString = transport.ConnectionString;
            }

            Action action = () => transport.Cleanup(transport);
            SettingsHolder.Set("CleanupTransport", action);

            if (connectionString == null)
            {
                return config.UseTransport(transportDefinitionType);
            }

            return config.UseTransport(transportDefinitionType, () => connectionString);
        }

        public static Configure DefineBuilder(this Configure config, string builder)
        {
            if (string.IsNullOrEmpty(builder))
            {
                return config.DefaultBuilder();
            }

            throw new InvalidOperationException("Unknown builder:" + builder);
        }
    }
}