namespace Particular.ThroughputCollector.Shared;

using System.Collections.Frozen;
using Contracts;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

public class BrokerSettingValues
{
    private readonly Dictionary<string, string> brokerSettingValues = [];

    public BrokerSettingValues(ThroughputSettings settings, ILogger<BrokerSettingValues> logger)
    {
        BrokerSettings brokerSettings = BrokerManifestLibrary.Find(settings.Broker);
        brokerSettings.Settings.ForEach(s => brokerSettingValues.Add(s.Name, string.Empty));

        //for each broker try and grab the required settings from config/env, and if they don't exist try to get them from the transportConnectionString
        switch (brokerSettings.Broker)
        {
            case Broker.ServiceControl:
                logger.LogInformation("Not using a broker - throughput data will come from ServiceControl.");
                brokerSettingValues[ServiceControlSettings.API] = GetConfigSetting(ServiceControlSettings.API, logger);
                if (string.IsNullOrEmpty(brokerSettingValues[ServiceControlSettings.API]))
                {
                    brokerSettingValues[ServiceControlSettings.API] = settings.ServiceControlAPI;
                }

                break;
            case Broker.AmazonSQS:
                brokerSettingValues[AmazonSQSSettings.Profile] = GetConfigSetting(AmazonSQSSettings.Profile, logger);
                brokerSettingValues[AmazonSQSSettings.Region] = GetConfigSetting(AmazonSQSSettings.Region, logger);
                brokerSettingValues[AmazonSQSSettings.Prefix] = GetConfigSetting(AmazonSQSSettings.Prefix, logger);
                //TODO if those settings don't exist - try and get them from transportConnectionString 
                break;
            case Broker.RabbitMQ:
                brokerSettingValues[RabbitMQSettings.API] = GetConfigSetting(RabbitMQSettings.API, logger);
                brokerSettingValues[RabbitMQSettings.UserName] = GetConfigSetting(RabbitMQSettings.UserName, logger);
                brokerSettingValues[RabbitMQSettings.Password] = GetConfigSetting(RabbitMQSettings.Password, logger);
                //TODO if those settings don't exist - try and get them from transportConnectionString 
                break;
            case Broker.AzureServiceBus:
                brokerSettingValues[AzureServiceBusSettings.ServiceBusName] =
                    GetConfigSetting(AzureServiceBusSettings.ServiceBusName, logger);
                brokerSettingValues[AzureServiceBusSettings.ClientId] =
                    GetConfigSetting(AzureServiceBusSettings.ClientId, logger);
                brokerSettingValues[AzureServiceBusSettings.ClientSecret] =
                    GetConfigSetting(AzureServiceBusSettings.ClientSecret, logger);
                break;
            case Broker.SqlServer:
                brokerSettingValues[SqlServerSettings.ConnectionString] =
                    GetConfigSetting(SqlServerSettings.ConnectionString, logger);
                if (string.IsNullOrEmpty(brokerSettingValues[SqlServerSettings.ConnectionString]))
                {
                    brokerSettingValues[SqlServerSettings.ConnectionString] = settings.TransportConnectionString;
                }

                brokerSettingValues[SqlServerSettings.AdditionalCatalogs] =
                    GetConfigSetting(SqlServerSettings.AdditionalCatalogs, logger);
                break;
        }

        static string GetConfigSetting(string name, ILogger logger)
        {
            logger.LogDebug($"Reading setting for {name}");

            return SettingsReader.Read<string>(new SettingsRootNamespace("ThroughputCollector"), name);
        }
    }

    public FrozenDictionary<string, string> SettingValues => brokerSettingValues.ToFrozenDictionary();
}