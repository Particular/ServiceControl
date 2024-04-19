namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

//NOTE do not change fields to be nullable as this needs to be compatible with older versions of the report
public class SignedReport
{

    public Report ReportData { get; init; }
    public string Signature { get; init; }
}

public class Report
{
    public string CustomerName { get; init; }

    public string MessageTransport { get; init; }

    public string ReportMethod { get; init; }

    public string ToolVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
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

public class QueueThroughput
{
    public string QueueName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
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
    public EndpointDailyThroughput[] DailyThroughputFromBroker { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public EndpointDailyThroughput[] DailyThroughputFromAudit { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public EndpointDailyThroughput[] DailyThroughputFromMonitoring { get; init; }
}

public class EnvironmentInformation
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public AuditInstance[] AuditInstances { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string> EnvironmentData { get; set; }
}
