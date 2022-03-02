namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Data.Common;
    using Azure.Messaging.ServiceBus;

    public static class ConnectionStringParser
    {
        public static ConnectionSettings Parse(string connectionString)
        {
            if (!connectionString.Contains("="))
            {
                if (connectionString.Contains("sb://"))
                {
                    throw new Exception("When using a fully-qualified namespace the'sb://' prefix is not allowed");
                }

                if (connectionString.EndsWith("/"))
                {
                    throw new Exception("When using a fully-qualified namespace a trailing '/' is not allowed");
                }

                return new ConnectionSettings(connectionString, false, connectionString, useDefaultCredentials: true);
            }

            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            string topicNameString = null;
            var useWebSockets = false;
            string clientIdString = null;

            if (builder.TryGetValue("TopicName", out var topicName))
            {
                topicNameString = (string)topicName;
            }

            if (builder.TryGetValue("TransportType", out var transportTypeString) && Enum.TryParse((string)transportTypeString, true, out ServiceBusTransportType transportType) && transportType == ServiceBusTransportType.AmqpWebSockets)
            {
                useWebSockets = true;
            }

            if (builder.TryGetValue("ClientId", out var clientId))
            {
                clientIdString = (string)clientId;
            }

            var hasEndpoint = builder.TryGetValue("Endpoint", out var endpoint);
            if (!hasEndpoint)
            {
                throw new Exception("The Endpoint property is mandatory on the connection string");
            }

            var shouldUseManagedIdentity = builder.TryGetValue("Authentication", out var authType) && (string)authType == "Managed Identity";
            if (shouldUseManagedIdentity)
            {
                var fullyQualifiedNamespace = endpoint.ToString().TrimEnd('/').Replace("sb://", "");
                return new ConnectionSettings(fullyQualifiedNamespace, true, fullyQualifiedNamespace, clientIdString, topicNameString, useWebSockets);
            }

            if (clientIdString != null)
            {
                throw new Exception("ClientId is only allowed when using Managed Identity (Authentication Type=Managed Identity)");
            }

            return new ConnectionSettings(connectionString, false, endpoint.ToString().TrimEnd('/').Replace("sb://", ""), null, topicNameString, useWebSockets);
        }
    }
}
