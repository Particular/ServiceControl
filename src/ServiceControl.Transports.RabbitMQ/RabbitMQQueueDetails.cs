#nullable enable
namespace ServiceControl.Transports.RabbitMQ;

using System.Collections.Generic;
using System.Text.Json;
using ServiceControl.Transports.BrokerThroughput;

public class RabbitMQBrokerQueueDetails(JsonElement token) : IBrokerQueue
{
    public string QueueName { get; } = token.GetProperty("name").GetString()!;
    public string SanitizedName => QueueName;
    public string Scope => VHost;
    public string VHost { get; } = token.GetProperty("vhost").GetString()!;
    public long? AckedMessages { get; set; }
    public List<string> EndpointIndicators { get; } = [];
}