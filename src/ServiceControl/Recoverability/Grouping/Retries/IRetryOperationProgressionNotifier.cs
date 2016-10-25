namespace ServiceControl.Recoverability
{
    public interface IRetryOperationProgressionNotifier
    {
        void Wait(string requestId, RetryType retryType);
        void Prepare(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages);
        void PrepareBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages);
        void Forwarding(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages);
        void BatchForwarded(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages);
        void Completed(string requestId, RetryType retryType, bool failed);
    }
}