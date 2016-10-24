namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class RetryOperationSummary
    {
        public RetryOperationSummary(string requestId, RetryType retryType, IBus bus)
        {
            this.requestId = requestId;
            this.retryType = retryType;
            this.bus = bus;
        }
        
        private readonly string requestId;
        private readonly RetryType retryType;
        private readonly IBus bus;

        public int TotalNumberOfMessages { get; private set; }
        public int NumberOfMessagesPrepared { get; private set; }
        public int NumberOfMessagesForwarded { get; private set; }
        public bool Failed { get; private set; }
        public RetryState RetryState { get; private set; }

        public static string MakeOperationId(string requestId, RetryType retryType)
        {
            return $"{retryType}/{requestId}";
        }

        public void Wait()
        {
            RetryState = RetryState.Waiting;
            NumberOfMessagesPrepared = 0;
            NumberOfMessagesForwarded = 0;
            TotalNumberOfMessages = 0;

            bus.Publish<RetryOperationWaiting>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
            });
        }

        public void Fail()
        {
            Failed = true;
        }

        public void Prepare(int totalNumberOfMessages)
        {
            RetryState = RetryState.Preparing;
            TotalNumberOfMessages = totalNumberOfMessages;

            bus.Publish<RetryOperationPreparing>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.TotalNumberOfMessages = TotalNumberOfMessages;
                e.NumberOfMessagesPreparing = NumberOfMessagesPrepared;
            });
        }

        public void PrepareBatch(int numberOfMessagesPrepared)
        {
            NumberOfMessagesPrepared = numberOfMessagesPrepared;

            bus.Publish<RetryOperationPreparing>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.TotalNumberOfMessages = TotalNumberOfMessages;
                e.NumberOfMessagesPreparing = NumberOfMessagesPrepared;
            });
        }

        public void Forwarding()
        {
            RetryState = RetryState.Forwarding;

            bus.Publish<RetryOperationForwarding>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = NumberOfMessagesForwarded;
                e.TotalNumberOfMessages = TotalNumberOfMessages;
            });
        }

        public void BatchForwarded(int numberOfMessagesForwarded)
        {
            NumberOfMessagesForwarded = NumberOfMessagesForwarded + numberOfMessagesForwarded;

            bus.Publish<RetryMessagesForwarded>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = NumberOfMessagesForwarded;
                e.TotalNumberOfMessages = TotalNumberOfMessages;
            });

            if (NumberOfMessagesForwarded == TotalNumberOfMessages)
            {
                RetryState = RetryState.Completed;

                bus.Publish<RetryOperationCompleted>(e =>
                {
                    e.RequestId = requestId;
                    e.RetryType = retryType;
                    e.Failed = Failed;
                });
            }
        }
    }
}