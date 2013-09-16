namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using NServiceBus.UnitOfWork;
    using Raven.Client;

    public class RavenUnitOfWork : IManageUnitsOfWork
    {
        public RavenUnitOfWork(IDocumentStore store)
        {
            this.store = store;
        }

        public IDocumentSession Session { get; private set; }

        public void Begin()
        {
            Session = store.OpenSession();
        }

        public void End(Exception ex = null)
        {
            if (ex == null)
            {
                Session.SaveChanges();
            }
        }

        readonly IDocumentStore store;
    }
}