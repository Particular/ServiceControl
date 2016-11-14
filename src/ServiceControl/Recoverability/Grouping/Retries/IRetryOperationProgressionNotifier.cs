﻿namespace ServiceControl.Recoverability
{
    using System;

    public interface IRetryOperationProgressionNotifier
    {
        void Wait(string requestId, RetryType retryType, double progression, int? slot);
        void Prepare(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, double progression, int? slot);
        void PrepareBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, double progression);
        void Forwarding(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages, double progression);
        void BatchForwarded(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages, double progression);
        void Completed(string requestId, RetryType retryType, bool failed, double progression, DateTime startTime, DateTime completionTime, string originator);
    }
}