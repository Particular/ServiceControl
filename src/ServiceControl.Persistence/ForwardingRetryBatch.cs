namespace ServiceControl.Persistence
{
    /// <summary>
    /// The batch currently being forwarded.
    /// </summary>
    public record ForwardingRetryBatch(string RequestId, RetryType RetryType, string Originator, string Classifier);
}
