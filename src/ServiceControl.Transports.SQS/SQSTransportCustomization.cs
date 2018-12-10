namespace ServiceControl.Transports.SQS
{
    using System;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;
    using Amazon;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;

    public class SQSTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqsTransport>();

            ConfigureTransport(transport, transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqsTransport>();

            ConfigureTransport(transport, transportSettings);
        }

        static void ConfigureTransport(TransportExtensions<SqsTransport> transport, TransportSettings transportSettings)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = transportSettings.ConnectionString };

            if (builder.ContainsKey("AccessKeyId") || builder.ContainsKey("SecretAccessKey"))
            {
                PromoteEnvironmentVariableFromConnectionString(builder, "AccessKeyId", "AWS_ACCESS_KEY_ID");
                PromoteEnvironmentVariableFromConnectionString(builder, "SecretAccessKey", "AWS_SECRET_ACCESS_KEY");
            }
            else
            {
                //See https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html#creds-assign
                log.Info("BasicAWSCredentials have not been supplied in the connection string. Attempting to use existing environment or IAM role credentials.");
            }

            var region = PromoteEnvironmentVariableFromConnectionString(builder, "Region", "AWS_REGION");

            var awsRegion = RegionEndpoint.EnumerableAllRegions
                .SingleOrDefault(x => x.SystemName == region);

            if (awsRegion == null)
            {
                throw new ArgumentException($"Unknown region: \"{region}\"");
            }

            if (builder.TryGetValue("QueueNamePrefix", out var queueNamePrefix))
            {
                var queueNamePrefixAsString = (string)queueNamePrefix;
                if (!string.IsNullOrEmpty(queueNamePrefixAsString))
                {
                    transport.QueueNamePrefix(queueNamePrefixAsString);
                }
            }

            //HINT: This is needed to make Core doesn't load a connection string value from the app.config.
            //      This prevents SQS from throwing on startup.
            var connectionString = transport.GetSettings().Get("NServiceBus.TransportConnectionString");

            connectionString.GetType()
                .GetField("GetValue", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(connectionString, (Func<string>)(() => null));

            transport.Transactions(TransportTransactionMode.ReceiveOnly);
        }


        static string PromoteEnvironmentVariableFromConnectionString(DbConnectionStringBuilder builder, string connectionStringKey, string environmentVariableName)
        {
            if (builder.TryGetValue(connectionStringKey, out var value))
            {
                var valueAsString = (string)value;
                Environment.SetEnvironmentVariable(environmentVariableName, valueAsString, EnvironmentVariableTarget.Process);
                return valueAsString;
            }

            throw new ArgumentException($"Missing value for '{connectionStringKey}'", connectionStringKey);
        }

        static ILog log = LogManager.GetLogger<SQSTransportCustomization>();
    }
}