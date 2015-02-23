namespace ServiceControl.Migrations
{
    using System;
    using Raven.Abstractions;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public abstract class ExpiredDocumentMigration<T> : Migration<T>
    {
        DateTime expiryThreshold;

        protected ExpiredDocumentMigration(IDocumentStore store)
            : this(store, TimeSpan.FromHours(Settings.HoursToKeepMessagesBeforeExpiring), TimeSpan.FromMinutes(5))
        {
        }

        protected ExpiredDocumentMigration(IDocumentStore store, TimeSpan timeToKeepMessagesBeforeExpiring, TimeSpan timerPeriod)
            : base(store, timerPeriod)
        {
            expiryThreshold = SystemTime.UtcNow.Add(-timeToKeepMessagesBeforeExpiring);
        }

        protected override void Migrate(T document, IDocumentSession updateSession, Func<bool> shouldCancel)
        {
            Migrate(document, updateSession, expiryThreshold, shouldCancel);
        }

        protected abstract void Migrate(T document, IDocumentSession updateSession, DateTime expiryDate, Func<bool> shouldCancel);
    }
}