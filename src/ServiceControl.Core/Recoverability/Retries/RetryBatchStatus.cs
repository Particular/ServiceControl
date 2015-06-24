namespace ServiceControl.Recoverability.Retries
{
    public enum RetryBatchStatus
    {
        MarkingDocuments = 1, 
        Staging = 2, 
        Forwarding = 3, 
        Done = 4
    }
}