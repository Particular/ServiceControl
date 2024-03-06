namespace Particular.ThroughputCollector.Contracts
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Broker
    {
        ServiceControl,
        AmazonSQS,
        RabbitMQ,
        AzureServiceBus,
        SqlServer
    }
}
