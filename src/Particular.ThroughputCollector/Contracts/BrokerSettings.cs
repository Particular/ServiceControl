namespace Particular.ThroughputCollector.Contracts
{
    using System.Text.Json.Serialization;

    public class BrokerSettings
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Broker Broker { get; set; }
        public List<BrokerSetting> Settings { get; set; } = [];
    }

    public class BrokerSetting
    {
        public required string Name { get; set; }
        public required string Description { get; set; }
    }

    public static class ServiceControlSettings
    {
        public static string API = "ServiceControl/ApiUrl";
        public static string Queue = "ServiceControl/Queue";
    }

    public static class SqlServerSettings
    {
        public static string ConnectionString = "SqlServer/ConnectionString";
        public static string AdditionalCatalogs = "SqlServer/AdditionalCatalogs";
    }

    public static class RabbitMQSettings
    {
        public static string API = "RabbitMQ/ApiUrl";
        public static string UserName = "RabbitMQ/UserName";
        public static string Password = "RabbitMQ/Password";
    }

    public static class AzureServiceBusSettings
    {
        public static string ServiceBusName = "ASB/ServiceBusName";
        public static string ClientId = "ASB/ClientId";
        public static string ClientSecret = "ASB/ClientSecret";
        public static string TenantId = "ASB/TenantId";
        public static string SubscriptionId = "ASB/SubscriptionId";
        public static string ManagementUrl = "ASB/ManagementUrl";
    }

    public static class AmazonSQSSettings
    {
        public static string Profile = "AmazonSQS/Profile";
        public static string Region = "AmazonSQS/Region";
        public static string Prefix = "AmazonSQS/Prefix";
    }
}
