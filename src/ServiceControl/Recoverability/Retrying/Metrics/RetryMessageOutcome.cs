namespace ServiceControl.Recoverability.Retrying.Metrics;

public enum RetryMessageOutcome
{
    Staged,
    Forwarded,
    Skipped,
    StagingRetried,
    Abandoned
}
