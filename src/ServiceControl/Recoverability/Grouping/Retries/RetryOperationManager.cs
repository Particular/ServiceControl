﻿namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    public class RetryOperationManager
    {
        private readonly IRetryOperationProgressionNotifier notifier;
        
        public RetryOperationManager(IRetryOperationProgressionNotifier notifier)
        {
            this.notifier = notifier;
        }

        static Dictionary<string, RetryOperationSummary> Operations = new Dictionary<string, RetryOperationSummary>();

        public void Wait(string requestId, RetryType retryType, string originator = null)
        {
            var summary = GetOrCreate(retryType, requestId);

            summary.Wait(originator);
        }

        public void PrepareAdoptedBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, string originator)
        {
            Prepairing(requestId, retryType, totalNumberOfMessages);

            PreparedBatch(requestId, retryType, numberOfMessagesPrepared, originator);
        }

        public void Prepairing(string requestId, RetryType retryType, int totalNumberOfMessages)
        {
            var summary = GetOrCreate(retryType, requestId);

            summary.Prepare(totalNumberOfMessages);
        }

        public void PreparedBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, string originator)
        {
            var summary = GetOrCreate(retryType, requestId);

            summary.PrepareBatch(numberOfMessagesPrepared, originator);
        }

        public void Forwarding(string requestId, RetryType retryType)
        {
            var summary = Get(requestId, retryType);

            summary.Forwarding();
        }

        public void ForwardingAfterRestart(string requestId, RetryType retryType, int totalNumberOfMessages, string originator)
        {
            var summary = GetOrCreate(retryType, requestId);

            summary.ForwardingAfterRestart(totalNumberOfMessages, originator);
        }

        public void ForwardedBatch(string requestId, RetryType retryType, int numberOfMessagesForwarded)
        {
            var summary = Get(requestId, retryType);

            summary.BatchForwarded(numberOfMessagesForwarded);
        }

        public void Fail(RetryType retryType, string requestId)
        {
            var summary = GetOrCreate(retryType, requestId);

            summary.Fail();
        }

        public void Skip(string requestId, RetryType retryType, int numberOfMessagesSkipped)
        {
            var summary = GetOrCreate(retryType, requestId);
            summary.Skip(numberOfMessagesSkipped);
        }

        private RetryOperationSummary GetOrCreate(RetryType retryType, string requestId)
        {
            RetryOperationSummary summary;
            if (!Operations.TryGetValue(RetryOperationSummary.MakeOperationId(requestId, retryType), out summary))
            {
                summary = new RetryOperationSummary(requestId, retryType) { Notifier = notifier };
                Operations[RetryOperationSummary.MakeOperationId(requestId, retryType)] = summary;
            }
            return summary;
        }

        private static RetryOperationSummary Get(string requestId, RetryType retryType)
        {
            return Operations[RetryOperationSummary.MakeOperationId(requestId, retryType)];
        }
        
        public RetryOperationSummary GetStatusForRetryOperation(string requestId, RetryType retryType)
        {
            RetryOperationSummary summary;
            Operations.TryGetValue(RetryOperationSummary.MakeOperationId(requestId, retryType), out summary);

            return summary;
        }
    }
}