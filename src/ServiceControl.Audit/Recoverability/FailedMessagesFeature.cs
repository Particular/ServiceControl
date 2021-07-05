namespace ServiceControl.Audit.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Auditing;
    using Contracts.MessageFailures;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

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

        class DetectSuccessfullRetriesEnricher : IEnrichImportedAuditMessages
        {
            public void Enrich(AuditEnricherContext context)
            {
                var headers = context.Headers;
                var isOldRetry = headers.TryGetValue("ServiceControl.RetryId", out _);
                var isNewRetry = headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var newRetryMessageId);
                var isAckHandled = headers.ContainsKey("ServiceControl.Retry.AcknowledgementSent");
                var hasAckQueue = headers.TryGetValue("ServiceControl.Retry.AcknowledgementQueue", out var ackQueue);

                var hasBeenRetried = isOldRetry || isNewRetry;

                context.Metadata.Add("IsRetried", hasBeenRetried);

                if (!hasBeenRetried || isAckHandled)
                {
                    //The message has not been sent for retry from ServiceControl or the endpoint indicated that is already has sent a retry acknowledgement to the 
                    //ServiceControl main instance. Nothing to do.
                    return;
                }

                if (hasAckQueue && isNewRetry)
                {
                    //The message has been sent for retry from ServiceControl 4.20 or higher (has the ACK queue header) but the endpoint did not recognized the header
                    //and did not sent the acknowledgement. We send it here to the error queue of the main instance.
                    var ackMessage = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>
                    {
                        ["ServiceControl.Retry.Successful"] = "true",
                        ["ServiceControl.Retry.UniqueMessageId"] = newRetryMessageId
                    }, new byte[0]);
                    var ackOperation = new TransportOperation(ackMessage, new UnicastAddressTag(ackQueue));
                    context.AddForSend(ackOperation);
                }
                else
                {
                    //The message has been sent for retry from ServiceControl older than 4.20. Regardless which version the endpoint was, we need to send a legacy confirmation
                    //message because the main instance of ServiceControl may still be on version lower than 4.19.
                    context.AddForSend(new MarkMessageFailureResolvedByRetry
                    {
                        FailedMessageId = isOldRetry ? headers.UniqueId() : newRetryMessageId,
                        AlternativeFailedMessageIds = GetAlternativeUniqueMessageId(headers).ToArray()
                    });
                }
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
        }
    }
}