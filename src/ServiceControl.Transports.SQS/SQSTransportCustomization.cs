namespace ServiceControl.Transports.SQS
{
    using System;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;
    using Amazon;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
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

            PromoteEnvironmentVariableFromConnectionString(builder, "AccessKeyId", "AWS_ACCESS_KEY_ID");
            PromoteEnvironmentVariableFromConnectionString(builder, "SecretAccessKey", "AWS_SECRET_ACCESS_KEY");
            var region = PromoteEnvironmentVariableFromConnectionString(builder, "Region", "AWS_REGION");

            var awsRegion = RegionEndpoint.EnumerableAllRegions
                .SingleOrDefault(x => x.SystemName == region);

            if (awsRegion == null)
            {
                throw new ArgumentException($"Unknown region: \"{region}\"");
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
    }
}