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
    class RavenUnitOfWorkBehavior : IBehavior<ReceiveLogicalMessageContext>
    {
        public void Invoke(ReceiveLogicalMessageContext context, Action next)
        {
            using (var session = context.Builder.Build<IDocumentSession>())
            {
                next();

                session.SaveChanges();
            }
        }
    }
   class RavenUnitOfWorkBehaviorPipelineOverride:PipelineOverride
    {
       public override void Override(BehaviorList<ReceiveLogicalMessageContext> behaviorList)
       {
           behaviorList.Add<RavenUnitOfWorkBehavior>();
       }
    }
#pragma warning restore 618
}