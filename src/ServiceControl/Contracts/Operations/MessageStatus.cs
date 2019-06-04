namespace ServiceControl.Contracts.Operations
{
    public enum MessageStatus
    {
        Failed = 1,
        RepeatedFailure = 2,
        Successful = 3,
        ResolvedSuccessfully = 4,
        ArchivedFailure = 5,
        RetryIssued = 6
    }
}