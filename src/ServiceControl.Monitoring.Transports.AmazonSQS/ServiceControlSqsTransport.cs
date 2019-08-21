namespace ServiceControl.Transports.AmazonSQS
{
    using System;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;
    using Amazon;
    using Amazon.Runtime;
    using Amazon.SQS;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    public class ServiceControlSqsTransport : SqsTransport
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            if (builder.ContainsKey("AccessKeyId") || builder.ContainsKey("SecretAccessKey"))
            {
                PromoteEnvironmentVariableFromConnectionString(builder, "AccessKeyId", "AWS_ACCESS_KEY_ID");
                PromoteEnvironmentVariableFromConnectionString(builder, "SecretAccessKey", "AWS_SECRET_ACCESS_KEY");

                // if the user provided the access key and secret access key they should always be loaded from environment credentials
                var transport = new TransportExtensions<SqsTransport>(settings);
                transport.ClientFactory(() => new AmazonSQSClient(new EnvironmentVariablesAWSCredentials()));
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

            settings.Set("NServiceBus.AmazonSQS.Region", awsRegion);

            if (builder.TryGetValue("QueueNamePrefix", out var queueNamePrefix))
            {
                var queueNamePrefixAsString = (string)queueNamePrefix;
                if (!string.IsNullOrEmpty(queueNamePrefixAsString))
                {
                    var extensions = new TransportExtensions<SqsTransport>(settings);
                    extensions.QueueNamePrefix(queueNamePrefixAsString);
                }
            }

            //HINT: This is needed to make sure Core doesn't load a connection string value from the app.config.
            //      This prevents SQS from throwing on startup.
            var connectionStringSetting = settings.Get("NServiceBus.TransportConnectionString");

            connectionStringSetting.GetType()
                .GetField("GetValue", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(connectionStringSetting, (Func<string>)(() => null));

            // SQS doesn't support connection strings so pass in null.
            return base.Initialize(settings, null);
        }

        static string PromoteEnvironmentVariableFromConnectionString(DbConnectionStringBuilder builder, string connectionStringKey, string environmentVariableName)
        {
            if (builder.TryGetValue(connectionStringKey, out var value))
            {
                var valueAsString = (string) value;
                Environment.SetEnvironmentVariable(environmentVariableName, valueAsString, EnvironmentVariableTarget.Process);
                return valueAsString;
            }

            throw new ArgumentException($"Missing value for '{connectionStringKey}'", connectionStringKey);
        }

        static ILog log = LogManager.GetLogger<ServiceControlSqsTransport>();
    }
}