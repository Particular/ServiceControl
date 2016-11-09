namespace ServiceControl.Recoverability
{
    public enum RetryType
    {
        Unknown = 0,
        SingleMessage = 1,
        FailureGroup = 2,
        MultipleMessages = 3,
        AllForEndpoint = 4,
        All = 5, 
        ByQueueAddress = 6
    }
}