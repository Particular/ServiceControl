namespace ServiceControl.Transports.RabbitMQ;

using System.Collections.Generic;
using System.Text.Json.Nodes;

public class RabbitMQQueueDetails(JsonNode token) : IQueueName
{
    public string QueueName { get; } = token["name"]!.GetValue<string>();
    public string VHost { get; } = token["vhost"]!.GetValue<string>();
    public long? AckedMessages { get; set; }
    public List<string> EndpointIndicators { get; } = [];
}