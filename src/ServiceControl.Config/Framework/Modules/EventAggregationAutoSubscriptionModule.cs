namespace ServiceControl.Config.Framework.Modules
{
    using Autofac;
    using Autofac.Core;
    using Caliburn.Micro;

    public class EventAggregationAutoSubscriptionModule : Module
    {
        protected override void AttachToComponentRegistration(IComponentRegistry registry, IComponentRegistration registration)
        {
            registration.Activated += OnComponentActivated;
        }

        static void OnComponentActivated(object sender, ActivatedEventArgs<object> e)
        {
            if (e.Instance is IHandle handler)
            {
                e.Context.Resolve<IEventAggregator>().Subscribe(handler);
            }
        }
    }
}