namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using System;

    public class PublishingRetryOperationProgressNotifier : IRetryOperationProgressNotifier
    {
        private readonly IBus bus;

        public PublishingRetryOperationProgressNotifier(IBus bus)
        {
            this.bus = bus;
        }

        public void Wait(string requestId, RetryType retryType, Progress progress, DateTime startTime)
        {
            bus.Publish<RetryOperationWaiting>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.Progress = progress;
                e.StartTime = startTime;
            });
        }

        public void Prepare(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime)
        {
            bus.Publish<RetryOperationPreparing>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.TotalNumberOfMessages = totalNumberOfMessages;
                e.Progress = progress;
                e.IsFailed = isFailed;
                e.StartTime = startTime;
            });
        }

        public void PrepareBatch(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime)
        {
            bus.Publish<RetryOperationPreparing>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.TotalNumberOfMessages = totalNumberOfMessages;
                e.Progress = progress;
                e.IsFailed = isFailed;
                e.StartTime = startTime;
            });
        }

        public void Forwarding(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime)
        {
            bus.Publish<RetryOperationForwarding>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.TotalNumberOfMessages = totalNumberOfMessages;
                e.Progress = progress;
                e.IsFailed = isFailed;
                e.StartTime = startTime;
            });

        }

        public void BatchForwarded(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime)
        {
            bus.Publish<RetryMessagesForwarded>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.TotalNumberOfMessages = totalNumberOfMessages;
                e.Progress = progress;
                e.IsFailed = isFailed;
                e.StartTime = startTime;
            });
        }

        public void Completed(string requestId, RetryType retryType, bool failed, Progress progress, DateTime startTime, DateTime completionTime, string originator, int numberOfMessagesProcessed)
        {
            bus.Publish<RetryOperationCompleted>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.Failed = failed;
                e.Progress = progress;
                e.StartTime = startTime;
                e.CompletionTime = completionTime;
                e.Originator = originator;
                e.NumberOfMessagesProcessed = numberOfMessagesProcessed;
            });
        }
    }
}