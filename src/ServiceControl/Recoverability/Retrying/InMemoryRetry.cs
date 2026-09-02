namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability.Retrying.Metrics;

    public class InMemoryRetry
    {
        public InMemoryRetry(string requestId, RetryType retryType, IDomainEvents domainEvents, RetryMetrics metrics, ILogger logger, TimeProvider timeProvider)
        {
            RequestId = requestId;
            RetryType = retryType;
            this.domainEvents = domainEvents;
            this.metrics = metrics;
            this.logger = logger;
            operationStartTimestamp = metrics.GetTimestamp();
            this.timeProvider = timeProvider;
        }

        public string RequestId { get; }
        public RetryType RetryType { get; }
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

        public Task Wait(DateTime started, string originator = null, string classifier = null, DateTime? last = null, CancellationToken cancellationToken = default)
        {
            RetryState = RetryState.Waiting;
            operationStartTimestamp = metrics.GetTimestamp();
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
                RequestId = RequestId,
                RetryType = RetryType,
                Progress = GetProgress(),
                StartTime = Started
            }, cancellationToken);
        }

        public void Fail()
        {
            Failed = true;
        }

        public Task Prepare(int totalNumberOfMessages, DateTime startTime, string originator, CancellationToken cancellationToken = default)
        {
            // A completed operation being prepared again is a new run that never went through Wait.
            var isNewRun = RetryState == RetryState.Completed;

            if (isNewRun)
            {
                operationStartTimestamp = metrics.GetTimestamp();
            }

            // Only a group retry goes through Wait, which stamps these; every other type gets them here.
            if (isNewRun || Started == default)
            {
                Started = startTime;
                Originator = originator;
            }

            RetryState = RetryState.Preparing;
            TotalNumberOfMessages = totalNumberOfMessages;
            NumberOfMessagesForwarded = 0;
            NumberOfMessagesPrepared = 0;

            return domainEvents.Raise(new RetryOperationPreparing
            {
                RequestId = RequestId,
                RetryType = RetryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            }, cancellationToken);
        }

        public Task PrepareBatch(int numberOfMessagesPrepared, CancellationToken cancellationToken = default)
        {
            NumberOfMessagesPrepared = numberOfMessagesPrepared;

            return domainEvents.Raise(new RetryOperationPreparing
            {
                RequestId = RequestId,
                RetryType = RetryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            }, cancellationToken);
        }

        public Task PrepareAdoptedBatch(int numberOfMessagesPrepared, string originator, string classifier, DateTime startTime, DateTime last, CancellationToken cancellationToken = default)
        {
            Originator = originator;
            Started = startTime;
            Last = last;
            Classifier = classifier;

            return PrepareBatch(numberOfMessagesPrepared, cancellationToken);
        }

        public Task Forwarding(CancellationToken cancellationToken = default)
        {
            RetryState = RetryState.Forwarding;

            return domainEvents.Raise(new RetryOperationForwarding
            {
                RequestId = RequestId,
                RetryType = RetryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            }, cancellationToken);
        }

        public async Task BatchForwarded(int numberOfMessagesForwarded, CancellationToken cancellationToken = default)
        {
            NumberOfMessagesForwarded += numberOfMessagesForwarded;
            metrics.RecordMessages(RetryType, RetryMessageOutcome.Forwarded, numberOfMessagesForwarded);

            await domainEvents.Raise(new RetryMessagesForwarded
            {
                RequestId = RequestId,
                RetryType = RetryType,
                TotalNumberOfMessages = TotalNumberOfMessages,
                Progress = GetProgress(),
                IsFailed = Failed,
                StartTime = Started
            }, cancellationToken);

            await CheckForCompletion(cancellationToken);
        }

        public Task Skip(int numberOfMessagesSkipped, CancellationToken cancellationToken = default)
        {
            NumberOfMessagesSkipped += numberOfMessagesSkipped;
            metrics.RecordMessages(RetryType, RetryMessageOutcome.Skipped, numberOfMessagesSkipped);
            return CheckForCompletion(cancellationToken);
        }

        async Task CheckForCompletion(CancellationToken cancellationToken)
        {
            if (NumberOfMessagesForwarded + NumberOfMessagesSkipped != TotalNumberOfMessages)
            {
                return;
            }

            RetryState = RetryState.Completed;
            CompletionTime = timeProvider.GetUtcNow().UtcDateTime;
            metrics.RecordOperationCompleted(RetryType, operationStartTimestamp, Failed);

            await domainEvents.Raise(new RetryOperationCompleted
            {
                RequestId = RequestId,
                RetryType = RetryType,
                Failed = Failed,
                Progress = GetProgress(),
                StartTime = Started,
                CompletionTime = CompletionTime.Value,
                Originator = Originator,
                NumberOfMessagesProcessed = NumberOfMessagesForwarded,
                Last = Last ?? DateTime.MaxValue,
                Classifier = Classifier
            }, cancellationToken);

            if (RetryType == RetryType.FailureGroup)
            {
                await domainEvents.Raise(new MessagesSubmittedForRetry
                {
                    FailedMessageIds = new string[0],
                    NumberOfFailedMessages = NumberOfMessagesForwarded,
                    Context = Originator
                }, cancellationToken);
            }

            logger.LogInformation("Retry operation {RequestId} completed. {NumberOfMessagesSkipped} messages skipped, {NumberOfMessagesForwarded} forwarded. Total {TotalNumberOfMessages}",
                RequestId,
                NumberOfMessagesSkipped,
                NumberOfMessagesForwarded,
                TotalNumberOfMessages);
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
            return RetryState is not RetryState.Completed and not RetryState.Waiting;
        }


        long operationStartTimestamp;

        IDomainEvents domainEvents;
        readonly RetryMetrics metrics;
        readonly ILogger logger;
        readonly TimeProvider timeProvider;
    }
}