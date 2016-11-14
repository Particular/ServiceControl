namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus.Logging;

    public class RetryOperationSummary
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(RetryOperationSummary));

        public RetryOperationSummary(string requestId, RetryType retryType)
        {
            this.requestId = requestId;
            this.retryType = retryType;
        }
        
        public IRetryOperationProgressionNotifier Notifier { get; set; }
        public int TotalNumberOfMessages { get; private set; }
        public int NumberOfMessagesPrepared { get; private set; }
        public int NumberOfMessagesForwarded { get; private set; }
        public int NumberOfMessagesSkipped { get; set; }
        public DateTime? CompletionTime { get; private set; }
        public bool Failed { get; private set; }
        public string Originator { get; private set; }
        public DateTime Started { get; private set; }
        public RetryState RetryState { get; private set; }
        private readonly string requestId;
        private readonly RetryType retryType;
        
        public static string MakeOperationId(string requestId, RetryType retryType)
        {
            return $"{retryType}/{requestId}";
        }

        public void Wait(DateTime started, string originator = null)
        {
            RetryState = RetryState.Waiting;
            NumberOfMessagesPrepared = 0;
            NumberOfMessagesForwarded = 0;
            TotalNumberOfMessages = 0;
            NumberOfMessagesSkipped = 0;
            CompletionTime = null;
            Originator = originator;
            Started = started;

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

        public void PrepareAdoptedBatch(int numberOfMessagesPrepared, string originator, DateTime startTime)
        {
            Originator = originator;
            Started = startTime;

            PrepareBatch(numberOfMessagesPrepared);
        }

        public void Forwarding()
        {
            RetryState = RetryState.Forwarding;

            Notifier?.Forwarding(requestId, retryType, NumberOfMessagesForwarded, TotalNumberOfMessages, GetProgression());
        }

        public void BatchForwarded(int numberOfMessagesForwarded)
        {
            NumberOfMessagesForwarded += numberOfMessagesForwarded;

            Notifier?.BatchForwarded(requestId, retryType, NumberOfMessagesForwarded, TotalNumberOfMessages, GetProgression());
            
            CheckForCompletion();
        }

        public void Skip(int numberOfMessagesSkipped)
        {
            NumberOfMessagesSkipped += numberOfMessagesSkipped;
            CheckForCompletion();
        }

        private void CheckForCompletion()
        {
            if (NumberOfMessagesForwarded + NumberOfMessagesSkipped == TotalNumberOfMessages)
            {
                RetryState = RetryState.Completed;
                CompletionTime = DateTime.Now;

                Notifier?.Completed(requestId, retryType, Failed, GetProgression(), Started, CompletionTime.Value, Originator);
                Log.Info($"Retry operation {requestId} completed. {NumberOfMessagesSkipped} messages skipped, {NumberOfMessagesForwarded} forwarded. Total {TotalNumberOfMessages}.");
            }
        }

        public double GetProgression()
        {
            var progression = RetryOperationProgressionCalculator.CalculateProgression(TotalNumberOfMessages, NumberOfMessagesPrepared, NumberOfMessagesForwarded, NumberOfMessagesSkipped, RetryState);
            return Math.Round(progression, 2);
        }
    }
}