#nullable enable
namespace ServiceControl.Transports.BrokerThroughput;

using System.Collections.Generic;

#pragma warning disable CA1711
public interface IBrokerQueue
#pragma warning restore CA1711
{
    string QueueName { get; }

    string SanitizedName { get; }

    string? Scope { get; }

    List<string> EndpointIndicators { get; }
}