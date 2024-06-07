using QueueThroughput = Particular.LicensingComponent.Report.QueueThroughput;

public class QueueDetails
{
    public string ScopeType { get; init; }
    public QueueThroughput[] Queues { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    /// <summary>
    /// If reported as null, the report will assume (EndTime - StartTime)
    /// </summary>
    public TimeSpan? TimeOfObservation { get; init; }
}