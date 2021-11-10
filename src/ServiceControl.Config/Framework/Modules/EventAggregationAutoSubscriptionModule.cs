namespace ServiceControl.Config.Framework.Modules
{
    using System;
    using System.Linq;
    using Autofac;
    using Autofac.Core;
    using Autofac.Core.Registration;
    using Autofac.Core.Resolving.Pipeline;
    using Caliburn.Micro;

    public class EventAggregationAutoSubscriptionModule : Module
    {
        protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistry, IComponentRegistration registration)
        {
            registration.PipelineBuilding += (sender, builder) => builder.Use(new AutosubscribeMiddleware());
        }

        class AutosubscribeMiddleware : IResolveMiddleware
        {
            public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
            {
                next(context);

                if (IsHandler(context.Instance))
                {
                    context.Resolve<IEventAggregator>().SubscribeOnPublishedThread(context.Instance);
                }
            }

            static bool IsHandler(object obj)
            {
                var interfaces = obj.GetType().GetInterfaces()
                    .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IHandle<>));
                return interfaces.Any();
            }

            public PipelinePhase Phase => PipelinePhase.Activation;
        }

        static void OnComponentActivated(object sender, ActivatedEventArgs<object> e)
        {
        }

    }
}