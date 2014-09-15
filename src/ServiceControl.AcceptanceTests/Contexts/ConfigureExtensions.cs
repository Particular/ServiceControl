namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
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

        public static Configure DefineTransport(this Configure config, ITransportIntegration transport)
        {
            if (transport == null)
            {
                return config.UseTransport<Msmq>();
            }

            return config.UseTransport(transport.Type, () => transport.ConnectionString);
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