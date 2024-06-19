#nullable enable
namespace ServiceControl.Transports.RabbitMQ;

using System.Collections.Generic;
using System.Text.Json.Nodes;
using ServiceControl.Transports.BrokerThroughput;

public class RabbitMQBrokerQueueDetails(JsonNode token) : IBrokerQueue
{
    public string QueueName { get; } = token["name"]!.GetValue<string>();
    public string SanitizedName => QueueName;
    public string? Scope => VHost;
    public string VHost { get; } = token["vhost"]!.GetValue<string>();
    public long? AckedMessages { get; set; }
    public List<string> EndpointIndicators { get; } = [];
}