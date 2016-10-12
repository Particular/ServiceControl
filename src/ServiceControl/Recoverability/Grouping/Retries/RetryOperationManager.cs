namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;

    public class RetryOperationSummary
    {
        static Dictionary<string, RetryOperationSummary> CurrentRetryGroups = new Dictionary<string, RetryOperationSummary>();

        public static void SetInProgress(string requestId, RetryType retryType, int numberOfMessages)
        {
            // SignalR

            SetStatus(requestId, retryType, numberOfMessages);
        }

        public static void MarkMessagesAsForwarded(string requestId, RetryType retryType, int numberOfMessagesForwarded)
        {
            var currentStatus = GetStatusForRetryOperation(requestId, retryType);

            currentStatus.MessagesRemaining -= numberOfMessagesForwarded;

            // SignalR

            if (currentStatus.MessagesRemaining == 0)
            {
                CurrentRetryGroups.Remove(MakeOperationId(requestId, retryType));
            }
        }

        static void SetStatus(string requestId, RetryType retryType, int numberOfMessages)
        {
            RetryOperationSummary summary;
            if (!CurrentRetryGroups.TryGetValue(MakeOperationId(requestId, retryType), out summary))
            {
                CurrentRetryGroups[MakeOperationId(requestId, retryType)] = new RetryOperationSummary { MessagesRemaining = numberOfMessages };
            }
        }

        static void SetStatus(string requestId, RetryType retryType)
        {
            RetryOperationSummary summary;
            if (!CurrentRetryGroups.TryGetValue(MakeOperationId(requestId, retryType), out summary))
            {
                CurrentRetryGroups[MakeOperationId(requestId, retryType)] = new RetryOperationSummary();
            }
        }

        public static RetryOperationSummary GetStatusForRetryOperation(string requestId, RetryType retryType)
        {
            RetryOperationSummary summary = null;
            CurrentRetryGroups.TryGetValue(MakeOperationId(requestId, retryType), out summary);

            return summary;
        }

        public int? MessagesRemaining { get; internal set; }

        static string MakeOperationId(string requestId, RetryType retryType)
        {
            return $"{retryType}/{requestId}";
        }
    }
}