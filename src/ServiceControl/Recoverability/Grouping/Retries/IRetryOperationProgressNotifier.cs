namespace ServiceControl.Recoverability
{
    using System;

    public interface IRetryOperationProgressNotifier
    {
        void Wait(string requestId, RetryType retryType, Progress progress);
        void Prepare(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress);
        void PrepareBatch(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress);
        void Forwarding(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress);
        void BatchForwarded(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress);
        void Completed(string requestId, RetryType retryType, bool failed, Progress progress, DateTime startTime, DateTime completionTime, string originator);
    }
}