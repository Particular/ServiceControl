namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;

    public class RetryOperationManager
    {
        private readonly IRetryOperationProgressNotifier notifier;
        
        public RetryOperationManager(IRetryOperationProgressNotifier notifier)
        {
            this.notifier = notifier;
        }

        internal static Dictionary<string, RetryOperation> Operations = new Dictionary<string, RetryOperation>();

        public void Wait(string requestId, RetryType retryType, DateTime started, string originator = null, string classifier = null, DateTime? last = null)
        {
            var summary = GetOrCreate(retryType, requestId);

            summary.Wait(started, originator, classifier, last);
        }

        public bool IsOperationInProgressFor(string requestId, RetryType retryType)
        {
            RetryOperation summary;
            if (!Operations.TryGetValue(RetryOperation.MakeOperationId(requestId, retryType), out summary))
            {
                return false;
            }

            return summary.RetryState != RetryState.Completed && summary.RetryState != RetryState.Waiting;
        }

        public void Prepairing(string requestId, RetryType retryType, int totalNumberOfMessages)
        {
            var summary = GetOrCreate(retryType, requestId);

            summary.Prepare(totalNumberOfMessages);
        }

        public void PreparedAdoptedBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, string originator, string classifier, DateTime startTime, DateTime last)
        {
            var summary = GetOrCreate(retryType, requestId);

            summary.Prepare(totalNumberOfMessages);
            summary.PrepareAdoptedBatch(numberOfMessagesPrepared, originator, classifier, startTime, last);
        }

        public void PreparedBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared)
        {
            var summary = GetOrCreate(retryType, requestId);

            summary.PrepareBatch(numberOfMessagesPrepared);
        }

        public void Forwarding(string requestId, RetryType retryType)
        {
            var summary = Get(requestId, retryType);

            summary.Forwarding();
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

        private RetryOperation GetOrCreate(RetryType retryType, string requestId)
        {
            RetryOperation summary;
            if (!Operations.TryGetValue(RetryOperation.MakeOperationId(requestId, retryType), out summary))
            {
                summary = new RetryOperation(requestId, retryType) { Notifier = notifier };
                Operations[RetryOperation.MakeOperationId(requestId, retryType)] = summary;
            }
            return summary;
        }

        private static RetryOperation Get(string requestId, RetryType retryType)
        {
            return Operations[RetryOperation.MakeOperationId(requestId, retryType)];
        }
        
        public RetryOperation GetStatusForRetryOperation(string requestId, RetryType retryType)
        {
            RetryOperation summary;
            Operations.TryGetValue(RetryOperation.MakeOperationId(requestId, retryType), out summary);

            return summary;
        }
    }
}