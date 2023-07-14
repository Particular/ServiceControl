namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using Persistence.MessageRedirects;
    using Raven.Client;
    using ServiceControl.Recoverability;

    class RetryBatchesManager : AbstractSessionManager, IRetryBatchesManager
    {
        public RetryBatchesManager(IAsyncDocumentSession session) : base(session)
        {
        }

        public void Delete(RetryBatch retryBatch) => Session.Delete(retryBatch);

        public void Delete(RetryBatchNowForwarding forwardingBatch) => Session.Delete(forwardingBatch);

        public Task<IList<FailedMessageRetry>> GetFailedMessageRetries(IList<string> stagingBatchFailureRetries) => throw new NotImplementedException();

        public void Evict(FailedMessageRetry failedMessageRetry) => Session.Advanced.Evict(failedMessageRetry);

        public Task<IList<FailedMessage>> GetFailedMessages(Dictionary<string, FailedMessageRetry>.KeyCollection keys) => throw new NotImplementedException();

        public async Task<RetryBatchNowForwarding> GetRetryBatchNowForwarding() =>
            await Session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .LoadAsync<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id)
                .ConfigureAwait(false);

        public async Task<RetryBatch> GetRetryBatch(string retryBatchId, CancellationToken cancellationToken) =>
            await Session.LoadAsync<RetryBatch>(retryBatchId, cancellationToken)
                .ConfigureAwait(false);

        public async Task<RetryBatch> GetStagingBatch() =>
            await Session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .FirstOrDefaultAsync(b => b.Status == RetryBatchStatus.Staging)
                .ConfigureAwait(false);

        public async Task Store(RetryBatchNowForwarding retryBatchNowForwarding, string stagingBatchId) =>
            await Session.StoreAsync(new RetryBatchNowForwarding
            {
                RetryBatchId = stagingBatchId
            }, RetryBatchNowForwarding.Id).ConfigureAwait(false);

        public async Task<MessageRedirectsCollection> GetOrCreateMessageRedirectsCollection()
        {
            var redirects = await Session.LoadAsync<MessageRedirectsCollection>(MessageRedirectsCollection.DefaultId).ConfigureAwait(false);

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