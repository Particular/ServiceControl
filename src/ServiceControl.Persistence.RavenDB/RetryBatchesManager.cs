namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using Persistence.MessageRedirects;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using RavenDB;
    using ServiceControl.Recoverability;

    class RetryBatchesManager : AbstractSessionManager, IRetryBatchesManager
    {
        readonly ExpirationManager expirationManager;

        public RetryBatchesManager(IAsyncDocumentSession session, ExpirationManager expirationManager) : base(session)
        {
            this.expirationManager = expirationManager;
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

        public async Task<RetryBatch> GetStagingBatch()
        {
            return await Session.Query<RetryBatch>()
                .Include(b => b.FailureRetries)
                .FirstOrDefaultAsync(b => b.Status == RetryBatchStatus.Staging);
        }

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

        public Task CancelExpiration(FailedMessage failedMessage)
        {
            expirationManager.CancelExpiration(Session, failedMessage);
            return Task.CompletedTask;
        }
    }
}