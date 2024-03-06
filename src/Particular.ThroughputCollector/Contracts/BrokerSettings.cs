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
        public static string API = "ThroughputCollector/ServiceControl/ApiUrl";
        public static string Queue = "ThroughputCollector/ServiceControl/Queue";
    }

    public static class SqlServerSettings
    {
        public static string ConnectionString = "ThroughputCollector/SqlServer/ConnectionString";
        public static string AdditionalCatalogs = "ThroughputCollector/SqlServer/AdditionalCatalogs";
    }

    public static class RabbitMQSettings
    {
        public static string API = "ThroughputCollector/RabbitMQ/ApiUrl";
        public static string UserName = "ThroughputCollector/RabbitMQ/UserName";
        public static string Password = "ThroughputCollector/RabbitMQ/Password";
    }

    public static class AzureServiceBusSettings
    {
        public static string ResourceId = "ThroughputCollector/ASB/ResourceId";
        public static string ClientId = "ThroughputCollector/ASB/ClientId";
        public static string ClientSecret = "ThroughputCollector/ASB/ClientSecret";
    }

    public static class AmazonSQSSettings
    {
        public static string Profile = "ThroughputCollector/AmazonSQS/Profile";
        public static string Region = "ThroughputCollector/AmazonSQS/Region";
        public static string Prefix = "ThroughputCollector/AmazonSQS/Prefix";
    }
}
