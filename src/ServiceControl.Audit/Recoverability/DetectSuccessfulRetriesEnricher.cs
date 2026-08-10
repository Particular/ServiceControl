namespace ServiceControl.Audit.Recoverability
{
    using System;
    using System.Collections.Generic;
    using Auditing;
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
                //The message has been sent for retry from ServiceControl 4.20 or higher (has the ACK queue header) but the endpoint did not recognize the header
                //and did not send the acknowledgment. We send it here to the acknowledgment queue.
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