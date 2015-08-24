namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using Metrics;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using Raven.Client;

    /// <summary>
    /// Not that we use a logical message behavior to enable uow for our import satellites
    /// </summary>
    class RavenUnitOfWorkBehavior : IBehavior<IncomingContext>
    {
        private readonly Timer uowTimer = Metric.Timer( "RavenUnitOfWork ISession time", Unit.Requests );
        private readonly Timer saveChangesTimer = Metric.Timer( "RavenDB ISession.SaveChanges() time", Unit.Requests );
        private readonly Timer executeUnitOfWorkTimer = Metric.Timer( "ExecuteUnitOfWork time", Unit.Requests );

        public IDocumentStore Store { get; set; }

        public void Invoke(IncomingContext context, Action next)
        {
            using (uowTimer.NewContext())
            {
                using (var session = Store.OpenSession())
                {
                    session.Advanced.UseOptimisticConcurrency = true;

                    context.Set(session);

                    using( executeUnitOfWorkTimer.NewContext() )
                    {
                        next();
                    }

                    using (saveChangesTimer.NewContext())
                    {
                        session.SaveChanges();
                    }
                }
            }
        }
    }

    class RavenRegisterStep : RegisterStep
    {
        public RavenRegisterStep()
            : base("Custom Raven Behavior", typeof(RavenUnitOfWorkBehavior), "Raven Behavior")
        {
            InsertAfter(WellKnownStep.CreateChildContainer);
            InsertBefore(WellKnownStep.ExecuteUnitOfWork);
        }
    }
}