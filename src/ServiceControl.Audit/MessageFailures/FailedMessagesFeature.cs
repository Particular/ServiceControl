namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;

    class FailedMessagesFeature : Feature
    {
        public FailedMessagesFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<DetectSuccessfullRetriesEnricher>(DependencyLifecycle.SingleInstance);
        }

        class DetectSuccessfullRetriesEnricher : AuditImportEnricher
        {
            public DetectSuccessfullRetriesEnricher(IDomainEvents domainEvents)
            {
                this.domainEvents = domainEvents;
            }

            public override async Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var isOldRetry = headers.TryGetValue("ServiceControl.RetryId", out _);
                var isNewRetry = headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var newRetryMessageId);

                var hasBeenRetried = isOldRetry || isNewRetry;

                metadata.Add("IsRetried", hasBeenRetried);

                if (!hasBeenRetried)
                {
                    return;
                }

                await domainEvents.Raise(new MessageFailureResolvedByRetry
                {
                    FailedMessageId = isOldRetry ? headers.UniqueId() : newRetryMessageId,
                    AlternativeFailedMessageIds = GetAlternativeUniqueMessageId(headers).ToArray()
                }).ConfigureAwait(false);
            }

            IEnumerable<string> GetAlternativeUniqueMessageId(IReadOnlyDictionary<string, string> headers)
            {
                var messageId = headers.MessageId();
                if (headers.TryGetValue(Headers.ProcessingEndpoint, out var processingEndpoint))
                {
                    yield return DeterministicGuid.MakeId(messageId, processingEndpoint).ToString();
                }

                if (headers.TryGetValue("NServiceBus.FailedQ", out var failedQ))
                {
                    yield return DeterministicGuid.MakeId(messageId, ExtractQueueNameForLegacyReasons(failedQ)).ToString();
                }

                if (headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress))
                {
                    yield return DeterministicGuid.MakeId(messageId, ExtractQueueNameForLegacyReasons(replyToAddress)).ToString();
                }
            }

            static string ExtractQueueNameForLegacyReasons(string address)
            {
                var atIndex = address?.IndexOf("@", StringComparison.InvariantCulture);

                if (atIndex.HasValue && atIndex.Value > -1)
                {
                    return address.Substring(0, atIndex.Value);
                }

                return address;
            }

            IDomainEvents domainEvents;
        }
    }
}