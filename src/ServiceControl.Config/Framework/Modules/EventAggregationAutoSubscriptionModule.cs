namespace ServiceControl.Config.Framework.Modules
{
    using System.Linq;
    using Autofac;
    using Autofac.Core;
    using Autofac.Core.Registration;
    using Caliburn.Micro;

    public class EventAggregationAutoSubscriptionModule : Module
    {
        protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistry, IComponentRegistration registration)
        {
            registration.Activated += OnComponentActivated;
        }

        static void OnComponentActivated(object sender, ActivatedEventArgs<object> e)
        {
            if (IsHandler(e.Instance))
            {
                e.Context.Resolve<IEventAggregator>().SubscribeOnPublishedThread(e.Instance);
            }
        }

        static bool IsHandler(object obj)
        {
            var interfaces = obj.GetType().GetInterfaces()
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IHandle<>));
            return interfaces.Any();
        }
    }
}