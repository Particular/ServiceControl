using System;

public class RetryGroup
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public int Count { get; set; }
    public DateTime? First { get; set; }
    public DateTime? Last { get; set; }
    public string RetryStatus { get; set; }
    public bool? RetryFailed { get; set; }
    public double RetryProgress { get; set; }
    public int? RetryRemainingCount { get; set; }
    public DateTime? RetryStartTime { get; set; }
    public DateTime? LastRetryCompletionTime { get; set; }
    public bool NeedUserAcknowledgement { get; set; }
}