namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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

    public string ServiceControlVersion { get; set; }
    public string ServicePulseVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Prefix { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ScopeType { get; init; }

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Not necessarily the difference between Start/End time. ASB for example collects
    /// 30 days worth of data and reports the max daily throughput for one 24h period.
    /// </summary>
    public TimeSpan ReportDuration { get; init; }

    public QueueThroughput[] Queues { get; init; }

    public long TotalThroughput { get; init; }

    public int TotalQueues { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string[]? IgnoredQueues { get; init; }

    public Dictionary<string, string> EnvironmentData { get; set; }
}

public class QueueThroughput
{
    public string QueueName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long? Throughput { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool? NoDataOrSendOnly { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string[]? EndpointIndicators { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? UserIndicator { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Scope { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public EndpointDailyThroughput[] DailyThroughputFromBroker { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public EndpointDailyThroughput[] DailyThroughputFromAudit { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public EndpointDailyThroughput[] DailyThroughputFromMonitoring { get; init; }
}
