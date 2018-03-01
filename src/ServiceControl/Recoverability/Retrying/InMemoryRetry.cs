namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus.Logging;
    using ServiceControl.Infrastructure.DomainEvents;

    public class InMemoryRetry
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(InMemoryRetry));

        IDomainEvents domainEvents;

        public InMemoryRetry(string requestId, RetryType retryType, IDomainEvents domainEvents)
        {
            this.requestId = requestId;
            this.retryType = retryType;
            this.domainEvents = domainEvents;
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

            domainEvents.Raise(new RetryOperationWaiting
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

            domainEvents.Raise(new RetryOperationPreparing
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

            domainEvents.Raise(new RetryOperationPreparing
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

            domainEvents.Raise(new RetryOperationForwarding
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

            domainEvents.Raise(new RetryMessagesForwarded
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

            domainEvents.Raise(new RetryOperationCompleted
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
                domainEvents.Raise(new MessagesSubmittedForRetry
                {
                    FailedMessageIds = new string[0],
                    NumberOfFailedMessages = NumberOfMessagesForwarded,
                    Context = Originator
                });
            }

            Log.Info($"Retry operation {requestId} completed. {NumberOfMessagesSkipped} messages skipped, {NumberOfMessagesForwarded} forwarded. Total {TotalNumberOfMessages}.");
        }

        public RetryProgress GetProgress()
        {
            var percentage = OperationProgressCalculator.CalculateProgress(TotalNumberOfMessages, NumberOfMessagesPrepared, NumberOfMessagesForwarded, NumberOfMessagesSkipped, RetryState);
            var roundedPercentage = Math.Round(percentage, 2);

            var remaining = TotalNumberOfMessages - (NumberOfMessagesForwarded + NumberOfMessagesSkipped);

            return new RetryProgress(roundedPercentage, NumberOfMessagesPrepared, NumberOfMessagesForwarded, NumberOfMessagesSkipped, remaining);
        }

        public bool NeedsAcknowledgement()
        {
            return RetryState == RetryState.Completed;
        }

        public bool IsInProgress()
        {
            return RetryState != RetryState.Completed && RetryState != RetryState.Waiting;
        }
    }
}
