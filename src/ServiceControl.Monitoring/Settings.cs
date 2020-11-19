namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using NLog;
    using Transports;
    using ServiceControl.Monitoring.Infrastructure.Settings;

    //TODO: align names with SC and SC.Audit
    public class Settings
    {
        public Settings()
        {
            TryLoadLicenseFromConfig();

            TransportType = SettingsReader<string>.Read("TransportType");
            ConnectionString = GetConnectionString();
            LogLevel = MonitorLogs.InitializeLevel();
            LogPath = SettingsReader<string>.Read("LogPath", DefaultLogLocation());
            ErrorQueue = SettingsReader<string>.Read("ErrorQueue", "error");
            HttpHostName = SettingsReader<string>.Read("HttpHostname");
            HttpPort = SettingsReader<string>.Read("HttpPort");
            EndpointName = SettingsReader<string>.Read("EndpointName");
            EndpointUptimeGracePeriod = TimeSpan.Parse(SettingsReader<string>.Read("EndpointUptimeGracePeriod", "00:00:40"));
            MaximumConcurrencyLevel = SettingsReader<int>.Read("MaximumConcurrencyLevel", 32);
        }

        public string EndpointName
        {
            get { return endpointName ?? ServiceName; }
            set { endpointName = value; }
        }

        public bool Portable { get; set; } = false;
        public string ServiceName { get; set; } = DEFAULT_ENDPOINT_NAME;
        public string TransportType { get; set; }
        public string ConnectionString { get; set; }
        public string ErrorQueue { get; set; }
        public string LogPath { get; set; }
        public LogLevel LogLevel { get; set; }
        public string Username { get; set; }
        public bool EnableInstallers { get; set; }
        public string HttpHostName { get; set; }
        public string HttpPort { get; set; }
        public TimeSpan EndpointUptimeGracePeriod { get; set; }
        public bool SkipQueueCreation { get; set; }
        public string RootUrl => $"http://{HttpHostName}:{HttpPort}/";
        public int MaximumConcurrencyLevel { get; set; }
        public string LicenseFileText { get; set; }

        // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
        // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
        internal static string DefaultLogLocation()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(assemblyLocation);
        }

        void TryLoadLicenseFromConfig()
        {
            LicenseFileText = SettingsReader<string>.Read("LicenseText");
        }

        public TransportCustomization LoadTransportCustomization()
        {
            try
            {
                var customizationType = LoadTransportCustomizationType(TransportType);

                return (TransportCustomization)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load transport customization type {TransportType}.", e);
            }
        }

        static string GetConnectionString()
        {
            var settingsValue = SettingsReader<string>.Read("ConnectionString");
            if (settingsValue != null)
            {
                return settingsValue;
            }

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            return connectionStringSettings?.ConnectionString;
        }

        static Type LoadTransportCustomizationType(string configuredTransportCustomizationName)
        {
            //HINT: We need to support old transport seam names for scenarios when a user upgrades from some old version
            //      of the monitoring instance. In such case the app.config will hold old seam type name.
            var transportTypeName = legacyTransportCustomizationNames.ContainsKey(configuredTransportCustomizationName)
                ? legacyTransportCustomizationNames[configuredTransportCustomizationName]
                : configuredTransportCustomizationName;

            var transportType = Type.GetType(transportTypeName, true);

            return transportType;
        }
        string endpointName;

        public Func<string, Dictionary<string, string>, byte[], Func<Task>, Task> OnMessage { get; set; } = (messageId, headers, body, next) => next();

        public const string DEFAULT_ENDPOINT_NAME = "Particular.Monitoring";

        static Dictionary<string, string> legacyTransportCustomizationNames = new Dictionary<string, string>
        {
            //MSMQ
            {"NServiceBus.MsmqTransport, NServiceBus.Transport.Msmq", "ServiceControl.Transports.Msmq.MsmqTransportCustomization, ServiceControl.Transports.Msmq"},

            //RabbitMQ
            {"NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ", "ServiceControl.Transports.RabbitMQ.RabbitMQConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ"},
            {"ServiceControl.Transports.RabbitMQ.ConventialRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", "ServiceControl.Transports.RabbitMQ.RabbitMQConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ"},
            {"ServiceControl.Transports.RabbitMQ.DirectRoutingTopologyRabbitMQTransport, ServiceControl.Transports.RabbitMQ", "ServiceControl.Transports.RabbitMQ.RabbitMQDirectRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ"},

            //ASQ
            {"NServiceBus.AzureStorageQueueTransport, NServiceBus.Transport.AzureStorageQueues", "ServiceControl.Transports.ASQ.ASQTransportCustomization, ServiceControl.Transports.ASQ"},
            {"ServiceControl.Transports.AzureStorageQueues.ServiceControlAzureStorageQueueTransport, ServiceControl.Transports.AzureStorageQueues", "ServiceControl.Transports.ASQ.ASQTransportCustomization, ServiceControl.Transports.ASQ"},

            //ASB NetStandard
            {"ServiceControl.Transports.AzureServiceBus.AzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus", "ServiceControl.Transports.ASBS.ASBSTransportCustomization, ServiceControl.Transports.ASBS"},

            //ASB
            {"NServiceBus.AzureServiceBusTransport, NServiceBus.Azure.Transports.WindowsAzureServiceBus", "ServiceControl.Transports.ASB.ASBForwardingTopologyTransportCustomization, ServiceControl.Transports.ASB"},
            {"ServiceControl.Transports.LegacyAzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus", "ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization, ServiceControl.Transports.ASB"},
            {"ServiceControl.Transports.LegacyAzureServiceBus.ForwardingTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus", "ServiceControl.Transports.ASB.ASBForwardingTopologyTransportCustomization, ServiceControl.Transports.ASB"},

            //SQS
            {"NServiceBus.SqsTransport, NServiceBus.AmazonSQS", "ServiceControl.Transports.SQS.SQSTransportCustomization, ServiceControl.Transports.SQS"},
            {"ServiceControl.Transports.AmazonSQS.ServiceControlSqsTransport, ServiceControl.Transports.AmazonSQS", "ServiceControl.Transports.SQS.SQSTransportCustomization, ServiceControl.Transports.SQS"},

            //SQL
            {"NServiceBus.SqlServerTransport, NServiceBus.Transport.SQLServer", "ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer"},
            {"ServiceControl.Transports.SQLServer.ServiceControlSQLServerTransport, ServiceControl.Transports.SQLServer", "ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer"}
        };
    }
}