namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using System.Collections.Generic;

    public class RetryOperationManager
    {
        public RetryOperationManager(IBus bus)
        {
            this.bus = bus;
        }

        static Dictionary<string, RetryOperationSummary> CurrentRetryGroups = new Dictionary<string, RetryOperationSummary>();

        public void SetStateAsWaiting(string requestId, RetryType retryType)
        {
            SetStatus(requestId, retryType, RetryState.Waiting, 0, 0, 0);

            bus.Publish<RetryOperationWaiting>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
            });
        }

        public void SetStateAsPreparingMessages(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages)
        {
            SetStatus(requestId, retryType, RetryState.Preparing, numberOfMessagesPrepared, 0, totalNumberOfMessages);

            bus.Publish<RetryOperationPreparing>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.TotalNumberOfMessages = totalNumberOfMessages;
                e.NumberOfMessagesPreparing = numberOfMessagesPrepared;
            });
        }

        public void SetStateAsForwardingMessages(string requestId, RetryType retryType)
        {
            var currentStatus = GetStatusForRetryOperation(requestId, retryType);

            SetStatus(requestId, retryType, RetryState.Forwarding, currentStatus.NumberOfMessagesPrepared, currentStatus.NumberOfMessagesForwarded, currentStatus.TotalNumberOfMessages);

            bus.Publish<RetryOperationForwarding>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = currentStatus.NumberOfMessagesForwarded;
                e.TotalNumberOfMessages = currentStatus.TotalNumberOfMessages;
            });
        }

        public void SetStateAsForwardedMessages(string requestId, RetryType retryType, int numberOfMessagesForwarded)
        {
            var currentStatus = GetStatusForRetryOperation(requestId, retryType);

            SetStatus(requestId, retryType, RetryState.Forwarding, currentStatus.NumberOfMessagesPrepared, currentStatus.NumberOfMessagesForwarded + numberOfMessagesForwarded, currentStatus.TotalNumberOfMessages);

            bus.Publish<RetryMessagesForwarded>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = numberOfMessagesForwarded;
                e.TotalNumberOfMessages = currentStatus.TotalNumberOfMessages;
            });

            if (currentStatus.NumberOfMessagesForwarded == currentStatus.TotalNumberOfMessages)
            {
                SetStateAsCompleted(requestId, retryType, currentStatus.Failed);
            }
        }

        void SetStateAsCompleted(string requestId, RetryType retryType, bool failed)
        {
            SetStatus(requestId, retryType, RetryState.Completed, 0, 0, 0);

            bus.Publish<RetryOperationCompleted>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.Failed = failed;
            });
        }

        public void WarnOfPossibleIncompleteDocumentMarking(RetryType retryType, string requestId)
        {
            RetryOperationSummary summary;
            if (!CurrentRetryGroups.TryGetValue(RetryOperationSummary.MakeOperationId(requestId, retryType), out summary))
            {
                summary = new RetryOperationSummary();
                CurrentRetryGroups[RetryOperationSummary.MakeOperationId(requestId, retryType)] = summary;
            }

            summary.Failed = true;
        }

        static void SetStatus(string requestId, RetryType retryType, RetryState state, int numberOfMessagesPrepared, int numberOfMessagesForwarded, int totalNumberOfMessages)
        {
            RetryOperationSummary summary;
            if (!CurrentRetryGroups.TryGetValue(RetryOperationSummary.MakeOperationId(requestId, retryType), out summary))
            {
                summary = new RetryOperationSummary();
                CurrentRetryGroups[RetryOperationSummary.MakeOperationId(requestId, retryType)] = summary;
            }

            summary.RetryState = state;
            summary.NumberOfMessagesPrepared = numberOfMessagesPrepared;
            summary.NumberOfMessagesForwarded = numberOfMessagesForwarded;
            summary.TotalNumberOfMessages = totalNumberOfMessages;
        }

        public RetryOperationSummary GetStatusForRetryOperation(string requestId, RetryType retryType)
        {
            RetryOperationSummary summary;
            CurrentRetryGroups.TryGetValue(RetryOperationSummary.MakeOperationId(requestId, retryType), out summary);

            return summary;
        }

        IBus bus;
    }

    public class RetryOperationSummary
    {
        public int TotalNumberOfMessages { get; internal set; }
        public int NumberOfMessagesPrepared { get; internal set; }
        public int NumberOfMessagesForwarded { get; internal set; }

        public bool Failed { get; set; }
        public RetryState RetryState { get; set; }

        public static string MakeOperationId(string requestId, RetryType retryType)
        {
            return $"{retryType}/{requestId}";
        }

        public double GetProgressPercentage()
        {
            const double waitingWeight = 0.05;
            const double prepairedWeight = 0.475;
            const double forwardedWeight = 0.475;

            if (RetryState == RetryState.Waiting)
            {
                return waitingWeight;
            }

            if (RetryState == RetryState.Completed)
            {
                return 1.0;
            }

            double total = TotalNumberOfMessages;
            double preparedPercentage = NumberOfMessagesPrepared / total;
            double forwardedPercentage = NumberOfMessagesForwarded / total;

            return waitingWeight + preparedPercentage*prepairedWeight + forwardedPercentage*forwardedWeight;
        }
    }

    public enum RetryState
    {
        Waiting,
        Preparing,
        Forwarding,
        Completed,
    }
}