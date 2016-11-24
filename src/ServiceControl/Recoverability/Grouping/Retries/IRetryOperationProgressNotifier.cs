namespace ServiceControl.Recoverability
{
    using System;

    public interface IRetryOperationProgressNotifier
    {
        void Wait(string requestId, RetryType retryType, Progress progress, DateTime startTime);
        void Prepare(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime);
        void PrepareBatch(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime);
        void Forwarding(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime);
        void BatchForwarded(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime);
        void Completed(string requestId, RetryType retryType, bool failed, Progress progress, DateTime startTime, DateTime completionTime, string originator, int numberOfMessagesProcessed);
    }
}