namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;

    public class InMemoryRetry
    {
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
        public DateTime? Last { get; private set; }
        public bool Failed { get; private set; }
        public string Originator { get; private set; }
        public string Classifier { get; private set; }
        public DateTime Started { get; private set; }
        public RetryState RetryState { get; private set; }


        public static string MakeOperationId(string requestId, RetryType retryType)
        {
            return $"{retryType}/{requestId}";
        }

        public Task Wait(DateTime started, string originator = null, string classifier = null, DateTime? last = null)
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

            return domainEvents.Raise(new RetryOperationWaiting
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

        public Task Prepare(int totalNumberOfMessages)
        {
            RetryState = RetryState.Preparing;
            TotalNumberOfMessages = totalNumberOfMessages;
            NumberOfMessagesForwarded = 0;
            NumberOfMessagesPrepared = 0;

            return domainEvents.Raise(new RetryOperationPreparing
            {
                RequestId = requestId,
                RetryType = retryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            });
        }

        public Task PrepareBatch(int numberOfMessagesPrepared)
        {
            NumberOfMessagesPrepared = numberOfMessagesPrepared;

            return domainEvents.Raise(new RetryOperationPreparing
            {
                RequestId = requestId,
                RetryType = retryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            });
        }

        public Task PrepareAdoptedBatch(int numberOfMessagesPrepared, string originator, string classifier, DateTime startTime, DateTime last)
        {
            Originator = originator;
            Started = startTime;
            Last = last;
            Classifier = classifier;

            return PrepareBatch(numberOfMessagesPrepared);
        }

        public Task Forwarding()
        {
            RetryState = RetryState.Forwarding;

            return domainEvents.Raise(new RetryOperationForwarding
            {
                RequestId = requestId,
                RetryType = retryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            });
        }

        public async Task BatchForwarded(int numberOfMessagesForwarded)
        {
            NumberOfMessagesForwarded += numberOfMessagesForwarded;

            await domainEvents.Raise(new RetryMessagesForwarded
            {
                RequestId = requestId,
                RetryType = retryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            }).ConfigureAwait(false);

            await CheckForCompletion();
        }

        public Task Skip(int numberOfMessagesSkipped)
        {
            NumberOfMessagesSkipped += numberOfMessagesSkipped;
            return CheckForCompletion();
        }

        private async Task CheckForCompletion()
        {
            if (NumberOfMessagesForwarded + NumberOfMessagesSkipped != TotalNumberOfMessages)
            {
                return;
            }

            RetryState = RetryState.Completed;
            CompletionTime = DateTime.UtcNow;

            await domainEvents.Raise(new RetryOperationCompleted
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
                Classifier = Classifier
            }).ConfigureAwait(false);

            if (retryType == RetryType.FailureGroup)
            {
                await domainEvents.Raise(new MessagesSubmittedForRetry
                {
                    FailedMessageIds = new string[0],
                    NumberOfFailedMessages = NumberOfMessagesForwarded,
                    Context = Originator
                }).ConfigureAwait(false);
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

        private readonly string requestId;
        private readonly RetryType retryType;

        IDomainEvents domainEvents;
        static readonly ILog Log = LogManager.GetLogger(typeof(InMemoryRetry));
    }
}