namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using Raven.Client;

    class CustomChecksHostedService : IHostedService
    {
        IDocumentStore store;
        CustomCheckNotifications notifications;
        IDisposable subscription;

        public CustomChecksHostedService(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            notifications = new CustomCheckNotifications(store, domainEvents);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            subscription = store.Changes().ForIndex(new CustomChecksIndex().IndexName).Subscribe(notifications);
            return Task.FromResult(0);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            subscription.Dispose();
            return Task.FromResult(0);
        }
    }
}