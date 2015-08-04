namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    [Serializable]
    public class Subscriptions
    {
        public static Action<Action<SubscriptionEventArgs>, Action> OnEndpointSubscribed = (actionToPerformIfMessageDrivenSubscriptions, actionToPerformIfMessageDrivenSubscriptionsNotRequired) =>
        {
            if (Feature.IsEnabled<MessageDrivenSubscriptions>())
            {
                Configure.Instance.Builder.Build<MessageDrivenSubscriptionManager>().ClientSubscribed +=
                    (sender, args) =>
                    {
                        actionToPerformIfMessageDrivenSubscriptions(args);
                    };

                return;
            }

            actionToPerformIfMessageDrivenSubscriptionsNotRequired();
        };
    }
}