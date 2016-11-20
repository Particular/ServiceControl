namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Operations;

    public class FailedMessagesFeature : Feature
    {
        public FailedMessagesFeature()
        {
            EnableByDefault();
            RegisterStartupTask<WireUpFailedMessageNotifications>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<FailedMessageViewIndexNotifications>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<DetectSuccessfullRetriesEnricher>(DependencyLifecycle.SingleInstance);
        }

        class DetectSuccessfullRetriesEnricher : ImportEnricher
        {
            public override bool EnrichErrors => false;

            IBus bus;

            public DetectSuccessfullRetriesEnricher(IBus bus)
            {
                this.bus = bus;
            }

            public override void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                string oldRetryId;
                string newRetryMessageId;

                var isOldRetry = headers.TryGetValue("ServiceControl.RetryId", out oldRetryId);
                var isNewRetry = headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out newRetryMessageId);

                var hasBeenRetried = isOldRetry || isNewRetry;

                metadata.Add("IsRetried", hasBeenRetried);

                if (!hasBeenRetried)
                {
                    return;
                }

                bus.Publish<MessageFailureResolvedByRetry>(m => m.FailedMessageId = headers.UniqueId());

                if (isNewRetry)
                {
                    bus.Publish<MessageFailureResolvedByRetry>(m => m.FailedMessageId = newRetryMessageId);
                }
            }
        }

        class WireUpFailedMessageNotifications : FeatureStartupTask
        {
            FailedMessageViewIndexNotifications notifications;
            IDocumentStore store;
            IDisposable subscription;

            public WireUpFailedMessageNotifications(FailedMessageViewIndexNotifications notifications, IDocumentStore store)
            {
                this.notifications = notifications;
                this.store = store;
            }

            protected override void OnStart()
            {
                subscription = store.Changes().ForIndex(new FailedMessageViewIndex().IndexName).Subscribe(notifications);
            }

            protected override void OnStop()
            {
                subscription.Dispose();
            }
        }
    }
}