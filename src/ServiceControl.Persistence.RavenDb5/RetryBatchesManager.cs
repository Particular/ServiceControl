namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using Persistence.MessageRedirects;
    using ServiceControl.Recoverability;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;

    class RetryBatchesManager : AbstractSessionManager, IRetryBatchesManager
    {
        public RetryBatchesManager(IAsyncDocumentSession session) : base(session)
        {
        }

        public void Delete(RetryBatch retryBatch) => Session.Delete(retryBatch);

        public void Delete(RetryBatchNowForwarding forwardingBatch) => Session.Delete(forwardingBatch);

        public async Task<FailedMessageRetry[]> GetFailedMessageRetries(IList<string> stagingBatchFailureRetries)
        {
            var result = await Session.LoadAsync<FailedMessageRetry>(stagingBatchFailureRetries);
            return result.Values.ToArray();
        }

        public void Evict(FailedMessageRetry failedMessageRetry) => Session.Advanced.Evict(failedMessageRetry);

        public async Task<FailedMessage[]> GetFailedMessages(Dictionary<string, FailedMessageRetry>.KeyCollection keys)
        {
            var result = await Session.LoadAsync<FailedMessage>(keys);
            return result.Values.ToArray();
        }

        public async Task<RetryBatchNowForwarding> GetRetryBatchNowForwarding() =>
            await Session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .LoadAsync<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id);

        public async Task<RetryBatch> GetRetryBatch(string retryBatchId, CancellationToken cancellationToken) =>
            await Session.LoadAsync<RetryBatch>(retryBatchId, cancellationToken);

        public async Task<RetryBatch> GetStagingBatch() =>
            await Session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .FirstOrDefaultAsync(b => b.Status == RetryBatchStatus.Staging);

        public async Task Store(RetryBatchNowForwarding retryBatchNowForwarding) =>
            await Session.StoreAsync(retryBatchNowForwarding, RetryBatchNowForwarding.Id);

        public async Task<MessageRedirectsCollection> GetOrCreateMessageRedirectsCollection()
        {
            var redirects = await Session.LoadAsync<MessageRedirectsCollection>(MessageRedirectsCollection.DefaultId);

            if (redirects != null)
            {
                redirects.ETag = Session.Advanced.GetChangeVectorFor(redirects);
                redirects.LastModified = Session.Advanced.GetLastModifiedFor(redirects)!.Value;
                return redirects;
            }

            return new MessageRedirectsCollection();
        }
    }
}