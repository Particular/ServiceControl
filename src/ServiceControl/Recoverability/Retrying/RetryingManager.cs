namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Persistence;

    public class RetryingManager
    {
        public RetryingManager(IDomainEvents domainEvents, ILogger<RetryingManager> logger)
        {
            this.domainEvents = domainEvents;
            this.logger = logger;
        }

        public Task Wait(string requestId, RetryType retryType, DateTime started, string originator = null, string classifier = null, DateTime? last = null)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return Task.CompletedTask;
            }

            var summary = GetOrCreate(retryType, requestId);

            return summary.Wait(started, originator, classifier, last);
        }

        public bool IsOperationInProgressFor(string requestId, RetryType retryType)
        {
            if (!retryOperations.TryGetValue(InMemoryRetry.MakeOperationId(requestId, retryType), out var summary))
            {
                return false;
            }

            return summary.IsInProgress();
        }

        public bool IsRetryInProgressFor(string requestId)
        {
            return retryOperations.Values.Any(o => o.RequestId == requestId && o.IsInProgress());
        }

        public async Task Preparing(string requestId, RetryType retryType, int totalNumberOfMessages)
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

            await summary.Prepare(totalNumberOfMessages);
            await summary.PrepareAdoptedBatch(numberOfMessagesPrepared, originator, classifier, startTime, last);
        }

        public async Task PreparedBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            await summary.PrepareBatch(numberOfMessagesPrepared);
        }

        public async Task Forwarding(string requestId, RetryType retryType)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            await summary.Forwarding();
        }

        public async Task ForwardedBatch(string requestId, RetryType retryType, int numberOfMessagesForwarded)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            await summary.BatchForwarded(numberOfMessagesForwarded);
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
            await summary.Skip(numberOfMessagesSkipped);
        }

        InMemoryRetry GetOrCreate(RetryType retryType, string requestId)
        {
            var key = InMemoryRetry.MakeOperationId(requestId, retryType);
            return retryOperations.GetOrAdd(key, _ => new InMemoryRetry(requestId, retryType, domainEvents, logger));
        }

        public InMemoryRetry GetStatusForRetryOperation(string requestId, RetryType retryType)
        {
            retryOperations.TryGetValue(InMemoryRetry.MakeOperationId(requestId, retryType), out var summary);

            return summary;
        }

        IDomainEvents domainEvents;
        readonly ILogger<RetryingManager> logger;
        ConcurrentDictionary<string, InMemoryRetry> retryOperations = new ConcurrentDictionary<string, InMemoryRetry>();
    }
}