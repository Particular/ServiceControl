namespace ServiceControl.Recoverability
{
    public enum RetryType
    {
        SingleMessage,
        MultipleMessages,
        FailureGroup,
        AllForEndpoint,
        All,
        ByQueueAddress
    }
}