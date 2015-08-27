namespace ServiceControl.MessageFailures
{
    using System;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;
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
            IBus bus;

            public DetectSuccessfullRetriesEnricher(IBus bus)
            {
                this.bus = bus;
            }

            public override void Enrich(ImportMessage message)
            {
                if (!(message is ImportSuccessfullyProcessedMessage))
                {
                    return;
                }

                string oldRetryId;
                string newRetryMessageId;

                var isOldRetry = message.PhysicalMessage.Headers.TryGetValue("ServiceControl.RetryId", out oldRetryId);
                var isNewRetry = message.PhysicalMessage.Headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out newRetryMessageId);

                var hasBeenRetried = isOldRetry || isNewRetry;

                message.Metadata.Add("IsRetried", hasBeenRetried);

                if (!hasBeenRetried)
                {
                    return;
                }

                if (isOldRetry)
                {
                    bus.Publish<MessageFailureResolvedByRetry>(m => m.FailedMessageId = message.UniqueMessageId);
                }

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