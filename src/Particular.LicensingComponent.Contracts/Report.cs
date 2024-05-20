namespace Particular.LicensingComponent.Contracts;

using System.Text.Json.Serialization;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

//NOTE do not change fields to be nullable as this needs to be compatible with older versions of the report
public record SignedReport
{

    public Report ReportData { get; init; }
    public string Signature { get; init; }
}

public record Report
{
    public string CustomerName { get; init; }

    public string MessageTransport { get; init; }

    public string ReportMethod { get; init; }

    public string? ToolType { get; init; }
    public string ToolVersion { get; init; }

    public string? Prefix { get; init; }

    public string ScopeType { get; init; }

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Not necessarily the difference between Start/End time. ASB for example collects
    /// 30 days worth of data and reports the max daily throughput for one 24h period.
    /// </summary>
    public TimeSpan ReportDuration { get; set; }

    public QueueThroughput[] Queues { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] // Must be serialized even if 0 to maintain compatibility with old report signatures
    public long TotalThroughput { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] // Must be serialized even if 0 to maintain compatibility with old report signatures
    public int TotalQueues { get; init; }

    public string[] IgnoredQueues { get; init; }

    public EnvironmentInformation EnvironmentInformation { get; set; }
}

public record QueueThroughput
{
    public string QueueName { get; set; }

    public long? Throughput { get; set; }

    public bool NoDataOrSendOnly { get; init; }

    public string[] EndpointIndicators { get; init; }

    public string? UserIndicator { get; init; }

    public string Scope { get; init; }

    public DailyThroughput[] DailyThroughputFromBroker { get; init; }
    public DailyThroughput[] DailyThroughputFromAudit { get; init; }
    public DailyThroughput[] DailyThroughputFromMonitoring { get; init; }
}

public record EnvironmentInformation
{
    public AuditServicesData AuditServicesData { get; set; }
    public Dictionary<string, string> EnvironmentData { get; set; }
}

public record DailyThroughput
{
    public DateOnly DateUTC { get; init; }

    public long MessageCount { get; init; }
}

public record AuditServicesData(Dictionary<string, int> Versions, Dictionary<string, int> Transports)
{
}
