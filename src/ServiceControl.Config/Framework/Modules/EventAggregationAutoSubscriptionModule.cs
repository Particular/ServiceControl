namespace ServiceControl.Config.Framework.Modules
{
    using System.Linq;
    using Autofac;
    using Autofac.Core;
    using Autofac.Core.Registration;
    using Autofac.Core.Resolving.Pipeline;
    using Caliburn.Micro;

    public class EventAggregationAutoSubscriptionModule : Module
    {
        protected override void AttachToComponentRegistration(IComponentRegistryBuilder registryBuilder, IComponentRegistration registration)
        {
            registration.PipelineBuilding += (sender, builder) =>
                builder.Use(PipelinePhase.Activation, (context, callback) =>
                {
                    callback(context);
                    if (IsHandler(context.Instance))
                    {
                        context.Resolve<IEventAggregator>().SubscribeOnPublishedThread(context.Instance);
                    }
                });
        }

        static bool IsHandler(object obj)
        {
            var interfaces = obj.GetType().GetInterfaces()
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IHandle<>));
            return interfaces.Any();
        }
    }
}