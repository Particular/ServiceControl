namespace ServiceControl.AcceptanceTests.Contexts
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Serializers.Binary;
    using NServiceBus.Serializers.Json;
    using NServiceBus.Serializers.XML;

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

        public static Configure DefineTransport(this Configure config, string transport)
        {
            if (string.IsNullOrEmpty(transport))
            {
                return config.UseTransport<Msmq>();
            }

            var transportType = Type.GetType(transport);

            return config.UseTransport(transportType);
        }

        public static Configure DefineSerializer(this Configure config, string serializer)
        {
            if (string.IsNullOrEmpty(serializer))
            {
                Configure.Serialization.Xml();
                return config;
            }

            var type = Type.GetType(serializer);

            if (type == typeof(XmlMessageSerializer))
            {
                Configure.Serialization.Xml();
                return config;
            }


            if (type == typeof(JsonMessageSerializer))
            {
                Configure.Serialization.Json();
                return config;
            }

            if (type == typeof(BsonMessageSerializer))
            {
                Configure.Serialization.Bson();
                return config;
            }

            if (type == typeof(BinaryMessageSerializer))
            {
                Configure.Serialization.Binary();
                return config;
            }

            throw new InvalidOperationException("Unknown serializer:" + serializer);
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