namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using Raven.Client;

    /// <summary>
    /// Not that we use a logical message behavior to enable uow for our import satellites
    /// </summary>
#pragma warning disable 618
    class RavenUnitOfWorkBehavior : IBehavior<IncomingContext>
    {
        public IDocumentStore Store { get; set; }

        public void Invoke(IncomingContext context, Action next)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;
                
                context.Set(session);

                next();

                session.SaveChanges();
            }
        }

        //todo: not sure we need this any more since there is a uow in the external raven support already
        class Registration:RegisterStep
        {
            public Registration()
                : base("RavenUnitOfWork", typeof(RavenUnitOfWorkBehavior), "Unit of work support for RavenDB")
            {
                InsertAfter(WellKnownStep.CreateChildContainer);
                InsertBefore(WellKnownStep.DeserializeMessages);
            }
        }
    }

#pragma warning restore 618
}