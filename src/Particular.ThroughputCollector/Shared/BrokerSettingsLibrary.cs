namespace Particular.ThroughputCollector.Shared;

using Contracts;

public static class BrokerSettingsLibrary
{
    public static List<BrokerSettings> AllBrokerSettings { get; private set; } = [];
    public static string SettingsNamespace = "ThroughputCollector";

    static BrokerSettingsLibrary()
    {
        //No broker used - ie need to use service control to gather data
        AllBrokerSettings.Add(new BrokerSettings
        {
            Broker = Broker.None,
            Settings = [new BrokerSetting { Name = ServiceControlSettings.API, Description = ServiceControlSettings.APIDescription },
                new BrokerSetting { Name = ServiceControlSettings.Queue, Description = ServiceControlSettings.QueueDescription }
            ]
        });

        //SqlServer
        AllBrokerSettings.Add(new BrokerSettings
        {
            Broker = Broker.SqlServer,
            Settings = [new BrokerSetting { Name = SqlServerSettings.ConnectionString, Description = SqlServerSettings.ConnectionStringDescription },
                new BrokerSetting { Name = SqlServerSettings.AdditionalCatalogs, Description = SqlServerSettings.AdditionalCatalogsDescription }
            ]
        });

        //RabbitMQ
        AllBrokerSettings.Add(new BrokerSettings
        {
            Broker = Broker.RabbitMQ,
            Settings = [new BrokerSetting { Name = RabbitMQSettings.API, Description = RabbitMQSettings.APIDescription },
                new BrokerSetting { Name = RabbitMQSettings.UserName, Description = RabbitMQSettings.UserNameDescription },
                new BrokerSetting { Name = RabbitMQSettings.Password, Description = RabbitMQSettings.PasswordDescription }
            ]
        });

        //Amazon SQS
        AllBrokerSettings.Add(new BrokerSettings
        {
            Broker = Broker.AmazonSQS,
            Settings = [
                new BrokerSetting { Name = AmazonSQSSettings.AccessKey, Description = AmazonSQSSettings.AccessKeyDescription },
                new BrokerSetting { Name = AmazonSQSSettings.SecretKey, Description = AmazonSQSSettings.SecretKeyDescription },
                new BrokerSetting { Name = AmazonSQSSettings.Profile, Description = AmazonSQSSettings.ProfileDescription },
                new BrokerSetting { Name = AmazonSQSSettings.Region, Description = AmazonSQSSettings.RegionDescription },
                new BrokerSetting { Name = AmazonSQSSettings.Prefix, Description = AmazonSQSSettings.PrefixDescription }
            ]
        });

        //Azure Service Bus
        AllBrokerSettings.Add(new BrokerSettings
        {
            Broker = Broker.AzureServiceBus,
            Settings = [new BrokerSetting { Name = AzureServiceBusSettings.ServiceBusName, Description = AzureServiceBusSettings.ServiceBusNameDescription },
                new BrokerSetting { Name = AzureServiceBusSettings.ManagementUrl, Description = AzureServiceBusSettings.ManagementUrlDescription },
                new BrokerSetting { Name = AzureServiceBusSettings.TenantId, Description = AzureServiceBusSettings.TenantIdDescription },
                new BrokerSetting { Name = AzureServiceBusSettings.ClientId, Description = AzureServiceBusSettings.ClientIdDescription },
                new BrokerSetting { Name = AzureServiceBusSettings.ClientSecret, Description = AzureServiceBusSettings.ClientSecretDescription },
                new BrokerSetting { Name = AzureServiceBusSettings.SubscriptionId, Description = AzureServiceBusSettings.SubscriptionIdDescription }
            ]
        });
    }

    public static BrokerSettings Find(Broker broker) => AllBrokerSettings.First(w => w.Broker == broker);
}

public static class CommonSettings
{
    public static string TransportConnectionString = "ThroughputCollector/TransportConnectionString";
}

public static class ServiceControlSettings
{
    public static string API = "ThroughputCollector/ServiceControl/ApiUrl";
    public static string APIDescription = "Service Control API URL";
    public static string Queue = "ThroughputCollector/ServiceControl/Queue";
    public static string QueueDescription = "Service Control main processing queue";
}

public static class SqlServerSettings
{
    public static string ConnectionString = "ThroughputCollector/SqlServer/ConnectionString";
    public static string ConnectionStringDescription = "A single database connection string that will provide at least read access to all queue tables.";
    public static string AdditionalCatalogs = "ThroughputCollector/SqlServer/AdditionalCatalogs";
    public static string AdditionalCatalogsDescription = "When the ConnectionString setting points to a single database, but multiple database catalogs on the same server also contain NServiceBus message queues, the AdditionalCatalogs setting specifies additional database catalogs to search. The tool replaces the Database or Initial Catalog parameter in the connection string with the additional catalog and queries all of them.";
}

public static class RabbitMQSettings
{
    public static string API = "ThroughputCollector/RabbitMQ/ApiUrl";
    public static string APIDescription = "RabbitMQ management URL";
    public static string UserName = "ThroughputCollector/RabbitMQ/UserName";
    public static string UserNameDescription = "Username to access the RabbitMQ management interface";
    public static string Password = "ThroughputCollector/RabbitMQ/Password";
    public static string PasswordDescription = "Password to access the RabbitMQ management interface";
}

public static class AzureServiceBusSettings
{
    public static string ServiceBusName = "ThroughputCollector/ASB/ServiceBusName";
    public static string ServiceBusNameDescription = "Azure Service Bus namespace to view metrics.";
    public static string ClientId = "ThroughputCollector/ASB/ClientId";
    public static string ClientIdDescription = "ClientId for an Azure login that has access to view metrics data for the Azure Service Bus namespace.";
    public static string ClientSecret = "ThroughputCollector/ASB/ClientSecret";
    public static string ClientSecretDescription = "ClientSecret for an Azure login that has access to view metrics data for the Azure Service Bus namespace.";
    public static string TenantId = "ThroughputCollector/ASB/TenantId";
    public static string TenantIdDescription = "??";
    public static string SubscriptionId = "ThroughputCollector/ASB/SubscriptionId";
    public static string SubscriptionIdDescription = "??";
    public static string ManagementUrl = "ThroughputCollector/ASB/ManagementUrl";
    public static string ManagementUrlDescription = "??";
}

public static class AmazonSQSSettings
{
    public static string AccessKey = "ThroughputCollector/AmazonSQS/AccessKey";
    public static string AccessKeyDescription = "The AWS Access Key ID to use to discover queue names and gather per-queue metrics.";
    public static string SecretKey = "ThroughputCollector/AmazonSQS/SecretKey";
    public static string SecretKeyDescription = "The AWS Secret Access Key to use to discover queue names and gather per-queue metrics.";
    public static string Profile = "ThroughputCollector/AmazonSQS/Profile";
    public static string ProfileDescription = "The name of a local AWS credentials profile to use to discover queue names and gather per-queue metrics.";
    public static string Region = "ThroughputCollector/AmazonSQS/Region";
    public static string RegionDescription = "The AWS region to use when accessing AWS services.";
    public static string Prefix = "ThroughputCollector/AmazonSQS/Prefix";
    public static string PrefixDescription = "Report only on queues that begin with a specific prefix. This is commonly used when one AWS account must contain queues for multiple projects or multiple environments.";
}


