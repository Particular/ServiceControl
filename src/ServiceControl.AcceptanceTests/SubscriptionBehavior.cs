namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Unicast.Subscriptions;

    static class SubscriptionBehaviorExtensions
    {
        public static void OnEndpointSubscribed(this BusConfiguration b, Action<SubscriptionEventArgs> action)
        {
            b.Pipeline.Register<SubscriptionBehavior.Registration>();
            
            b.RegisterComponents(c => c.ConfigureComponent(builder => new SubscriptionBehavior(action), DependencyLifecycle.InstancePerCall));
        }
    }

    class SubscriptionBehavior : IBehavior<IncomingContext>
    {
        readonly Action<SubscriptionEventArgs> action;

        public SubscriptionBehavior(Action<SubscriptionEventArgs> action)
        {
            this.action = action;
        }

        public void Invoke(IncomingContext context, Action next)
        {
            next();
            var subscriptionMessageType = GetSubscriptionMessageTypeFrom(context.PhysicalMessage);
            if (subscriptionMessageType != null)
            {
                action(new SubscriptionEventArgs
                {
                    MessageType = subscriptionMessageType,
                    SubscriberReturnAddress = context.PhysicalMessage.ReplyToAddress
                });
            }
        }

        static string GetSubscriptionMessageTypeFrom(TransportMessage msg)
        {
            return (from header in msg.Headers where header.Key == Headers.SubscriptionMessageType select header.Value).FirstOrDefault();
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("SubscriptionBehavior", typeof(SubscriptionBehavior), "So we can get subscription events")
            {
                InsertBefore(WellKnownStep.CreateChildContainer);
            }
        }
    }
}