#nullable enable
namespace ServiceControl.Transports.BrokerThroughput;

using System.Collections.Generic;

#pragma warning disable CA1711
public interface IBrokerQueue
#pragma warning restore CA1711
{
    public string QueueName { get; }
    public string SanitizedName { get; }
    public string? Scope { get; }
    public List<string> EndpointIndicators { get; }
}