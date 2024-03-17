namespace ServiceControl.Transports.RabbitMQ;

using System.Text.Json.Nodes;
using Particular.ThroughputCollector.Broker;

public class RabbitMQQueueDetails : IQueueName
{
    public RabbitMQQueueDetails(JsonNode token)
    {
        QueueName = token["name"]!.GetValue<string>();
        VHost = token["vhost"]!.GetValue<string>();
    }

    public string Id => $"{VHost}:{QueueName}";

    public string QueueName { get; }
    public string VHost { get; }
    public long? AckedMessages { get; set; }
    public List<string> EndpointIndicators { get; } = [];
}