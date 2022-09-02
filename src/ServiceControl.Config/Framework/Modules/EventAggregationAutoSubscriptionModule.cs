namespace ServiceControl.Config.Framework.Modules
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using Autofac;
    using Autofac.Core;
    using Autofac.Core.Registration;
    using Autofac.Core.Resolving.Pipeline;
    using Caliburn.Micro;
    using Rx;
    using UI.InstanceDetails;
    using Action = Caliburn.Micro.Action;

    public class EventAggregationAutoSubscriptionModule : Module
    {
        protected override void AttachToComponentRegistration(IComponentRegistryBuilder registryBuilder, IComponentRegistration registration)
        {
            registration.PipelineBuilding += (sender, builder) =>
            {
                builder.Use(PipelinePhase.Activation, (context, callback) =>
                {
                    callback(context);
                    if (IsHandler(context.Instance))
                    {
                        var eventAggregator = context.Resolve<IEventAggregator>();
                        eventAggregator.SubscribeOnPublishedThread(context.Instance);

                        if (context.Instance is RxScreen screen)
                        {
                            screen.EventAggregator = eventAggregator;
                        }
                    }
                });
            };
        }

        static bool IsHandler(object obj)
        {
            var interfaces = obj.GetType().GetInterfaces()
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IHandle<>));
            return interfaces.Any();
        }
    }
}