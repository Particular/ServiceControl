namespace ServiceControl.Recoverability
{
    using System;

    public interface IRetryOperationProgressionNotifier
    {
        void Wait(string requestId, RetryType retryType, Progression progression, int? slot);
        void Prepare(string requestId, RetryType retryType, int totalNumberOfMessages, Progression progression);
        void PrepareBatch(string requestId, RetryType retryType, int totalNumberOfMessages, Progression progression);
        void Forwarding(string requestId, RetryType retryType, int totalNumberOfMessages, Progression progression);
        void BatchForwarded(string requestId, RetryType retryType, int totalNumberOfMessages, Progression progression);
        void Completed(string requestId, RetryType retryType, bool failed, Progression progression, DateTime startTime, DateTime completionTime, string originator);
    }
}