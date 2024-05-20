namespace Particular.LicensingComponent.MonitoringThroughput;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using System;

public class RecordEndpointThroughputData
{
    public EndpointThroughputData[] EndpointThroughputData { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
}

public class EndpointThroughputData
{
    public string Name { get; set; }
    public long Throughput { get; set; }
}