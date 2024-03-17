namespace Particular.ThroughputCollector.Contracts
{
    using System.Collections.Frozen;
    using ServiceControl.Configuration;
    using Shared;

    public class ThroughputSettings
    {
        public ThroughputSettings(Broker broker, string transportConnectionString, string serviceControlAPI, string serviceControlQueue, string errorQueue, string persistenceType, string customerName, string serviceControlVersion, string auditQueue = "audit")
        {
            Broker = broker;
            TransportConnectionString = transportConnectionString;
            ServiceControlAPI = serviceControlAPI;
            ServiceControlQueue = serviceControlQueue;
            ErrorQueue = errorQueue;
            PersistenceType = persistenceType;
            AuditQueue = auditQueue;
            CustomerName = customerName;
            ServiceControlVersion = serviceControlVersion;

            BrokerSettingValues = LoadBrokerSettingValues().ToFrozenDictionary();
        }

        public string ServiceControlAPI { get; set; }
        public Broker Broker { get; set; }
        public string ErrorQueue { get; set; }
        public string ServiceControlQueue { get; set; }
        public string AuditQueue { get; set; } //NOTE can we get this?
        public string TransportConnectionString { get; set; }
        public string PersistenceType { get; set; }
        public string CustomerName { get; set; }
        public string ServiceControlVersion { get; set; }
        public FrozenDictionary<string, string> BrokerSettingValues { get; set; }

        private Dictionary<string, string> LoadBrokerSettingValues()
        {
            var brokerSettingValues = new Dictionary<string, string>();
            var brokerSettings = BrokerSettingsLibrary.Find(Broker);
            brokerSettings.Settings.ForEach(s => brokerSettingValues.Add(s.Name, string.Empty));

            //for each broker try and grab the required settings from config/env, and if they don't exist try to get them from the transportConnectionString
            switch (brokerSettings.Broker)
            {
                case Broker.None:
                    brokerSettingValues[ServiceControlSettings.API] = GetConfigSetting(ServiceControlSettings.API);
                    if (string.IsNullOrEmpty(brokerSettingValues[ServiceControlSettings.API]))
                    {
                        brokerSettingValues[ServiceControlSettings.API] = ServiceControlAPI;
                    }
                    brokerSettingValues[ServiceControlSettings.Queue] = GetConfigSetting(ServiceControlSettings.Queue);
                    if (string.IsNullOrEmpty(brokerSettingValues[ServiceControlSettings.Queue]))
                    {
                        brokerSettingValues[ServiceControlSettings.Queue] = ServiceControlQueue;
                    }
                    break;
                case Broker.AmazonSQS:
                    brokerSettingValues[AmazonSQSSettings.Profile] = GetConfigSetting(AmazonSQSSettings.Profile);
                    brokerSettingValues[AmazonSQSSettings.Region] = GetConfigSetting(AmazonSQSSettings.Region);
                    brokerSettingValues[AmazonSQSSettings.Prefix] = GetConfigSetting(AmazonSQSSettings.Prefix);
                    break;
                case Broker.RabbitMQ:
                    brokerSettingValues[RabbitMQSettings.API] = GetConfigSetting(RabbitMQSettings.API);
                    brokerSettingValues[RabbitMQSettings.UserName] = GetConfigSetting(RabbitMQSettings.UserName);
                    brokerSettingValues[RabbitMQSettings.Password] = GetConfigSetting(RabbitMQSettings.Password);
                    break;
                case Broker.AzureServiceBus:
                    brokerSettingValues[AzureServiceBusSettings.ServiceBusName] =
                        GetConfigSetting(AzureServiceBusSettings.ServiceBusName);
                    brokerSettingValues[AzureServiceBusSettings.ClientId] =
                        GetConfigSetting(AzureServiceBusSettings.ClientId);
                    brokerSettingValues[AzureServiceBusSettings.ClientSecret] =
                        GetConfigSetting(AzureServiceBusSettings.ClientSecret);
                    brokerSettingValues[AzureServiceBusSettings.SubscriptionId] =
                        GetConfigSetting(AzureServiceBusSettings.SubscriptionId);
                    brokerSettingValues[AzureServiceBusSettings.TenantId] =
                        GetConfigSetting(AzureServiceBusSettings.TenantId);
                    brokerSettingValues[AzureServiceBusSettings.ManagementUrl] =
                        GetConfigSetting(AzureServiceBusSettings.ManagementUrl);
                    break;
                case Broker.SqlServer:
                    brokerSettingValues[SqlServerSettings.ConnectionString] =
                        GetConfigSetting(SqlServerSettings.ConnectionString);
                    brokerSettingValues[SqlServerSettings.AdditionalCatalogs] =
                        GetConfigSetting(SqlServerSettings.AdditionalCatalogs);
                    break;
            }

            brokerSettingValues[CommonSettings.TransportConnectionString] = TransportConnectionString;

            return brokerSettingValues;
        }

        string GetConfigSetting(string name)
        {
            //logger.LogDebug($"Reading setting for {name}");

            name = name.Replace($"{BrokerSettingsLibrary.SettingsNamespace}/", "");
            return SettingsReader.Read<string>(new SettingsRootNamespace(BrokerSettingsLibrary.SettingsNamespace), name);
        }
    }
}
