namespace Particular.ThroughputCollector.Contracts
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Broker
    {
        None,
        AmazonSQS,
        RabbitMQ,
        AzureServiceBus,
        SqlServer
    }
}
