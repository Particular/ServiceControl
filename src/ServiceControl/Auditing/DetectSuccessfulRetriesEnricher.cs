namespace ServiceControl.Auditing
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class DetectSuccessfulRetriesEnricher : IEnrichImportedAuditMessages
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
                // The acknowledgement queue is named by whichever instance issued the retry, so this stays a
                // transport operation rather than a direct write to the local recoverability unit of work.
                // In a combined host it simply comes back in through local error ingestion.
                var ackMessage = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>
                {
                    ["ServiceControl.Retry.Successful"] = DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow),
                    ["ServiceControl.Retry.UniqueMessageId"] = newRetryMessageId
                }, Array.Empty<byte>());
                var ackOperation = new TransportOperation(ackMessage, new UnicastAddressTag(ackQueue));
                context.AddForSend(ackOperation);
            }
        }
    }
}
