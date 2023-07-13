namespace ServiceControl.MessageFailures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Api;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using Persistence;
    using Raven.Client;

    class FailedMessageViewIndexNotifications  // TODO: Must be registered as single and as hosted service
        : IFailedMessageViewIndexNotifications
        , IDisposable
        , IHostedService
    {
        public FailedMessageViewIndexNotifications(IDocumentStore store)
        {
            this.store = store;
        }

        void OnNext()
        {
            try
            {
                UpdatedCount().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logging.WarnFormat("Failed to emit MessageFailuresUpdated - {0}", ex);
            }
        }

        async Task UpdatedCount()
        {
            using (var session = store.OpenAsyncSession())
            {
                var failedUnresolvedMessageCount = await session
                    .Query<FailedMessage, FailedMessageViewIndex>()
                    .CountAsync(p => p.Status == FailedMessageStatus.Unresolved)
                    .ConfigureAwait(false);

                var failedArchivedMessageCount = await session
                    .Query<FailedMessage, FailedMessageViewIndex>()
                    .CountAsync(p => p.Status == FailedMessageStatus.Archived)
                    .ConfigureAwait(false);

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
                    }).ConfigureAwait(false);
                }
            }
        }

        public IDisposable Subscribe(Func<FailedMessageTotals, Task> callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (!(subscriber is null))
            {
                throw new InvalidOperationException("Already a subscriber, only a single subscriber supported");
            }

            subscriber = callback;
            return this;
        }

        public void Dispose()
        {
            subscriber = null;
            subscription.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            subscription = store
                .Changes()
                .ForIndex(new FailedMessageViewIndex().IndexName)
                .Subscribe(d => OnNext());
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        readonly IDocumentStore store;
        readonly ILog logging = LogManager.GetLogger(typeof(FailedMessageViewIndexNotifications));

        Func<FailedMessageTotals, Task> subscriber;
        IDisposable subscription;
        int lastUnresolvedCount;
        int lastArchivedCount;
    }
}