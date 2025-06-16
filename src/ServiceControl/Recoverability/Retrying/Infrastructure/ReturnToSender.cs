namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using ServiceControl.Persistence;

    class ReturnToSender(IErrorMessageDataStore errorMessageStore, ILogger<ReturnToSender> logger)
    {
        public virtual async Task HandleMessage(MessageContext message, IMessageDispatcher sender, string errorQueueTransportAddress, CancellationToken cancellationToken = default)
        {
            var outgoingHeaders = new Dictionary<string, string>(message.Headers);

            outgoingHeaders.Remove("ServiceControl.Retry.StagingId");
            outgoingHeaders["ServiceControl.Retry.AcknowledgementQueue"] = errorQueueTransportAddress;

            byte[] body = null;
            var messageId = message.NativeMessageId;

            logger.LogDebug("{messageId}: Retrieving message body", messageId);

            if (outgoingHeaders.TryGetValue("ServiceControl.Retry.Attempt.MessageId", out var attemptMessageId))
            {
                body = await FetchFromFailedMessage(outgoingHeaders, messageId, attemptMessageId);
                outgoingHeaders.Remove("ServiceControl.Retry.Attempt.MessageId");
            }
            else
            {
                logger.LogWarning("{messageId}: Can't find message body. Missing header ServiceControl.Retry.Attempt.MessageId", messageId);
            }

            var outgoingMessage = new OutgoingMessage(messageId, outgoingHeaders, body ?? EmptyBody);

            var destination = outgoingHeaders["ServiceControl.TargetEndpointAddress"];

            logger.LogDebug("{messageId}: Forwarding message to {destination}", messageId, destination);

            if (!outgoingHeaders.TryGetValue("ServiceControl.RetryTo", out var retryTo))
            {
                retryTo = destination;
                outgoingHeaders.Remove("ServiceControl.TargetEndpointAddress");
            }
            else
            {
                logger.LogDebug("{messageId}: Found ServiceControl.RetryTo header. Rerouting to {retryTo}", messageId, retryTo);
            }

            var transportOp = new TransportOperation(outgoingMessage, new UnicastAddressTag(retryTo));

            await sender.Dispatch(new TransportOperations(transportOp), message.TransportTransaction, cancellationToken);

            logger.LogDebug("{messageId}: Forwarded message to {retryTo}", messageId, retryTo);
        }

        async Task<byte[]> FetchFromFailedMessage(Dictionary<string, string> outgoingHeaders, string messageId, string attemptMessageId)
        {
            var uniqueMessageId = outgoingHeaders["ServiceControl.Retry.UniqueMessageId"];
            byte[] body = await errorMessageStore.FetchFromFailedMessage(uniqueMessageId);

            if (body == null)
            {
                logger.LogWarning("{messageId}: Message Body not found in failed message with unique id {uniqueMessageId} for attempt Id {attemptMessageId}", messageId, uniqueMessageId, attemptMessageId);
            }
            else
            {
                logger.LogDebug("{messageId}: Body size: {messageLength} bytes retrieved from failed message attachment", messageId, body.LongLength);
            }

            return body;
        }

        static readonly byte[] EmptyBody = Array.Empty<byte>();
        readonly ILogger<ReturnToSender> logger = logger;
    }
}
