namespace ServiceControl.Persistence;

/// <summary>The outcome of a sweep start request.</summary>
public enum RetentionSweepStatus
{
    /// <summary>The sweep was started on a background task.</summary>
    Started,
    /// <summary>A sweep is already running.</summary>
    AlreadyRunning
}