namespace ServiceControl.Recoverability
{
    using System;

    public interface IRetryOperationProgressionNotifier
    {
        void Wait(string requestId, RetryType retryType, double progression);
        void Prepare(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, double progression);
        void PrepareBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, double progression);
        void Forwarding(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages, double progression);
        void BatchForwarded(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages, double progression);
        void Completed(string requestId, RetryType retryType, bool failed, double progression, DateTime startTime, DateTime completionTime, string originator);
    }
}