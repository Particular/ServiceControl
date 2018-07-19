﻿namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class CustomCheckNotifications : IObserver<IndexChangeNotification>
    {
        public CustomCheckNotifications(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public void OnNext(IndexChangeNotification value)
        {
            try
            {
                UpdateCount().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logging.WarnFormat("Failed to emit CustomCheckUpdated - {0}", ex);
            }
        }

        public void OnError(Exception error)
        {
            //Ignore
        }

        public void OnCompleted()
        {
            //Ignore
        }

        async Task UpdateCount()
        {
            using (var session = store.OpenAsyncSession())
            {
                var failedCustomCheckCount = await session.Query<CustomCheck, CustomChecksIndex>().CountAsync(p => p.Status == Status.Fail)
                    .ConfigureAwait(false);
                if (lastCount == failedCustomCheckCount)
                {
                    return;
                }

                lastCount = failedCustomCheckCount;
                await domainEvents.Raise(new CustomChecksUpdated
                {
                    Failed = lastCount
                }).ConfigureAwait(false);
            }
        }

        IDomainEvents domainEvents;
        IDocumentStore store;
        int lastCount;
        ILog logging = LogManager.GetLogger(typeof(CustomCheckNotifications));
    }
}