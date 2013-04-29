namespace ServiceBus.Management.RavenDB
{
    using System;
    using NServiceBus.UnitOfWork;
    using Raven.Client;

    public class RavenUnitOfWork : IManageUnitsOfWork
    {
        public IDocumentSession Session { get; set; }
        
        public void Begin(){}

        public void End(Exception ex = null)
        {
            if (ex == null)
                Session.SaveChanges();
        }
    }
}