namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Infrastructure.DomainEvents;

    public class RetryingManager
    {
        IDomainEvents domainEvents;
        internal static Dictionary<string, InMemoryRetry> RetryOperations = new Dictionary<string, InMemoryRetry>();

        public RetryingManager(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public async Task Wait(string requestId, RetryType retryType, DateTime started, string originator = null, string classifier = null, DateTime? last = null)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            await summary.Wait(started, originator, classifier, last);
        }

        public bool IsOperationInProgressFor(string requestId, RetryType retryType)
        {
            InMemoryRetry summary;
            if (!RetryOperations.TryGetValue(InMemoryRetry.MakeOperationId(requestId, retryType), out summary))
            {
                return false;
            }

            return summary.IsInProgress();
        }

        public bool IsRetryInProgressFor(string requestId)
        {
            return RetryOperations.Keys.Where(key => key.EndsWith($"/{requestId}")).Any(key => RetryOperations[key].IsInProgress());
        }

        public async Task Prepairing(string requestId, RetryType retryType, int totalNumberOfMessages)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            await summary.Prepare(totalNumberOfMessages);
        }

        public async Task PreparedAdoptedBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, string originator, string classifier, DateTime startTime, DateTime last)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            await summary.Prepare(totalNumberOfMessages).ConfigureAwait(false);
            await summary.PrepareAdoptedBatch(numberOfMessagesPrepared, originator, classifier, startTime, last).ConfigureAwait(false);
        }

        public async Task PreparedBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            await summary.PrepareBatch(numberOfMessagesPrepared).ConfigureAwait(false);
        }

        public async Task Forwarding(string requestId, RetryType retryType)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = Get(requestId, retryType);

            await summary.Forwarding().ConfigureAwait(false);
        }

        public async Task ForwardedBatch(string requestId, RetryType retryType, int numberOfMessagesForwarded)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = Get(requestId, retryType);

            await summary.BatchForwarded(numberOfMessagesForwarded)
                .ConfigureAwait(false);
        }

        public void Fail(RetryType retryType, string requestId)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            summary.Fail();
        }

        public async Task Skip(string requestId, RetryType retryType, int numberOfMessagesSkipped)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);
            await summary.Skip(numberOfMessagesSkipped)
                .ConfigureAwait(false);
        }

        private InMemoryRetry GetOrCreate(RetryType retryType, string requestId)
        {
            InMemoryRetry summary;
            if (!RetryOperations.TryGetValue(InMemoryRetry.MakeOperationId(requestId, retryType), out summary))
            {
                summary = new InMemoryRetry(requestId, retryType, domainEvents);
                RetryOperations[InMemoryRetry.MakeOperationId(requestId, retryType)] = summary;
            }
            return summary;
        }

        private static InMemoryRetry Get(string requestId, RetryType retryType)
        {
            return RetryOperations[InMemoryRetry.MakeOperationId(requestId, retryType)];
        }

        public InMemoryRetry GetStatusForRetryOperation(string requestId, RetryType retryType)
        {
            InMemoryRetry summary;
            RetryOperations.TryGetValue(InMemoryRetry.MakeOperationId(requestId, retryType), out summary);

            return summary;
        }
    }
}