using System;

public class GroupOperation
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public int Count { get; set; }
    public DateTime? First { get; set; }
    public DateTime? Last { get; set; }
    public string OperationStatus { get; set; }
    public bool? OperationFailed { get; set; }
    public double OperationProgress { get; set; }
    public int? OperationRemainingCount { get; set; }
    public DateTime? OperationStartTime { get; set; }
    public DateTime? OperationCompletionTime { get; set; }
    public bool NeedUserAcknowledgement { get; set; }
}