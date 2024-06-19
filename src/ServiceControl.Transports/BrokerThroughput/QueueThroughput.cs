#nullable enable
namespace ServiceControl.Transports.BrokerThroughput;

using System;

public class QueueThroughput
{
    public DateOnly DateUTC { get; set; }
    public long TotalThroughput { get; set; }
}