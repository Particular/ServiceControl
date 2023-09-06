namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using Persistence.MessageRedirects;
    using ServiceControl.Recoverability;
    using Raven.Client;
    using Raven.Client.Documents.Session;

    class RetryBatchesManager : AbstractSessionManager, IRetryBatchesManager
    {
        public RetryBatchesManager(IAsyncDocumentSession session) : base(session)
        {
        }

        public void Delete(RetryBatch retryBatch) => Session.Delete(retryBatch);

        public void Delete(RetryBatchNowForwarding forwardingBatch) => Session.Delete(forwardingBatch);

        public Task<FailedMessageRetry[]> GetFailedMessageRetries(IList<string> stagingBatchFailureRetries) => Session.LoadAsync<FailedMessageRetry>(stagingBatchFailureRetries);

        public void Evict(FailedMessageRetry failedMessageRetry) => Session.Advanced.Evict(failedMessageRetry);

        public Task<FailedMessage[]> GetFailedMessages(Dictionary<string, FailedMessageRetry>.KeyCollection keys) => Session.LoadAsync<FailedMessage>(keys);

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
                redirects.ETag = Session.Advanced.GetEtagFor(redirects);
                redirects.LastModified = Session.Advanced.GetMetadataFor(redirects).Value<DateTime>("Last-Modified");

                return redirects;
            }

            return new MessageRedirectsCollection();
        }
    }
}