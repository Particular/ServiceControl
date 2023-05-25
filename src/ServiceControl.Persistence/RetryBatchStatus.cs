namespace ServiceControl.Persistence
{
    public enum RetryBatchStatus
    {
        MarkingDocuments = 1,
        Staging = 2,
        Forwarding = 3
    }
}