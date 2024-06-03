namespace ServiceControl.MessageFailures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Api;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using Persistence;
    using Persistence.RavenDB;
    using Raven.Client.Documents;

    class FailedMessageViewIndexNotifications(IRavenSessionProvider sessionProvider, IRavenDocumentStoreProvider documentStoreProvider) : IFailedMessageViewIndexNotifications
        , IDisposable
        , IHostedService
    {
        void OnNext()
        {
            try
            {
                UpdatedCount().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to emit MessageFailuresUpdated", ex);
            }
        }

        async Task UpdatedCount()
        {
            using var session = await sessionProvider.OpenSession();
            var failedUnresolvedMessageCount = await session
                .Query<FailedMessage, FailedMessageViewIndex>()
                .CountAsync(p => p.Status == FailedMessageStatus.Unresolved);

            var failedArchivedMessageCount = await session
                .Query<FailedMessage, FailedMessageViewIndex>()
                .CountAsync(p => p.Status == FailedMessageStatus.Archived);

            if (lastUnresolvedCount == failedUnresolvedMessageCount && lastArchivedCount == failedArchivedMessageCount)
            {
                return;
            }

            lastUnresolvedCount = failedUnresolvedMessageCount;
            lastArchivedCount = failedArchivedMessageCount;

            if (subscriber != null)
            {
                await subscriber(new FailedMessageTotals
                {
                    ArchivedTotal = failedArchivedMessageCount,
                    UnresolvedTotal = failedUnresolvedMessageCount
                });
            }
        }

        public IDisposable Subscribe(Func<FailedMessageTotals, Task> callback)
        {
            if (subscriber is not null)
            {
                throw new InvalidOperationException("Already a subscriber, only a single subscriber supported");
            }

            subscriber = callback ?? throw new ArgumentNullException(nameof(callback));
            return this;
        }

        public void Dispose()
        {
            subscriber = null;
            subscription?.Dispose();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            subscription = documentStore
                .Changes()
                .ForIndex(new FailedMessageViewIndex().IndexName)
                .Subscribe(d => OnNext());
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedMessageViewIndexNotifications));

        Func<FailedMessageTotals, Task> subscriber;
        IDisposable subscription;
        int lastUnresolvedCount;
        int lastArchivedCount;
    }
}