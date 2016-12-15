namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus.Logging;
    using ServiceControl.Infrastructure.DomainEvents;

    public class RetryOperation
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(RetryOperation));

        public RetryOperation(string requestId, RetryType retryType)
        {
            this.requestId = requestId;
            this.retryType = retryType;
        }
        
        public int TotalNumberOfMessages { get; private set; }
        public int NumberOfMessagesPrepared { get; private set; }
        public int NumberOfMessagesForwarded { get; private set; }
        public int NumberOfMessagesSkipped { get; set; }
        public DateTime? CompletionTime { get; private set; }
        public DateTime? Last{ get; private set; }
        public bool Failed { get; private set; }
        public string Originator { get; private set; }
        public string Classifier { get; private set; }
        public DateTime Started { get; private set; }
        public RetryState RetryState { get; private set; }
        private readonly string requestId;
        private readonly RetryType retryType;


        public static string MakeOperationId(string requestId, RetryType retryType)
        {
            return $"{retryType}/{requestId}";
        }

        public void Wait(DateTime started, string originator = null, string classifier = null, DateTime? last = null)
        {
            RetryState = RetryState.Waiting;
            NumberOfMessagesPrepared = 0;
            NumberOfMessagesForwarded = 0;
            TotalNumberOfMessages = 0;
            NumberOfMessagesSkipped = 0;
            CompletionTime = null;
            Originator = originator;
            Started = started;
            Failed = false;
            Last = last;
            Classifier = classifier;
            
            DomainEvents.Raise(new RetryOperationWaiting
            {
                RequestId = requestId,
                RetryType = retryType,
                Progress = GetProgress(),
                StartTime = Started
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
            NumberOfMessagesForwarded = 0;
            NumberOfMessagesPrepared = 0;

            DomainEvents.Raise(new RetryOperationPreparing 
            {
                RequestId = requestId,
                RetryType = retryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            });
        }

        public void PrepareBatch(int numberOfMessagesPrepared)
        {
            NumberOfMessagesPrepared = numberOfMessagesPrepared;

            DomainEvents.Raise(new RetryOperationPreparing
            {
                RequestId = requestId,
                RetryType = retryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started,
            });
        }

        public void PrepareAdoptedBatch(int numberOfMessagesPrepared, string originator, string classifier, DateTime startTime, DateTime last)
        {
            Originator = originator;
            Started = startTime;
            Last = last;
            Classifier = classifier;

            PrepareBatch(numberOfMessagesPrepared);
        }

        public void Forwarding()
        {
            RetryState = RetryState.Forwarding;

            DomainEvents.Raise(new RetryOperationForwarding
            {
                RequestId = requestId,
                RetryType = retryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            });
        }

        public void BatchForwarded(int numberOfMessagesForwarded)
        {
            NumberOfMessagesForwarded += numberOfMessagesForwarded;

            DomainEvents.Raise(new RetryMessagesForwarded
            {
                RequestId = requestId,
                RetryType = retryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started,
            });
            
            CheckForCompletion();
        }

        public void Skip(int numberOfMessagesSkipped)
        {
            NumberOfMessagesSkipped += numberOfMessagesSkipped;
            CheckForCompletion();
        }

        private void CheckForCompletion()
        {
            if (NumberOfMessagesForwarded + NumberOfMessagesSkipped != TotalNumberOfMessages)
            {
                return;
            }

            RetryState = RetryState.Completed;
            CompletionTime = DateTime.UtcNow;

            DomainEvents.Raise(new RetryOperationCompleted
            {
                RequestId = requestId,
                RetryType = retryType,
                Failed = Failed,
                Progress = GetProgress(),
                StartTime = Started,
                CompletionTime = CompletionTime.Value,
                Originator = Originator,
                NumberOfMessagesProcessed = NumberOfMessagesForwarded,
                Last = Last ?? DateTime.MaxValue,
                Classifier = Classifier,
            });

            if (retryType == RetryType.FailureGroup)
            {
                DomainEvents.Raise(new MessagesSubmittedForRetry
                {
                    FailedMessageIds = new string[0],
                    NumberOfFailedMessages = NumberOfMessagesForwarded,
                    Context = Originator
                });
            }
                
            Log.Info($"Retry operation {requestId} completed. {NumberOfMessagesSkipped} messages skipped, {NumberOfMessagesForwarded} forwarded. Total {TotalNumberOfMessages}.");
        }
        
        public Progress GetProgress()
        {
            var percentage = RetryOperationProgressCalculator.CalculateProgress(TotalNumberOfMessages, NumberOfMessagesPrepared, NumberOfMessagesForwarded, NumberOfMessagesSkipped, RetryState);
            var roundedPercentage = Math.Round(percentage, 2);
            
            var remaining = TotalNumberOfMessages - (NumberOfMessagesForwarded + NumberOfMessagesSkipped);

            return new Progress(roundedPercentage, NumberOfMessagesPrepared, NumberOfMessagesForwarded, NumberOfMessagesSkipped, remaining);
        }

        public bool NeedsAcknowledgement()
        {
            return RetryState == RetryState.Completed;
        }
    }
}
