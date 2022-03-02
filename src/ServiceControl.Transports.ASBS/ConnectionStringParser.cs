namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Data.Common;

    public class ConnectionStringParser
    {
        public ConnectionSettings Parse(string connectionString)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            var hasEndpoint = builder.TryGetValue("Endpoint", out var endpoint);
            if (!hasEndpoint)
            {
                throw new Exception("Endpoint property is mandatory on the connection string");
            }

            return new ConnectionSettings(connectionString, false, endpoint.ToString().TrimEnd('/').Replace("sb://", ""));
        }


        //static string TopicNamePart = "TopicName";
        //static string TransportTypePart = "TransportType";
        /*
         *
         *
         *  var connectionString = transportSettings.ConnectionString;

            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            var shouldUseManagedIdentity = builder.TryGetValue("Authentication", out var authType) && (string)authType == "Managed Identity";

            if (shouldUseManagedIdentity)
            {
                if (builder.TryGetValue("ClientId", out var clientId))
                {
                    transport.CustomTokenCredential(new ManagedIdentityCredential((string)clientId));
                }
                else
                {
                    transport.CustomTokenCredential(new ManagedIdentityCredential());
                }

                var hasEndpoint = builder.TryGetValue("Endpoint", out var endpoint);
                if (!hasEndpoint)
                {
                    throw new Exception("Endpoint property is mandatory on the connection string");
                }

                var fullyQualifiedNamespace = endpoint.ToString().TrimEnd('/').Replace("sb://", "");
                transport.ConnectionString(fullyQualifiedNamespace);
            }
            else
            {
                transport.ConnectionString(transportSettings.ConnectionString);
            }
                    if (builder.TryGetValue(TopicNamePart, out var topicName))
            {
                transport.TopicName((string)topicName);
            }

            if (builder.TryGetValue(TransportTypePart, out var transportTypeString) && Enum.TryParse((string)transportTypeString, true, out ServiceBusTransportType transportType) && transportType == ServiceBusTransportType.AmqpWebSockets)
            {
                transport.UseWebSockets();
            }
         */
    }
}
