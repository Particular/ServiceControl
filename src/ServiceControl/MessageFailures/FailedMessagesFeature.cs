namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;
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

                DomainEvents.Raise(new MessageFailureResolvedByRetry
                {
                    FailedMessageId = isOldRetry ? headers.UniqueId() : newRetryMessageId,
                    AlternativeFailedMessageIds = GetAlternativeUniqueMessageId(headers).ToArray()
                });
            }

            private IEnumerable<string> GetAlternativeUniqueMessageId(IReadOnlyDictionary<string, string> headers)
            {
                var messageId = headers.MessageId();
                string processingEndpoint;
                if (headers.TryGetValue(Headers.ProcessingEndpoint, out processingEndpoint))
                {
                    yield return DeterministicGuid.MakeId(messageId, processingEndpoint).ToString();
                }

                string failedQ;
                if (headers.TryGetValue("NServiceBus.FailedQ", out failedQ))
                {
                    yield return DeterministicGuid.MakeId(messageId, Address.Parse(failedQ).Queue).ToString();
                }

                string replyToAddress;
                if (headers.TryGetValue(Headers.ReplyToAddress, out replyToAddress))
                {
                    yield return DeterministicGuid.MakeId(messageId, Address.Parse(replyToAddress).Queue).ToString();
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