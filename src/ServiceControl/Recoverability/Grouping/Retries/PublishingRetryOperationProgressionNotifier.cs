namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class PublishingRetryOperationProgressionNotifier : IRetryOperationProgressionNotifier
    {
        private readonly IBus bus;

        public PublishingRetryOperationProgressionNotifier(IBus bus)
        {
            this.bus = bus;
        }

        public void Wait(string requestId, RetryType retryType)
        {
            bus.Publish<RetryOperationWaiting>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
            });
        }

        public void Prepare(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages)
        {
            bus.Publish<RetryOperationPreparing>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesPreparing = numberOfMessagesPrepared;
                e.TotalNumberOfMessages = totalNumberOfMessages;
            });
        }

        public void PrepareBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages)
        {
            bus.Publish<RetryOperationPreparing>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesPreparing = numberOfMessagesPrepared;
                e.TotalNumberOfMessages = totalNumberOfMessages;
            });
        }

        public void Forwarding(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages)
        {
            bus.Publish<RetryOperationForwarding>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = numberOfMessagesForwarded;
                e.TotalNumberOfMessages = totalNumberOfMessages;
            });

        }

        public void BatchForwarded(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages)
        {
            bus.Publish<RetryMessagesForwarded>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = numberOfMessagesForwarded;
                e.TotalNumberOfMessages = totalNumberOfMessages;
            });
        }

        public void Completed(string requestId, RetryType retryType, bool failed)
        {
            bus.Publish<RetryOperationCompleted>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.Failed = failed;
            });
        }
    }
}