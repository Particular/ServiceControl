namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Operations.BodyStorage;

    class ReturnToSender(IBodyStorage bodyStorage, ILogger<ReturnToSender> logger)
    {
        public virtual async Task HandleMessage(MessageContext message, IMessageDispatcher sender, string errorQueueTransportAddress, CancellationToken cancellationToken = default)
        {
            var outgoingHeaders = new Dictionary<string, string>(message.Headers);

            outgoingHeaders.Remove("ServiceControl.Retry.StagingId");
            outgoingHeaders["ServiceControl.Retry.AcknowledgementQueue"] = errorQueueTransportAddress;

            byte[] body = null;
            var messageId = message.NativeMessageId;

            logger.LogDebug("{MessageId}: Retrieving message body", messageId);

            if (outgoingHeaders.ContainsKey("ServiceControl.Retry.Attempt.MessageId"))
            {
                body = await FetchFromFailedMessage(outgoingHeaders, messageId, cancellationToken);
                outgoingHeaders.Remove("ServiceControl.Retry.Attempt.MessageId");
            }
            else
            {
                logger.LogWarning("{MessageId}: Can't find message body. Missing header ServiceControl.Retry.Attempt.MessageId", messageId);
            }

            var outgoingMessage = new OutgoingMessage(messageId, outgoingHeaders, body ?? EmptyBody);

            var destination = outgoingHeaders["ServiceControl.TargetEndpointAddress"];

            logger.LogDebug("{MessageId}: Forwarding message to {Destination}", messageId, destination);

            if (!outgoingHeaders.TryGetValue("ServiceControl.RetryTo", out var retryTo))
            {
                retryTo = destination;
                outgoingHeaders.Remove("ServiceControl.TargetEndpointAddress");
            }
            else
            {
                logger.LogDebug("{MessageId}: Found ServiceControl.RetryTo header. Rerouting to {RetryTo}", messageId, retryTo);
            }

            var transportOp = new TransportOperation(outgoingMessage, new UnicastAddressTag(retryTo));

            await sender.Dispatch(new TransportOperations(transportOp), message.TransportTransaction, cancellationToken);

            logger.LogDebug("{MessageId}: Forwarded message to {RetryTo}", messageId, retryTo);
        }

        async Task<byte[]> FetchFromFailedMessage(Dictionary<string, string> outgoingHeaders, string messageId, CancellationToken cancellationToken)
        {
            var uniqueMessageId = outgoingHeaders["ServiceControl.Retry.UniqueMessageId"];
            var result = await bodyStorage.TryFetch(uniqueMessageId, cancellationToken);

            if (result.State is MessageBodyState.NotFound or MessageBodyState.Unavailable)
            {
                throw new InvalidOperationException($"Cannot retry failed message {uniqueMessageId} because its body state is {result.State}.");
            }

            if (result.State == MessageBodyState.Empty)
            {
                return EmptyBody;
            }

            await using (result.Content.Stream)
            {
                using var memoryStream = new MemoryStream();
                await result.Content.Stream.CopyToAsync(memoryStream, cancellationToken);
                var body = memoryStream.ToArray();

                logger.LogDebug("{MessageId}: Body size: {MessageLength} bytes retrieved from failed message attachment", messageId, body.LongLength);
                return body;
            }
        }

        static readonly byte[] EmptyBody = Array.Empty<byte>();
    }
}
