namespace ServiceControl.Transports.RabbitMQ
{
    using System.Collections.Generic;
    using System.Data.Common;
    using System;
    using System.Security.Cryptography.X509Certificates;
    using NServiceBus;
    using System.Linq;

    public static class TransportCustomizationExtensionMethods
    {
        public static X509Certificate2 ExtractClientCertificate(this TransportCustomization<RabbitMQTransport> transportCustomization, ref string connectionString)
        {
            X509Certificate2 clientCertificate = null;
            if (connectionString.Contains("certPath=", StringComparison.InvariantCultureIgnoreCase))
            {
                var dictionary = new DbConnectionStringBuilder { ConnectionString = connectionString }
                    .OfType<KeyValuePair<string, object>>()
                    .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);

                clientCertificate = new X509Certificate2(dictionary["certPath"]);
                connectionString = string.Join(", ", dictionary.Select(kv => $"{kv.Key}={kv.Value}"));
            }

            return clientCertificate;
        }
    }
}