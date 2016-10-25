namespace ServiceControl.Recoverability
{
    public class RetryOperationSummary
    {
        public RetryOperationSummary(string requestId, RetryType retryType)
        {
            this.requestId = requestId;
            this.retryType = retryType;
        }
        
        public IRetryOperationProgressionNotifier Notifier { get; set; }
        public int TotalNumberOfMessages { get; private set; }
        public int NumberOfMessagesPrepared { get; private set; }
        public int NumberOfMessagesForwarded { get; private set; }
        public bool Failed { get; private set; }
        public RetryState RetryState { get; private set; }
        private readonly string requestId;
        private readonly RetryType retryType;

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

            Notifier?.Wait(requestId, retryType, GetProgression());
        }

        public void Fail()
        {
            Failed = true;
        }

        public void Prepare(int totalNumberOfMessages)
        {
            RetryState = RetryState.Preparing;
            TotalNumberOfMessages = totalNumberOfMessages;
            NumberOfMessagesForwarded = 0;
            NumberOfMessagesPrepared = 0;

            Notifier?.Prepare(requestId, retryType, NumberOfMessagesPrepared, TotalNumberOfMessages, GetProgression());
        }

        public void PrepareBatch(int numberOfMessagesPrepared)
        {
            NumberOfMessagesPrepared = numberOfMessagesPrepared;

            Notifier?.PrepareBatch(requestId, retryType, NumberOfMessagesPrepared, TotalNumberOfMessages, GetProgression());
        }

        public void Forwarding()
        {
            RetryState = RetryState.Forwarding;

            Notifier?.Forwarding(requestId, retryType, NumberOfMessagesForwarded, TotalNumberOfMessages, GetProgression());
        }

        public void ForwardingAfterRestart(int totalNumberOfMessages)
        {
            TotalNumberOfMessages = totalNumberOfMessages;

            Forwarding();
        }

        public void BatchForwarded(int numberOfMessagesForwarded)
        {
            NumberOfMessagesForwarded = NumberOfMessagesForwarded + numberOfMessagesForwarded;

            Notifier?.BatchForwarded(requestId, retryType, NumberOfMessagesForwarded, TotalNumberOfMessages, GetProgression());
            
            if (NumberOfMessagesForwarded == TotalNumberOfMessages)
            {
                RetryState = RetryState.Completed;

                Notifier?.Completed(requestId, retryType, Failed, GetProgression());
            }
        }

        public double GetProgression()
        {
            return RetryOperationProgressionCalculator.CalculateProgression(TotalNumberOfMessages, NumberOfMessagesPrepared, NumberOfMessagesForwarded);
        }
    }
}