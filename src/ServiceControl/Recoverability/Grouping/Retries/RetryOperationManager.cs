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
            SetStatus(requestId, retryType, RetryState.Waiting, 0, 0);

            bus.Publish<RetryOperationWaiting>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
            });
        }

        public void SetStateAsPreparingMessages(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages)
        {
            SetStatus(requestId, retryType, RetryState.Preparing, numberOfMessagesPrepared, totalNumberOfMessages);

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

            SetStatus(requestId, retryType, RetryState.Forwarding, currentStatus.NumberOfMessagesCompleted ?? 0, currentStatus.TotalNumberOfMessages ?? 0);

            bus.Publish<RetryOperationForwarding>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = currentStatus.NumberOfMessagesCompleted ?? 0;
                e.TotalNumberOfMessages = currentStatus.TotalNumberOfMessages ?? 0;
            });
        }

        public void SetStateAsForwardedMessages(string requestId, RetryType retryType, int numberOfMessagesForwarded)
        {
            var currentStatus = GetStatusForRetryOperation(requestId, retryType);

            SetStatus(requestId, retryType, RetryState.Forwarding, currentStatus.NumberOfMessagesCompleted ?? 0 + numberOfMessagesForwarded, currentStatus.TotalNumberOfMessages ?? 0);

            bus.Publish<RetryMessagesForwarded>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = numberOfMessagesForwarded;
                e.TotalNumberOfMessages = currentStatus.TotalNumberOfMessages ?? 0;
            });

            if (currentStatus.NumberOfMessagesCompleted == currentStatus.TotalNumberOfMessages)
            {
                SetStateAsCompleted(requestId, retryType);
            }
        }

        void SetStateAsCompleted(string requestId, RetryType retryType)
        {
            SetStatus(requestId, retryType, RetryState.Completed, 0, 0);

            bus.Publish<RetryOperationCompleted>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
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

            summary.WasComplete = false;
        }

        static void SetStatus(string requestId, RetryType retryType, RetryState state, int numberOfMessagesCompleted, int totalNumberOfMessages)
        {
            RetryOperationSummary summary;
            if (!CurrentRetryGroups.TryGetValue(RetryOperationSummary.MakeOperationId(requestId, retryType), out summary))
            {
                CurrentRetryGroups[RetryOperationSummary.MakeOperationId(requestId, retryType)] = new RetryOperationSummary();
            }

            summary.RetryState = state;
            summary.NumberOfMessagesCompleted = numberOfMessagesCompleted;
            summary.TotalNumberOfMessages = totalNumberOfMessages;
        }

        public RetryOperationSummary GetStatusForRetryOperation(string requestId, RetryType retryType)
        {
            RetryOperationSummary summary = null;
            CurrentRetryGroups.TryGetValue(RetryOperationSummary.MakeOperationId(requestId, retryType), out summary);

            return summary;
        }

        IBus bus;
    }

    public class RetryOperationSummary
    {
        public int? TotalNumberOfMessages { get; internal set; }
        public int? NumberOfMessagesCompleted { get; internal set; }

        public bool WasComplete { get; set; }
        public RetryState RetryState { get; set; }

        public static string MakeOperationId(string requestId, RetryType retryType)
        {
            return $"{retryType}/{requestId}";
        }
    }

    public enum RetryState
    {
        Waiting,
        Preparing,
        Forwarding,
        Completed
    }
}