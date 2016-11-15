namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using System;

    public class PublishingRetryOperationProgressionNotifier : IRetryOperationProgressionNotifier
    {
        private readonly IBus bus;

        public PublishingRetryOperationProgressionNotifier(IBus bus)
        {
            this.bus = bus;
        }

        public void Wait(string requestId, RetryType retryType, double progression, int? slot)
        {
            bus.Publish<RetryOperationWaiting>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.Progression = progression;
                e.Slot = slot;
            });
        }

        public void Prepare(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, double progression, int? slot)
        {
            bus.Publish<RetryOperationPreparing>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesPreparing = numberOfMessagesPrepared;
                e.TotalNumberOfMessages = totalNumberOfMessages;
                e.Progression = progression;
                e.Slot = slot;
            });
        }

        public void PrepareBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, double progression)
        {
            bus.Publish<RetryOperationPreparing>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesPreparing = numberOfMessagesPrepared;
                e.TotalNumberOfMessages = totalNumberOfMessages;
                e.Progression = progression;
            });
        }

        public void Forwarding(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages, double progression)
        {
            bus.Publish<RetryOperationForwarding>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = numberOfMessagesForwarded;
                e.TotalNumberOfMessages = totalNumberOfMessages;
                e.Progression = progression;
            });

        }

        public void BatchForwarded(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages, double progression)
        {
            bus.Publish<RetryMessagesForwarded>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = numberOfMessagesForwarded;
                e.TotalNumberOfMessages = totalNumberOfMessages;
                e.Progression = progression;
            });
        }

        public void Completed(string requestId, RetryType retryType, bool failed, double progression, DateTime startTime, DateTime completionTime, string originator)
        {
            bus.Publish<RetryOperationCompleted>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.Failed = failed;
                e.Progression = progression;
                e.StartTime = startTime;
                e.CompletionTime = completionTime;
                e.Originator = originator;
            });
        }
    }
}