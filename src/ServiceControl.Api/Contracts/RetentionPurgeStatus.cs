namespace ServiceControl.Api.Contracts;

public enum RetentionPurgeStatus
{
    Started,
    AlreadyRunning,
    NotSupported,
    Error
}