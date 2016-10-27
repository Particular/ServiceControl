﻿namespace ServiceControl.Recoverability
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

        public void SetInProgress(string requestId, RetryType retryType, int numberOfMessages)
        {
            SetStatus(requestId, retryType, numberOfMessages);

            bus.Publish<RetryOperationStarted>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessages = numberOfMessages;
            });
        }

        public void MarkMessagesAsForwarded(string requestId, RetryType retryType, int numberOfMessagesForwarded)
        {
            var currentStatus = GetStatusForRetryOperation(requestId, retryType);

            currentStatus.MessagesRemaining -= numberOfMessagesForwarded;

            bus.Publish<RetryMessagesForwarded>(e =>
            {
                e.RequestId = requestId;
                e.RetryType = retryType;
                e.NumberOfMessagesForwarded = numberOfMessagesForwarded;
            });

            if (currentStatus.MessagesRemaining == 0)
            {
                CurrentRetryGroups.Remove(RetryOperationSummary.MakeOperationId(requestId, retryType));

                bus.Publish<RetryOperationCompleted>(e =>
                {
                    e.RequestId = requestId;
                    e.RetryType = retryType;
                });
            }
        }

        public void WarnOfPossibleIncompleteDocumentMarking(RetryType retryType, string requestId)
        {
            RetryOperationSummary summary;
            if (!CurrentRetryGroups.TryGetValue(RetryOperationSummary.MakeOperationId(requestId, retryType), out summary))
            {
                summary = new RetryOperationSummary();
                CurrentRetryGroups[RetryOperationSummary.MakeOperationId(requestId, retryType)] = summary;
            }

            summary.IsPossiblyIncomplete = true;
        }

        static void SetStatus(string requestId, RetryType retryType, int numberOfMessages)
        {
            RetryOperationSummary summary;
            if (!CurrentRetryGroups.TryGetValue(RetryOperationSummary.MakeOperationId(requestId, retryType), out summary))
            {
                CurrentRetryGroups[RetryOperationSummary.MakeOperationId(requestId, retryType)] = new RetryOperationSummary { MessagesRemaining = numberOfMessages };
            }
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
        public int? MessagesRemaining { get; internal set; }

        public bool IsPossiblyIncomplete { get; set; } // Need a better name for this

        public static string MakeOperationId(string requestId, RetryType retryType)
        {
            return $"{retryType}/{requestId}";
        }
    }
}