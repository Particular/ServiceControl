namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Data.Common;
    using Azure.Identity;
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

                return new ConnectionSettings(new TokenCredentialAuthentication(connectionString, new DefaultAzureCredential()));
            }

            TimeSpan? queryDelayInterval = null;
            string topicNameString = null;
            var useWebSockets = false;

            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            if (builder.TryGetValue("QueueLengthQueryDelayInterval", out var value))
            {
                if (!int.TryParse(value.ToString(), out var delayInterval))
                {
                    throw new Exception($"Can't parse {value} as a valid query delay interval.");
                }
                queryDelayInterval = TimeSpan.FromMilliseconds(delayInterval);
            }

            if (builder.TryGetValue("TopicName", out var topicName))
            {
                topicNameString = (string)topicName;
            }

            if (builder.TryGetValue("TransportType", out var transportTypeString) && Enum.TryParse((string)transportTypeString, true, out ServiceBusTransportType transportType) && transportType == ServiceBusTransportType.AmqpWebSockets)
            {
                useWebSockets = true;
            }


            var hasEndpoint = builder.TryGetValue("Endpoint", out var endpoint);
            if (!hasEndpoint)
            {
                throw new Exception("The Endpoint property is mandatory on the connection string");
            }

            string clientIdString = null;

            if (builder.TryGetValue("ClientId", out var clientId))
            {
                clientIdString = (string)clientId;
            }

            var shouldUseManagedIdentity = builder.TryGetValue("Authentication", out var authType) && (string)authType == "Managed Identity";
            if (shouldUseManagedIdentity)
            {
                var fullyQualifiedNamespace = endpoint.ToString().TrimEnd('/').Replace("sb://", "");

                return new ConnectionSettings(
                  new TokenCredentialAuthentication(fullyQualifiedNamespace, new ManagedIdentityCredential(clientId: clientIdString)),
                  topicNameString,
                  useWebSockets,
                  queryDelayInterval);
            }

            if (clientIdString != null)
            {
                throw new Exception("ClientId is only allowed when using Managed Identity (Authentication Type=Managed Identity)");
            }

            return new ConnectionSettings(
                new SharedAccessSignatureAuthentication(connectionString),
                topicNameString,
                useWebSockets,
                queryDelayInterval);
        }
    }
}
