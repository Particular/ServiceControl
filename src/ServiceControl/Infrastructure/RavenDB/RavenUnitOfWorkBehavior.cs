namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using Raven.Client;

    /// <summary>
    /// Not that we use a logical message behavior to enable uow for our import satellites
    /// </summary>
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
    }

    class RavenRegisterStep : RegisterStep
    {
        public RavenRegisterStep()
            : base("Custom Raven Behavior", typeof(RavenUnitOfWorkBehavior), "Raven Behavior")
        {
            InsertBefore(WellKnownStep.ExecuteUnitOfWork);
        }
    }


    //class RavenUnitOfWorkBehaviorPipelineOverride:PipelineOverride
    //{
    //   public override void Override(BehaviorList<ReceiveLogicalMessageContext> behaviorList)
    //   {
    //       behaviorList.InnerList.Insert(0,typeof(RavenUnitOfWorkBehavior));
    //   }
    //}
}