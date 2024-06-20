#nullable enable
namespace ServiceControl.Transports.BrokerThroughput;

using System.Collections.Generic;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public class DefaultBrokerQueue(string queueName) : IBrokerQueue
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public string QueueName { get; } = queueName;
    public string SanitizedName { get; set; } = queueName;
    public string? Scope { get; } = null;
    public List<string> EndpointIndicators { get; } = [];
}