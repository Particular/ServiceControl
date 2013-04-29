namespace ServiceBus.Management
{
    public enum MessageStatus
    {
        Failed = 1,
        RepeatedFailure = 2,
        Successful = 3,
        RetryIssued = 4
    }
}