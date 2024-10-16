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

                if (dictionary.TryGetValue("certPassphrase", out var certPassphrase))
                {
                    clientCertificate = new X509Certificate2(dictionary["certPath"], certPassphrase);
                }
                else
                {
                    clientCertificate = new X509Certificate2(dictionary["certPath"]);
                }

                connectionString = string.Join(";", dictionary
                    .Where(connectionPair => !connectionPair.Key.Equals("certPath", StringComparison.InvariantCultureIgnoreCase))
                    .Where(connectionPair => !connectionPair.Key.Equals("certPassphrase", StringComparison.InvariantCultureIgnoreCase))
                    .Select(connectionPair => $"{connectionPair.Key}={connectionPair.Value}"));
            }

            return clientCertificate;
        }
    }
}