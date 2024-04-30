namespace Particular.ThroughputCollector.Contracts;

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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolType { get; init; }
    public string ToolVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public string? Prefix { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public string ScopeType { get; init; }

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Not necessarily the difference between Start/End time. ASB for example collects
    /// 30 days worth of data and reports the max daily throughput for one 24h period.
    /// </summary>
    public TimeSpan ReportDuration { get; set; }

    public QueueThroughput[] Queues { get; init; }

    public long TotalThroughput { get; init; }

    public int TotalQueues { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public string[] IgnoredQueues { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public EnvironmentInformation EnvironmentInformation { get; set; }
}

public record QueueThroughput
{
    public string QueueName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public long? Throughput { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool NoDataOrSendOnly { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public string[] EndpointIndicators { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public string? UserIndicator { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public string Scope { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public DailyThroughput[] DailyThroughputFromBroker { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public DailyThroughput[] DailyThroughputFromAudit { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public DailyThroughput[] DailyThroughputFromMonitoring { get; init; }
}

public record EnvironmentInformation
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public AuditServicesData AuditServicesData { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
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
